using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// QA da demanda 008 (R6, reteste do QA-R6-01) — complementa <c>PutSoDeFilhosMoveOTokenHttpTests</c>:
/// (1) a conferência acontece NO BANCO (<c>UPDATE ... WHERE xmin</c>), não só na checagem prévia do
/// service — provado com uma corrida real; (2) o que um PUT idêntico ao estado atual faz com o
/// token; (3) o token não anda onde não deve (leitura, criação, publicação repetida); (4) o defeito à
/// parte do aviso: <c>campoVersaoIds</c> é aceito e devolvido, mas nunca gravado.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class TokenDeConcorrenciaQaHttpTests : IAsyncLifetime
{
    private const string Roles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-publicar,"
        + "erros-buscar,erros-adicionar,erros-atualizar,erros-publicar,"
        + "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-publicar,"
        + "versoeslayout-adicionar,versoesprovedor-buscar,versoesprovedor-adicionar,camposversao-adicionar,avisos-adicionar,avisos-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public TokenDeConcorrenciaQaHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ (1) conferência no banco

    /// <summary>
    /// A checagem do service lê o <c>xmin</c> ANTES; só o <c>UPDATE ... WHERE xmin</c> protege a janela
    /// entre a leitura e a gravação. Uma transação crua trava a linha do provedor e a avança (como um
    /// segundo editor que commitou no meio); o PUT só de aliases passa pela checagem do service (a
    /// linha ainda não foi commitada), bloqueia no UPDATE do pai e, ao destravar, tem de dar 409.
    /// Sem o UPDATE forçado, o PUT não tocaria o pai e responderia 200.
    /// </summary>
    [Fact]
    public async Task Provedor_PutSoDeAliases_ComOPaiAvancadoNaJanela_Devolve409ENaoAlteraOsAliases()
    {
        var provedor = await CriarProvedorAsync(["ADN"]);

        await using var contextoTravado = _fixture.CriarContexto();
        await using var transacaoCrua = await contextoTravado.Database.BeginTransactionAsync();
        await contextoTravado.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE codex.provedores SET atualizadoem = now() WHERE provedorid = {provedor.Id}");

        var putTask = Task.Run(() => EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = new[] { "Novo" } },
            provedor.Token));

        await AguardarUpdateBloqueadoAsync("provedores");
        await transacaoCrua.CommitAsync();

        var (status, corpo) = await putTask;

        status.Should().Be(HttpStatusCode.Conflict, corpo.ToString());
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");

        await using var contexto = _fixture.CriarContexto();
        (await contexto.AliasesProvedor.AsNoTracking().Where(a => a.ProvedorId == provedor.Id).Select(a => a.Alias).ToArrayAsync())
            .Should().BeEquivalentTo(["ADN"], because: "o PUT recusado volta atrás inteiro — a remoção do alias antigo não pode ter persistido");
    }

    // ------------------------------------------------------------------ (2) PUT idêntico

    [Fact]
    public async Task Provedor_PutIdenticoAoEstadoAtual_MoveOTokenEAvancaAtualizadoEm()
    {
        var provedor = await CriarProvedorAsync(["ADN"]);
        var atualizadoAntes = await AtualizadoEmDoProvedorAsync(provedor.Id);

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = new[] { "ADN" } },
            provedor.Token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        TokenDe(corpo).Should().NotBe(provedor.Token, because: "o UPDATE forçado sai mesmo sem diferença de conteúdo");
        (await AtualizadoEmDoProvedorAsync(provedor.Id)).Should().BeAfter(atualizadoAntes);

        var (statusVelho, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = new[] { "ADN" } },
            provedor.Token);
        statusVelho.Should().Be(HttpStatusCode.Conflict,
            because: "efeito visível para quem edita: a outra aba aberta antes do 'salvar sem mudar' passa a levar 409");

        var (statusNovo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = new[] { "ADN" } },
            TokenDe(corpo));
        statusNovo.Should().Be(HttpStatusCode.OK, because: "com o token devolvido pelo PUT a edição segue normalmente");
    }

    [Fact]
    public async Task Erro_PutIdenticoAoEstadoAtual_MoveOToken()
    {
        var erro = await CriarErroAsync();

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}",
            new { origem = nameof(OrigemErro.InvoiCy), titulo = erro.Nome, variantes = new[] { "mensagem" } },
            erro.Token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        TokenDe(corpo).Should().NotBe(erro.Token);
    }

    // ------------------------------------------------------------------ (3) o token não anda onde não deve

    [Fact]
    public async Task Provedor_Leitura_NaoMoveOToken_ECriacaoDevolveOTokenCorrente()
    {
        var provedor = await CriarProvedorAsync(["ADN"]);

        var (_, corpoGet1) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        var (_, corpoGet2) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");

        TokenDe(corpoGet1).Should().Be(provedor.Token, because: "o token devolvido pela criação é o corrente");
        TokenDe(corpoGet2).Should().Be(TokenDe(corpoGet1), because: "ler não escreve");
    }

    [Fact]
    public async Task Provedor_PublicarDuasVezes_SegundaNaoMoveOToken_ETrocarSlugEncadeiaComOTokenDevolvido()
    {
        var provedor = await CriarProvedorAsync(["ADN"]);

        var (statusPub, corpoPub) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/publicacao", new { publicado = true }, provedor.Token);
        statusPub.Should().Be(HttpStatusCode.OK, corpoPub.ToString());
        var tokenAposPublicar = TokenDe(corpoPub);

        var (statusPub2, corpoPub2) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/publicacao", new { publicado = true }, tokenAposPublicar);
        statusPub2.Should().Be(HttpStatusCode.OK, corpoPub2.ToString());
        TokenDe(corpoPub2).Should().Be(tokenAposPublicar,
            because: "publicar o que já está publicado não escreve — o conserto do token não pode vazar para este caminho");

        var (statusSlug, corpoSlug) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/slug", new { slug = $"slug-token-{NovoSufixo()}" }, tokenAposPublicar);
        statusSlug.Should().Be(HttpStatusCode.OK, corpoSlug.ToString());

        var (_, corpoGet) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        TokenDe(corpoGet).Should().Be(TokenDe(corpoSlug), because: "o token da resposta da troca de slug é o corrente");
    }

    [Fact]
    public async Task Erro_CriacaoDevolveOTokenCorrente_ELeituraNaoMove()
    {
        var erro = await CriarErroAsync();

        var (_, corpoGet1) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{erro.Id}");
        var (_, corpoGet2) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{erro.Id}");

        TokenDe(corpoGet1).Should().Be(erro.Token);
        TokenDe(corpoGet2).Should().Be(erro.Token);
    }

    [Fact]
    public async Task Artigo_CriacaoDevolveOTokenCorrente_ELeituraNaoMove()
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo = nameof(TipoArtigo.Conceito),
            titulo = $"Artigo token qa {NovoSufixo()}",
            corpo = "Corpo.",
        });
        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        var id = corpo.GetProperty("data").GetProperty("id").GetInt32();

        var (_, corpoGet1) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/artigos/{id}");
        var (_, corpoGet2) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/artigos/{id}");

        TokenDe(corpoGet1).Should().Be(TokenDe(corpo));
        TokenDe(corpoGet2).Should().Be(TokenDe(corpo));
    }

    // ------------------------------------------------------------------ (4) defeito à parte: aviso

    /// <summary>
    /// DEFEITO-AVISO-01 (fora do R6): <c>campoVersaoIds</c> do aviso de versão é aceito, devolvido na
    /// resposta do POST e descartado — <c>Aviso.CamposAfetadosIds</c> é <c>Ignore</c> no EF e nada grava
    /// <c>avisoscampos</c>. O GET seguinte volta <c>camposAfetadosIds: []</c>. Fica pulado para não
    /// derrubar a suíte; remover o <c>Skip</c> para ver o reprodutor falhar.
    /// </summary>
    [Fact(Skip = "DEFEITO-AVISO-01 (fora do R6): campoVersaoIds do aviso nunca é gravado — reprodutor aberto, ver qa-backend.md §9")]
    public async Task Aviso_CampoVersaoIds_EGravadoELidoDeVolta()
    {
        var versaoProvedorId = await CriarVersaoProvedorAsync();
        var campoVersaoId = await SemearCampoVersaoAsync(versaoProvedorId);

        var (statusCriar, corpoCriar) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{versaoProvedorId}/avisos", new
        {
            severidade = nameof(Severidade.Informativo),
            titulo = $"Aviso campos {NovoSufixo()}",
            corpo = "Corpo do aviso.",
            campoVersaoIds = new[] { campoVersaoId },
        });
        statusCriar.Should().Be(HttpStatusCode.Created, corpoCriar.ToString());
        corpoCriar.GetProperty("data").GetProperty("camposAfetadosIds").EnumerateArray().Select(c => c.GetInt32())
            .Should().BeEquivalentTo([campoVersaoId], because: "a resposta do POST ecoa o que foi enviado");

        var (_, corpoVersao) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/versoes/{versaoProvedorId}");
        var aviso = corpoVersao.GetProperty("data").GetProperty("avisos").EnumerateArray().Single();

        aviso.GetProperty("camposAfetadosIds").EnumerateArray().Select(c => c.GetInt32())
            .Should().BeEquivalentTo([campoVersaoId], because: "o que o POST aceitou tem de estar no banco");
    }

    // ------------------------------------------------------------------ apoio

    private sealed record Criado(int Id, string Nome, uint Token);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static uint TokenDe(JsonElement corpo) => corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

    private async Task<Criado> CriarProvedorAsync(string[] aliases)
    {
        var sufixo = NovoSufixo();
        var nome = $"Provedor token qa {sufixo}";

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome,
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return new Criado(corpo.GetProperty("data").GetProperty("id").GetInt32(), nome, TokenDe(corpo));
    }

    private async Task<Criado> CriarErroAsync()
    {
        var sufixo = NovoSufixo();
        var titulo = $"Erro token qa {sufixo}";

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo,
            variantes = new[] { "mensagem" },
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return new Criado(corpo.GetProperty("data").GetProperty("id").GetInt32(), titulo, TokenDe(corpo));
    }

    private async Task<int> CriarVersaoProvedorAsync()
    {
        var provedor = await CriarProvedorAsync([]);

        var (statusLayout, corpoLayout) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout token qa {NovoSufixo()}",
            descricao = (string?)null,
        });
        statusLayout.Should().Be(HttpStatusCode.Created, corpoLayout.ToString());

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedor.Id}/versoes", new
        {
            versaoLayoutId = corpoLayout.GetProperty("data").GetProperty("id").GetInt32(),
            rotulo = $"Versao token qa {NovoSufixo()}",
        });
        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<int> SemearCampoVersaoAsync(int versaoProvedorId)
    {
        await using var contexto = _fixture.CriarContexto();

        var versaoLayoutId = await contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.Id == versaoProvedorId).Select(v => v.VersaoLayoutId).SingleAsync();
        var sufixo = NovoSufixo();

        var campoLayout = Codex.Domain.Entities.CampoLayout.Criar(versaoLayoutId, $"Campo {sufixo}", $"//Campo{sufixo}", "significado", ordem: 1);
        contexto.CamposLayout.Add(campoLayout);
        await contexto.SaveChangesAsync();

        var campoVersao = Codex.Domain.Entities.CampoVersao.Criar(versaoProvedorId, campoLayout.Id, "texto", EstadoPreenchimento.Obrigatorio);
        contexto.CamposVersao.Add(campoVersao);
        await contexto.SaveChangesAsync();

        return campoVersao.Id;
    }

    private async Task<DateTimeOffset> AtualizadoEmDoProvedorAsync(int id)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Provedores.AsNoTracking().Where(p => p.Id == id).Select(p => p.AtualizadoEm).SingleAsync();
    }

    /// <summary>Espera, via <c>pg_stat_activity</c>, um <c>UPDATE</c> da tabela parado em lock — nunca atraso fixo.</summary>
    private async Task AguardarUpdateBloqueadoAsync(string tabela)
    {
        var prazo = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < prazo)
        {
            await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
            await conexao.OpenAsync();

            await using var comando = conexao.CreateCommand();
            comando.CommandText =
                "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query ILIKE @padrao)";
            comando.Parameters.AddWithValue("padrao", $"UPDATE%{tabela}%");

            if ((bool)(await comando.ExecuteScalarAsync())!)
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Nenhum UPDATE de '{tabela}' parou em lock dentro do prazo — o PUT só de filhos não está tocando a linha do pai.");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
