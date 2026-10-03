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
/// S-01 (auditoria de segurança da demanda 008) — o outro lado da corrida: o POST que cria campo de
/// versão também trava a linha da versão (<c>CampoVersaoServicoInterno.CriarAsync</c>) e REVALIDA o
/// vínculo campo canônico↔layout sob o lock, porque o FluentValidation leu o layout ANTES de
/// qualquer lock. Se uma troca de layout (PUT) commitar na janela entre a validação e o lock, a
/// revalidação sob lock tem de recusar — nunca inserir um <c>CampoVersao</c> cujo canônico pertence
/// ao layout que acabou de deixar de ser a origem desta versão.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CriarCampoVersaoCorridaHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "provedores-buscar,provedores-adicionar,versoesprovedor-buscar,versoesprovedor-adicionar,camposversao-adicionar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CriarCampoVersaoCorridaHttpTests(PostgresContainerFixture fixture)
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
    public async Task TrocaDeLayoutConcorrenteDuranteCriacaoDeCampo_RevalidaSobLockEDevolve422()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var campoLayoutIdDaOrigem = await CriarCampoLayoutAtivoAsync(layoutOrigem);
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        // 1. Trava a linha da versão — simula o lock que o PUT de troca de layout também tomaria.
        await using var contextoTravado = _fixture.CriarContexto();
        await using var transacaoCrua = await contextoTravado.Database.BeginTransactionAsync();
        await contextoTravado.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM codex.versoesprovedor WHERE versaoprovedorid = {versao.Id} FOR UPDATE");

        // 2. Dispara o POST de criação de campo em paralelo. O FluentValidation lê o layout ANTES
        // do lock (layoutOrigem, ainda válido agora) e passa; o service bloqueia no lock ao tentar
        // inserir.
        var postTask = Task.Run(() => EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versao.Id}/campos",
            new { campoLayoutId = campoLayoutIdDaOrigem, tipo = "texto", estadoPreenchimento = nameof(EstadoPreenchimento.Obrigatorio) }));

        await AguardarPostBloqueadoNoLockAsync(versao.Id);

        // 3. Com a versão ainda travada por mim, aplico a troca de layout que um PUT concorrente
        // estaria commitando, e libero o lock.
        var versaoTravada = await contextoTravado.VersoesProvedor.SingleAsync(v => v.Id == versao.Id);
        versaoTravada.TrocarVersaoLayout(layoutDestino);
        await contextoTravado.SaveChangesAsync();
        await transacaoCrua.CommitAsync();

        // 4. Só agora o POST consegue o lock, revalida (o campo pertence ao layout que DEIXOU de
        // ser a origem) e tem de recusar — se revalidasse só com o que o validator já tinha lido, inseriria.
        var (status, corpo, _) = await postTask;

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "a revalidação sob lock tem de ver a troca de layout que acabou de ser commitada");
        CodigoDe(corpo).Should().Be(CodigosErro.CampoLayoutOrigemAlterado);
        MensagemDe(corpo).Should().NotContain("uq_codex_").And.NotContain("at Codex.",
            because: "nome de constraint e stack trace são detalhe interno do schema (CWE-209, rules/security.md)");

        (await ContarCamposDaVersaoAsync(versao.Id)).Should().Be(0,
            because: "a transação inteira do POST volta atrás — nenhum CampoVersao órfão de layout pode sobrar");
    }

    // ------------------------------------------------------------------ apoio

    private sealed record VersaoCriada(int Id, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string? CodigoDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("code").GetString();

    private static string? MensagemDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("message").GetString();

    private async Task AguardarPostBloqueadoNoLockAsync(int versaoId)
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
            $"O POST não chegou a bloquear esperando o lock da versão {versaoId} dentro do prazo — " +
            "ou o lock da versão não está mais sendo adquirido no caminho de criação de campo.");
    }

    private async Task<int> CriarVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout corrida campo {NovoSufixo()}", maiorOrdem + 1);
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
            nome = $"Provedor corrida campo {sufixo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = Array.Empty<string>(),
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<VersaoCriada> CriarVersaoAsync(int provedorId, int versaoLayoutId)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo = $"Versao corrida campo {NovoSufixo()}",
        });

        status.Should().Be(HttpStatusCode.Created);
        var criada = corpo.GetProperty("data");

        return new VersaoCriada(criada.GetProperty("id").GetInt32(), criada.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task<int> ContarCamposDaVersaoAsync(int versaoProvedorId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.CamposVersao.AsNoTracking()
            .CountAsync(c => c.VersaoProvedorId == versaoProvedorId && c.DesativadoEm == null);
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
