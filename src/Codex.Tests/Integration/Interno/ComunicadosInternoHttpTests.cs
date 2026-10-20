using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Contrato de I30-I35 exercitado por <b>HTTP real</b> contra Postgres real. Objeto C# correto não
/// é contrato HTTP correto: nome e caixa de propriedade, enum virando número, data vazando formato
/// e status de erro só aparecem no wire — por isso os enums vão como <b>string literal no JSON</b>,
/// exatamente como o cliente TypeScript manda, e as asserções leem o JSON, não o DTO.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ComunicadosInternoHttpTests : IAsyncLifetime
{
    private const string RolesDeComunicado =
        "comunicados-buscar,comunicados-adicionar,comunicados-atualizar,comunicados-remover,comunicados-publicar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ComunicadosInternoHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ I32 (criar)

    [Fact]
    public async Task I32_Criar_Devolve201ComEnumsEmStringEOsCamposDoDetalhe()
    {
        var expiraEm = DateTimeOffset.UtcNow.AddDays(7);

        var (status, corpo, resposta) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/comunicados", new
        {
            titulo = $"Instabilidade {Guid.NewGuid():N}",
            corpo = "Emissão intermitente desde as 9h.",
            severidade = "Critico",
            abrangencia = "Global",
            expiraEm,
        });

        status.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull(because: "POST devolve Location do recurso criado");
        resposta.Headers.ETag.Should().NotBeNull(because: "sem ETag o cliente não tem de onde tirar o If-Match das escritas seguintes");

        var dados = corpo.GetProperty("data");
        dados.GetProperty("severidade").GetString().Should().Be("Critico",
            because: "enum sai como nome, nunca como ordinal — o frontend tipa isso como união de literais");
        dados.GetProperty("abrangencia").GetString().Should().Be("Global");
        dados.GetProperty("publicado").GetBoolean().Should().BeFalse(because: "publicação só muda por I35");
        dados.GetProperty("encerrado").GetBoolean().Should().BeFalse();
        dados.GetProperty("corpo").GetString().Should().Be("Emissão intermitente desde as 9h.");
        dados.GetProperty("provedorId").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("versaoProvedorId").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("versaoConcorrencia").GetUInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task I32_Criar_ComAbrangenciaDeProvedor_DevolveIdERotuloDoProvedor()
    {
        var cenario = await MontarEscopoAsync();

        var criado = await CriarAsync(new
        {
            titulo = $"Janela do provedor {Guid.NewGuid():N}",
            corpo = "Manutenção programada.",
            severidade = "Alerta",
            abrangencia = "Provedor",
            provedorId = cenario.ProvedorId,
            expiraEm = DateTimeOffset.UtcNow.AddDays(3),
        });

        criado.GetProperty("provedorId").GetInt32().Should().Be(cenario.ProvedorId);
        criado.GetProperty("provedorNome").GetString().Should().Be(cenario.ProvedorNome,
            because: "o rótulo de exibição é resolvido por join — sem ele a tela mostra um id cru");
        criado.GetProperty("versaoRotulo").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task I32_Criar_ComAbrangenciaDeVersao_DevolveOsDoisRotulos()
    {
        var cenario = await MontarEscopoAsync();

        var criado = await CriarAsync(new
        {
            titulo = $"Janela da versão {Guid.NewGuid():N}",
            corpo = "Ajuste de layout.",
            severidade = "Informativo",
            abrangencia = "ProvedorVersao",
            provedorId = cenario.ProvedorId,
            versaoProvedorId = cenario.VersaoProvedorId,
            expiraEm = DateTimeOffset.UtcNow.AddDays(3),
        });

        criado.GetProperty("abrangencia").GetString().Should().Be("ProvedorVersao");
        criado.GetProperty("versaoProvedorId").GetInt32().Should().Be(cenario.VersaoProvedorId);
        criado.GetProperty("versaoRotulo").GetString().Should().Be(cenario.VersaoRotulo);
    }

    [Fact]
    public async Task I32_Criar_ComEscopoInconsistente_Devolve422()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/comunicados", new
        {
            titulo = "Global com provedor",
            corpo = "Inconsistente.",
            severidade = "Alerta",
            abrangencia = "Global",
            provedorId = 1,
            expiraEm = DateTimeOffset.UtcNow.AddDays(1),
        });

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "a inconsistência tem de virar 422 com nome de campo, não 500 de CHECK estourado");
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task I32_Criar_ComExpiracaoNoPassado_Devolve422()
    {
        var (status, _, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/comunicados", new
        {
            titulo = "Já nasceu vencido",
            corpo = "Expiração no passado.",
            severidade = "Alerta",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(-1),
        });

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData(2000, HttpStatusCode.Created)]
    [InlineData(2001, HttpStatusCode.UnprocessableEntity)]
    public async Task I32_Criar_RespeitaAFronteiraDeDoisMilCaracteresNoCorpo(int tamanho, HttpStatusCode esperado)
    {
        var (status, _, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/comunicados", new
        {
            titulo = $"Fronteira {tamanho} {Guid.NewGuid():N}",
            corpo = new string('a', tamanho),
            severidade = "Alerta",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(1),
        });

        status.Should().Be(esperado,
            because: "decisoes.md B-6 fixou 2000 nos dois lados — limite que só existe de um lado vira truncamento silencioso ou 500 do outro");
    }

    // ------------------------------------------------------------------ I30/I31 (ler)

    [Fact]
    public async Task I30_Listar_OrdenaDaExpiracaoMaisDistanteParaAMaisProxima()
    {
        var cenario = await MontarEscopoAsync();
        var baseExpiracao = DateTimeOffset.UtcNow;

        // Inserção em ordem propositalmente diferente da esperada E do seu inverso: com só dois
        // elementos, metade das implementações erradas passaria por acaso.
        await CriarNoEscopoAsync(cenario.ProvedorId, "media", baseExpiracao.AddDays(5));
        await CriarNoEscopoAsync(cenario.ProvedorId, "longa", baseExpiracao.AddDays(30));
        await CriarNoEscopoAsync(cenario.ProvedorId, "curta", baseExpiracao.AddDays(1));

        var itens = await ListarAsync($"?provedorId={cenario.ProvedorId}");

        itens.Select(item => item.GetProperty("titulo").GetString()!.Split(' ')[1])
            .Should().ContainInOrder("longa", "media", "curta");
    }

    [Fact]
    public async Task I30_ListaNaoTrazCorpo_MasODetalheTraz()
    {
        var cenario = await MontarEscopoAsync();
        var criado = await CriarNoEscopoAsync(cenario.ProvedorId, "detalhe", DateTimeOffset.UtcNow.AddDays(2));

        var itemDaLista = (await ListarAsync($"?provedorId={cenario.ProvedorId}")).Single();
        itemDaLista.TryGetProperty("corpo", out _).Should().BeFalse(
            because: "§0.2 — listagem nunca carrega texto livre em toda linha da página");

        var detalhe = await ObterDetalheAsync(criado.GetProperty("id").GetInt32());
        detalhe.GetProperty("corpo").GetString().Should().NotBeNullOrWhiteSpace(
            because: "§0.3 — sem corpo no detalhe o modal de edição abre com o campo vazio");
    }

    [Fact]
    public async Task I31_ObterDetalhe_DeIdInexistente_Devolve404()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, "/api/v1/interno/comunicados/999999");

        status.Should().Be(HttpStatusCode.NotFound);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("NAO_ENCONTRADO");
    }

    // ------------------------------------------------------------------ I33 (atualizar)

    [Fact]
    public async Task I33_Atualizar_TrocaEscopoEConteudoEDevolveORecursoAtualizado()
    {
        var cenario = await MontarEscopoAsync();
        var criado = await CriarNoEscopoAsync(cenario.ProvedorId, "escopo", DateTimeOffset.UtcNow.AddDays(2));
        var id = criado.GetProperty("id").GetInt32();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/comunicados/{id}", new
        {
            titulo = "Agora é global",
            corpo = "Conteúdo revisado.",
            severidade = "Informativo",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(9),
        }, ifMatch: criado.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.OK);

        var dados = corpo.GetProperty("data");
        dados.GetProperty("abrangencia").GetString().Should().Be("Global");
        dados.GetProperty("provedorId").ValueKind.Should().Be(JsonValueKind.Null,
            because: "trocar de escopo sem zerar o id deixaria a linha inválida para o CHECK de consistência");
        dados.GetProperty("provedorNome").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("titulo").GetString().Should().Be("Agora é global");
        dados.GetProperty("severidade").GetString().Should().Be("Informativo");
    }

    [Fact]
    public async Task I33_Atualizar_SemIfMatch_Devolve400()
    {
        var criado = await CriarAsync(ComunicadoGlobalPadrao());

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/comunicados/{criado.GetProperty("id").GetInt32()}", ComunicadoGlobalPadrao());

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("IF_MATCH_OBRIGATORIO");
    }

    [Fact]
    public async Task I33_Atualizar_ComVersaoDefasada_Devolve409()
    {
        var criado = await CriarAsync(ComunicadoGlobalPadrao());
        var id = criado.GetProperty("id").GetInt32();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/comunicados/{id}", ComunicadoGlobalPadrao(),
            ifMatch: criado.GetProperty("versaoConcorrencia").GetUInt32() + 1);

        status.Should().Be(HttpStatusCode.Conflict);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
        corpo.GetRawText().Should().NotContain("uq_codex_").And.NotContain("at Codex.");
    }

    // ------------------------------------------------------------------ I34 (remover)

    [Fact]
    public async Task I34_Remover_Devolve204EOComunicadoSomeDoDetalhe()
    {
        var criado = await CriarAsync(ComunicadoGlobalPadrao());
        var id = criado.GetProperty("id").GetInt32();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Delete, $"/api/v1/interno/comunicados/{id}", ifMatch: criado.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.NoContent);

        var (statusDepois, _, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/comunicados/{id}");
        statusDepois.Should().Be(HttpStatusCode.NotFound, because: "soft delete tira o comunicado do acervo interno");
    }

    // ------------------------------------------------------------------ I35 (publicar)

    [Fact]
    public async Task I35_Publicar_EDepoisDespublicar_RefleteNoRecurso()
    {
        var criado = await CriarAsync(ComunicadoGlobalPadrao());
        var id = criado.GetProperty("id").GetInt32();

        var publicado = await PublicarAsync(id, criado.GetProperty("versaoConcorrencia").GetUInt32(), publicado: true);
        publicado.GetProperty("publicado").GetBoolean().Should().BeTrue();

        var despublicado = await PublicarAsync(id, publicado.GetProperty("versaoConcorrencia").GetUInt32(), publicado: false);
        despublicado.GetProperty("publicado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task I35_PublicarComunicadoJaExpirado_Devolve422ComCodigoProprio()
    {
        var (id, versao) = await CriarEVencerAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/comunicados/{id}/publicacao", new { publicado = true }, ifMatch: versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "decisoes.md B-4 — publicar algo que o job desfaria no tique seguinte seria desobedecer em silêncio");
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("COMUNICADO_EXPIRADO",
            because: "o código genérico VALIDACAO_NEGOCIO não distinguiria este caso de qualquer outra regra");
    }

    [Fact]
    public async Task I35_PublicarComunicadoJaExpirado_DizOProximoPasso_NaoSoOProblema()
    {
        var (id, versao) = await CriarEVencerAsync();

        var (_, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/comunicados/{id}/publicacao", new { publicado = true }, ifMatch: versao);

        var mensagem = corpo.GetProperty("error").GetProperty("message").GetString();

        mensagem.Should().NotBeNullOrWhiteSpace();
        mensagem.Should().ContainAll(["expiração", "publique"],
            because: "exigência de decisoes.md B-4: um 422 que diz o que está errado mas não o que fazer é beco sem saída — "
                     + "o editor conclui que a tela quebrou");
    }

    [Fact]
    public async Task I35_DespublicarComunicadoJaExpirado_ContinuaPermitido()
    {
        var criado = await CriarAsync(ComunicadoGlobalPadrao());
        var id = criado.GetProperty("id").GetInt32();
        await PublicarAsync(id, criado.GetProperty("versaoConcorrencia").GetUInt32(), publicado: true);

        await VencerAsync(id);
        var atual = await ObterDetalheAsync(id);

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/comunicados/{id}/publicacao", new { publicado = false },
            ifMatch: atual.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.OK,
            because: "despublicar é o mesmo estado a que o job converge — recusar travaria o editor num comunicado vencido no ar");
    }

    [Fact]
    public async Task I35_Publicar_NuncaCriaLinhaEmItensBusca()
    {
        var linhasAntes = await ContarLinhasDeBuscaAsync();

        var criado = await CriarAsync(ComunicadoGlobalPadrao());
        var id = criado.GetProperty("id").GetInt32();
        await PublicarAsync(id, criado.GetProperty("versaoConcorrencia").GetUInt32(), publicado: true);

        (await ContarLinhasDeBuscaAsync()).Should().Be(linhasAntes,
            because: "PQ-05/M-1 — comunicado não é indexado em nenhuma transição de estado");
    }

    // ------------------------------------------------------------------ apoio

    private sealed record EscopoDeTeste(int ProvedorId, string ProvedorNome, int VersaoProvedorId, string VersaoRotulo);

    private async Task<EscopoDeTeste> MontarEscopoAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout comunicado {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor comunicado {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao comunicado {sufixo}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return new EscopoDeTeste(provedor.Id, provedor.Nome, versao.Id, versao.Rotulo);
    }

    private static object ComunicadoGlobalPadrao() => new
    {
        titulo = "Comunicado global",
        corpo = "Corpo do comunicado.",
        severidade = "Alerta",
        abrangencia = "Global",
        expiraEm = DateTimeOffset.UtcNow.AddDays(5),
    };

    private Task<JsonElement> CriarNoEscopoAsync(int provedorId, string marcador, DateTimeOffset expiraEm) => CriarAsync(new
    {
        titulo = $"Janela {marcador} {Guid.NewGuid():N}",
        corpo = "Corpo do comunicado de escopo.",
        severidade = "Alerta",
        abrangencia = "Provedor",
        provedorId,
        expiraEm,
    });

    private async Task<JsonElement> CriarAsync(object requisicao)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/comunicados", requisicao);

        status.Should().Be(HttpStatusCode.Created, because: "I32 devolve 201");
        return corpo.GetProperty("data");
    }

    private async Task<JsonElement> ObterDetalheAsync(int id)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/comunicados/{id}");

        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("data");
    }

    private async Task<JsonElement> PublicarAsync(int id, uint versao, bool publicado)
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/comunicados/{id}/publicacao", new { publicado }, ifMatch: versao);

        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("data");
    }

    private async Task<IReadOnlyList<JsonElement>> ListarAsync(string consulta)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/comunicados{consulta}");

        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>
    /// Cria um comunicado válido e empurra a expiração para o passado direto no banco — é o que o
    /// tempo faz sozinho em produção, e o único caminho possível: I32/I33 recusam data passada.
    /// </summary>
    private async Task<(int Id, uint Versao)> CriarEVencerAsync()
    {
        var criado = await CriarAsync(ComunicadoGlobalPadrao());
        var id = criado.GetProperty("id").GetInt32();

        await VencerAsync(id);

        // O UPDATE direto muda o xmin — reler é obrigatório, senão o If-Match falharia por 409
        // antes de chegar na regra que este teste quer provar.
        var atual = await ObterDetalheAsync(id);
        atual.GetProperty("encerrado").GetBoolean().Should().BeTrue(because: "o cenário só faz sentido com o comunicado já vencido");

        return (id, atual.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task VencerAsync(int id)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "UPDATE codex.comunicados SET expiraem = now() - interval '1 hour' WHERE comunicadoid = @id";
        comando.Parameters.AddWithValue("id", id);

        (await comando.ExecuteNonQueryAsync()).Should().Be(1);
    }

    private async Task<long> ContarLinhasDeBuscaAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM codex.itensbusca";

        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeComunicado);

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
