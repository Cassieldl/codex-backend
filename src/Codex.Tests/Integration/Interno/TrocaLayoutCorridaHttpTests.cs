using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// A guarda <c>VERSAO_COM_CAMPOS</c> contava campos
/// FORA de lock — criar campo não toca a linha da versão, então o If-Match do PUT de troca de
/// layout não detectava a corrida. Reproduz as duas interleavings com um lock de banco real
/// (<c>SELECT ... FOR UPDATE</c> numa transação crua), não com <c>Task.Delay</c>: o teste só avança
/// depois de confirmar, via <c>pg_locks</c>, que o PUT está de fato bloqueado esperando a linha.
///
/// <para>
/// Prova por mutação (ver handoff 008): sem <c>VersaoProvedorRepositorioInterno.BloquearParaAlteracaoAsync</c>
/// e a recontagem dentro da transação, o PUT lê a contagem ANTES do commit da transação crua e
/// responde <c>200</c> — é exatamente esse cenário que o teste teria de pegar.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class TrocaLayoutCorridaHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "provedores-buscar,provedores-adicionar,versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public TrocaLayoutCorridaHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CriacaoDeCampoConcorrenteDurenteTrocaDeLayout_PutRecontaSobLockEDevolve422()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var campoLayoutId = await CriarCampoLayoutAtivoAsync(layoutOrigem);
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        // 1. Trava a linha da versão numa transação crua — simula o lock que a criação de campo
        // concorrente também tomaria (CampoVersaoServicoInterno.CriarAsync chama o mesmo
        // BloquearParaAlteracaoAsync antes de inserir).
        await using var contextoTravado = _fixture.CriarContexto();
        await using var transacaoCrua = await contextoTravado.Database.BeginTransactionAsync();
        await contextoTravado.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM codex.versoesprovedor WHERE versaoprovedorid = {versao.Id} FOR UPDATE");

        // 2. Dispara o PUT de troca de layout em paralelo — ele tem de bloquear no lock, não passar direto.
        var putTask = Task.Run(() => EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDestino }, versao.Versao));

        await AguardarPutBloqueadoNoLockAsync(versao.Id);

        // 3. Com a versão ainda travada por mim, insere o CampoVersao que o editor concorrente
        // estaria criando, e libero o lock.
        contextoTravado.CamposVersao.Add(
            CampoVersao.Criar(versao.Id, campoLayoutId, "texto", EstadoPreenchimento.Obrigatorio));
        await contextoTravado.SaveChangesAsync();
        await transacaoCrua.CommitAsync();

        // 4. Só agora o PUT consegue o lock, reconta (vê o campo que acabou de ser commitado) e
        // tem de recusar — se contasse antes do lock, veria 0 e trocaria o layout.
        var (status, corpo, _) = await putTask;

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "a recontagem sob lock tem de enxergar o campo que a transação concorrente acabou de commitar");
        CodigoDe(corpo).Should().Be(CodigosErro.VersaoComCampos);
        MensagemDe(corpo).Should().NotContain("uq_codex_").And.NotContain("at Codex.",
            because: "nome de constraint e stack trace são detalhe interno do schema (CWE-209, rules/security.md)");

        (await ObterVersaoLayoutIdAsync(versao.Id)).Should().Be(layoutOrigem,
            because: "a troca não pode ter sido aplicada — a transação inteira do PUT volta atrás");
    }

    // ------------------------------------------------------------------ apoio

    private sealed record VersaoCriada(int Id, string Rotulo, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string? CodigoDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("code").GetString();

    private static string? MensagemDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("message").GetString();

    /// <summary>
    /// Poll determinístico via <c>pg_stat_activity</c> — nunca <c>Task.Delay</c> fixo.
    /// <c>pg_locks</c> não serve aqui: uma sessão esperando a liberação de uma linha travada por
    /// <c>FOR UPDATE</c> espera num lock de <c>locktype = 'transactionid'</c> (a XID da transação
    /// dona da linha), que não carrega <c>relation</c> — só <c>wait_event_type/query</c> em
    /// <c>pg_stat_activity</c> identifica de forma portátil "alguém está parado nesta query". Timeout
    /// generoso (10s) porque Testcontainers pode ser lento para o pool devolver conexão nova ao PUT.
    /// </summary>
    private async Task AguardarPutBloqueadoNoLockAsync(int versaoId)
    {
        var prazo = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < prazo)
        {
            await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
            await conexao.OpenAsync();

            await using var comando = conexao.CreateCommand();
            comando.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE wait_event_type = 'Lock'
                      AND query ILIKE '%versoesprovedor%FOR UPDATE%'
                )
                """;

            if ((bool)(await comando.ExecuteScalarAsync())!)
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"O PUT não chegou a bloquear esperando o lock da versão {versaoId} dentro do prazo — " +
            "ou o lock da versão não está mais sendo adquirido no caminho de troca de layout.");
    }

    private async Task<int> CriarVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout corrida {NovoSufixo()}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        return versaoLayout.Id;
    }

    private async Task<int> CriarCampoLayoutAtivoAsync(int versaoLayoutId)
    {
        await using var contexto = _fixture.CriarContexto();

        var sufixo = NovoSufixo();
        var campoLayout = CampoLayout.Criar(versaoLayoutId, $"Campo {sufixo}", $"//Campo{sufixo}", "significado", ordem: 1);
        contexto.CamposLayout.Add(campoLayout);
        await contexto.SaveChangesAsync();

        return campoLayout.Id;
    }

    private async Task<int> CriarProvedorAsync()
    {
        var sufixo = NovoSufixo();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor corrida {sufixo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = Array.Empty<string>(),
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<VersaoCriada> CriarVersaoAsync(int provedorId, int versaoLayoutId)
    {
        var rotulo = $"Versao corrida {NovoSufixo()}";

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo,
        });

        status.Should().Be(HttpStatusCode.Created);
        var criada = corpo.GetProperty("data");

        return new VersaoCriada(
            criada.GetProperty("id").GetInt32(), rotulo, criada.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task<int> ObterVersaoLayoutIdAsync(int versaoId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.Id == versaoId)
            .Select(v => v.VersaoLayoutId)
            .SingleAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
