using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Contrato de I38-I42 exercitado por <b>HTTP real</b> contra Postgres real (003/§5). Objeto C#
/// correto não é contrato HTTP correto: nome/caixa de propriedade, status de erro e código do
/// envelope só aparecem no wire — as asserções leem o JSON, nunca o DTO.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ExemplosInternoHttpTests : IAsyncLifetime
{
    private const string RolesDeExemplo =
        "exemplos-buscar,exemplos-adicionar,exemplos-atualizar,exemplos-remover";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ExemplosInternoHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ I39 (criar)

    [Fact]
    public async Task I39_Criar_Devolve201ComSlugDerivadoDoNomeEETag()
    {
        var versaoId = await SemearVersaoAsync();

        var (status, corpo, resposta) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", new
            {
                nome = "NF-e de Serviço Padrão",
                descricao = "Cenário feliz.",
                xml = "<nfe>ok</nfe>",
            });

        status.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull();
        resposta.Headers.ETag.Should().NotBeNull(because: "sem ETag o cliente não tem de onde tirar o If-Match de I41/I42");

        var dados = corpo.GetProperty("data");
        dados.GetProperty("slug").GetString().Should().Be("nf-e-de-servico-padrao",
            because: "o slug é derivado do nome na criação e sustenta a âncora #exemplo-{slug}");
        dados.GetProperty("versaoProvedorId").GetInt32().Should().Be(versaoId);
        dados.GetProperty("xml").GetString().Should().Be("<nfe>ok</nfe>");
        dados.GetProperty("versaoConcorrencia").GetUInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task I39_Criar_EmVersaoInexistente_Devolve404()
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, "/api/v1/interno/versoes/999999/exemplos", CorpoValido("Órfão"));

        status.Should().Be(HttpStatusCode.NotFound);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("NAO_ENCONTRADO");
    }

    [Fact]
    public async Task I39_Criar_ComNomeJaUsadoNaVersao_Devolve409()
    {
        var versaoId = await SemearVersaoAsync();
        var nome = $"Cenário repetido {Guid.NewGuid():N}";

        var (primeiro, _, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido(nome));
        primeiro.Should().Be(HttpStatusCode.Created);

        var (segundo, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido(nome));

        segundo.Should().Be(HttpStatusCode.Conflict);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("REGISTRO_DUPLICADO");
    }

    /// <summary>
    /// modelo-dados.md §1.3 — dois nomes DIFERENTES podem normalizar para o mesmo slug. Sem o
    /// sufixo determinístico, o segundo cadastro morreria como 409 de duplicata para um nome que
    /// não está duplicado.
    /// </summary>
    [Fact]
    public async Task I39_Criar_ComNomeQueColideNoSlug_RecebeSufixoDeterministico()
    {
        var versaoId = await SemearVersaoAsync();

        var primeiro = await CriarAsync(versaoId, "NF-e válida");
        var segundo = await CriarAsync(versaoId, "NF-e valida");

        primeiro.GetProperty("slug").GetString().Should().Be("nf-e-valida");
        segundo.GetProperty("slug").GetString().Should().Be("nf-e-valida-2");
    }

    // ------------------------------------------------------------------ C3 — cap de 10 por versão

    [Fact]
    public async Task I39_CapDeDezExemplos_DecimoPassa_DecimoPrimeiroE422()
    {
        var versaoId = await SemearVersaoAsync();

        for (var indice = 1; indice <= 10; indice++)
        {
            var (status, _, _) = await EnviarAsync(
                HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido($"Cenário {indice}"));

            status.Should().Be(HttpStatusCode.Created, because: $"o {indice}º exemplo ainda cabe no cap de 10 (C3)");
        }

        var (statusExcedente, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido("Cenário 11"));

        statusExcedente.Should().Be(HttpStatusCode.UnprocessableEntity);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("LIMITE_EXCEDIDO");
    }

    /// <summary>O soft delete devolve a vaga — o cap conta exemplos ativos, não linhas da tabela.</summary>
    [Fact]
    public async Task I42_RemoverLiberaVagaNoCapDeDez()
    {
        var versaoId = await SemearVersaoAsync();
        var primeiro = await CriarAsync(versaoId, "Cenário 1");

        for (var indice = 2; indice <= 10; indice++)
            (await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido($"Cenário {indice}")))
                .Status.Should().Be(HttpStatusCode.Created);

        var (bloqueado, _, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido("Excedente"));
        bloqueado.Should().Be(HttpStatusCode.UnprocessableEntity);

        var (removido, _, _) = await EnviarAsync(
            HttpMethod.Delete, $"/api/v1/interno/exemplos/{primeiro.GetProperty("id").GetInt32()}",
            ifMatch: primeiro.GetProperty("versaoConcorrencia").GetUInt32());
        removido.Should().Be(HttpStatusCode.NoContent);

        var (liberado, _, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido("Excedente"));
        liberado.Should().Be(HttpStatusCode.Created);
    }

    // ------------------------------------------------------------------ C3 — teto de 64 KiB

    [Theory]
    [InlineData(64 * 1024, HttpStatusCode.Created)]
    [InlineData((64 * 1024) + 1, HttpStatusCode.UnprocessableEntity)]
    public async Task I39_TetoDoXml_MedeCaracteresNosDoisLadosDaFronteira(int caracteres, HttpStatusCode esperado)
    {
        var versaoId = await SemearVersaoAsync();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", new
            {
                nome = $"Cenário {caracteres}",
                descricao = (string?)null,
                xml = new string('x', caracteres),
            });

        status.Should().Be(esperado,
            because: "o teto de negócio é 64 KiB em CARACTERES (modelo-dados.md §1.5) — a fronteira exata passa, um a mais reprova");
    }

    /// <summary>
    /// A separação de unidades de modelo-dados.md §1.5 medida de verdade: 64 KiB de caracteres
    /// ACENTUADOS passam de 64 KiB em BYTES UTF-8 e ainda assim têm de ser aceitos. Se alguém
    /// trocar <c>xml.Length</c> por <c>Encoding.UTF8.GetByteCount</c>, este caso vira 422.
    /// </summary>
    [Fact]
    public async Task I39_XmlAcentuadoNoTetoDeCaracteres_EAceito_MesmoPassandoDe64KiBEmBytes()
    {
        var versaoId = await SemearVersaoAsync();
        var xml = new string('ç', 64 * 1024);

        System.Text.Encoding.UTF8.GetByteCount(xml).Should().BeGreaterThan(64 * 1024,
            because: "é exatamente o caso que a divergência deliberada de unidades protege");

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", new
            {
                nome = $"Cenário acentuado {Guid.NewGuid():N}",
                descricao = (string?)null,
                xml,
            });

        status.Should().Be(HttpStatusCode.Created);
    }

    // ------------------------------------------------------------------ I38/I40/I41/I42

    /// <summary>
    /// Três exemplos, com a ordem esperada divergindo tanto da alfabética quanto do inverso da
    /// inserção — dois elementos passariam por acidente em metade das mutações de ordenação.
    /// </summary>
    [Fact]
    public async Task I38_Listar_DevolveNaOrdemDeCriacaoComDesempatePorId()
    {
        var versaoId = await SemearVersaoAsync();

        await CriarAsync(versaoId, "Zebra");
        await CriarAsync(versaoId, "Alfa");
        await CriarAsync(versaoId, "Meio");

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/versoes/{versaoId}/exemplos");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("nome").GetString())
            .Should().ContainInOrder("Zebra", "Alfa", "Meio");
    }

    [Fact]
    public async Task I38_Listar_DeVersaoInexistente_Devolve404()
    {
        var (status, _, _) = await EnviarAsync(HttpMethod.Get, "/api/v1/interno/versoes/999999/exemplos");

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task I40_ObterPorId_Inexistente_Devolve404()
    {
        var (status, _, _) = await EnviarAsync(HttpMethod.Get, "/api/v1/interno/exemplos/999999");

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task I41_Atualizar_MudaConteudoEPreservaOSlugOriginal()
    {
        var versaoId = await SemearVersaoAsync();
        var criado = await CriarAsync(versaoId, "Nome Original");
        var id = criado.GetProperty("id").GetInt32();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/exemplos/{id}", new
        {
            nome = "Nome Totalmente Diferente",
            descricao = "nova descrição",
            xml = "<nfe>v2</nfe>",
        }, ifMatch: criado.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.OK);
        var dados = corpo.GetProperty("data");
        dados.GetProperty("nome").GetString().Should().Be("Nome Totalmente Diferente");
        dados.GetProperty("xml").GetString().Should().Be("<nfe>v2</nfe>");
        dados.GetProperty("slug").GetString().Should().Be("nome-original",
            because: "renomear NUNCA recalcula slug — a âncora publicada não pode se mover (modelo-dados.md §1.3)");
    }

    [Fact]
    public async Task I41_Atualizar_SemIfMatch_Devolve400()
    {
        var versaoId = await SemearVersaoAsync();
        var criado = await CriarAsync(versaoId, "Sem If-Match");

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/exemplos/{criado.GetProperty("id").GetInt32()}", CorpoValido("Editado"));

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("IF_MATCH_OBRIGATORIO");
    }

    [Fact]
    public async Task I41_Atualizar_ComIfMatchDesatualizado_Devolve409()
    {
        var versaoId = await SemearVersaoAsync();
        var criado = await CriarAsync(versaoId, "Concorrência");
        var id = criado.GetProperty("id").GetInt32();
        var versaoInicial = criado.GetProperty("versaoConcorrencia").GetUInt32();

        (await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/exemplos/{id}", CorpoValido("Primeira edição"), versaoInicial))
            .Status.Should().Be(HttpStatusCode.OK);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/exemplos/{id}", CorpoValido("Segunda edição"), versaoInicial);

        status.Should().Be(HttpStatusCode.Conflict);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
    }

    [Fact]
    public async Task I42_Remover_SomeDaListagemEDoDetalhe()
    {
        var versaoId = await SemearVersaoAsync();
        var criado = await CriarAsync(versaoId, "Para remover");
        var id = criado.GetProperty("id").GetInt32();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Delete, $"/api/v1/interno/exemplos/{id}",
            ifMatch: criado.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/exemplos/{id}")).Status.Should().Be(HttpStatusCode.NotFound);

        var (_, lista, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/versoes/{versaoId}/exemplos");
        lista.GetProperty("data").EnumerateArray().Should().BeEmpty();
    }

    // ------------------------------------------------------------------ helpers

    private static object CorpoValido(string nome) => new
    {
        nome,
        descricao = (string?)null,
        xml = "<nfe>conteudo</nfe>",
    };

    private async Task<JsonElement> CriarAsync(int versaoId, string nome)
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", CorpoValido(nome));

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").Clone();
    }

    private async Task<int> SemearVersaoAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdemAtual = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout i38 {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor i38 {sufixo}", $"I38-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão i38 {sufixo}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return versao.Id;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeExemplo);

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
