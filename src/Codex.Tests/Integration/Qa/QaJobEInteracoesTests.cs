using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.API.Jobs;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA (002) — o que nenhum teste entregue exercita junto:
/// o <see cref="ExpiracaoComunicadosJob"/> RODANDO (não só registrado), o caminho de recuperação
/// completo prometido por B-4, e as interações entre regras de artigo e erro.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaJobEInteracoesTests : IAsyncLifetime
{
    private const string Roles =
        "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-remover,artigos-publicar,"
        + "comunicados-buscar,comunicados-adicionar,comunicados-atualizar,comunicados-remover,comunicados-publicar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public QaJobEInteracoesTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        _cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, Roles);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ================================================================ o job rodando

    /// <summary>
    /// Expect: o <see cref="ExpiracaoComunicadosJob"/> ligado converge um comunicado publicado e
    /// vencido para <c>publicado = false</c> SEM intervenção, e não toca o vigente.
    /// Os testes entregues cobrem o registro e o service; nenhum roda o BackgroundService.
    /// </summary>
    [Fact]
    public async Task Job_Ligado_ConvergeOVencidoENaoTocaOVigente()
    {
        var (vencidoId, vigenteId) = await SemearVencidoEVigenteAsync();

        var job = CriarJob(TimeSpan.FromMilliseconds(50));
        await job.StartAsync(CancellationToken.None);
        try
        {
            (await EsperarPublicadoAsync(vencidoId, esperado: false)).Should().BeTrue(
                because: "o job precisa despublicar o vencido sozinho — é a semântica de U-5, não só o filtro de leitura");
        }
        finally
        {
            await job.StopAsync(CancellationToken.None);
        }

        (await PublicadoAsync(vigenteId)).Should().BeTrue(
            because: "convergir o vencido não pode derrubar o que ainda está no ar");
    }

    /// <summary>
    /// Expect: rodar o job de novo depois de convergido NÃO muda mais nada — idempotência
    /// observada pelo estado, não pelo contador.
    /// </summary>
    [Fact]
    public async Task Job_SegundaPassada_NaoAlteraMaisNadaEConvergeZero()
    {
        var (vencidoId, _) = await SemearVencidoEVigenteAsync();

        using var escopo = _factory.Services.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<IExpiracaoComunicadosService>();
        var agora = DateTimeOffset.UtcNow;

        var primeira = await servico.ExecutarAsync(agora, CancellationToken.None);
        primeira.Should().BeGreaterThan(0);
        (await PublicadoAsync(vencidoId)).Should().BeFalse();

        var segunda = await servico.ExecutarAsync(agora, CancellationToken.None);

        segunda.Should().Be(0, because: "a condição é sobre o estado atual da linha, não sobre 'já processei este id'");
        (await PublicadoAsync(vencidoId)).Should().BeFalse();
    }

    /// <summary>
    /// Expect: convergido pelo job, o comunicado continua DISTINGUÍVEL de um removido — a tela 12
    /// precisa mostrar "encerrado", não sumir com a linha.
    /// </summary>
    [Fact]
    public async Task Job_ComunicadoConvergido_ContinuaVisivelComoEncerrado_NuncaDesativado()
    {
        var (vencidoId, _) = await SemearVencidoEVigenteAsync();

        using (var escopo = _factory.Services.CreateScope())
        {
            var servico = escopo.ServiceProvider.GetRequiredService<IExpiracaoComunicadosService>();
            await servico.ExecutarAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        }

        using var resposta = await _cliente.GetAsync($"/api/v1/interno/comunicados/{vencidoId}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: "despublicar não é apagar");

        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        dados.GetProperty("publicado").GetBoolean().Should().BeFalse();
        dados.GetProperty("encerrado").GetBoolean().Should().BeTrue();
    }

    // ================================================================ B-4 ponta a ponta

    /// <summary>
    /// Expect: publicar comunicado vencido → 422 <c>COMUNICADO_EXPIRADO</c> e a mensagem diz o
    /// PRÓXIMO PASSO (estender a expiração), não só o problema — exigência literal de B-4.
    /// </summary>
    [Fact]
    public async Task B4_PublicarVencido_Retorna422ComCodigoEProximoPasso()
    {
        var comunicado = await SemearVencidoRascunhoAsync();

        var (status, corpo) = await EnviarAsync(HttpMethod.Put,
            $"/api/v1/interno/comunicados/{comunicado.Id}/publicacao", new { publicado = true }, comunicado.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);

        var erro = corpo.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("COMUNICADO_EXPIRADO");

        var mensagem = erro.GetProperty("message").GetString()!;
        mensagem.ToLowerInvariant().Should().Contain("estend",
            because: "B-4 exige que a mensagem indique o próximo passo — 422 sem saída faz o editor achar que a tela quebrou");
        mensagem.ToLowerInvariant().Should().Contain("expira");
    }

    /// <summary>
    /// Expect: o caminho de recuperação que a mensagem promete FUNCIONA — estender por I33 e
    /// então publicar por I35 devolve 200 e o comunicado fica no ar.
    /// </summary>
    [Fact]
    public async Task B4_CaminhoDeRecuperacaoProMetidoPelaMensagem_Funciona()
    {
        var comunicado = await SemearVencidoRascunhoAsync();

        var (statusEdicao, corpoEdicao) = await EnviarAsync(HttpMethod.Put,
            $"/api/v1/interno/comunicados/{comunicado.Id}",
            new
            {
                titulo = "Reaberto pela sonda de QA",
                corpo = "Estendido para o futuro.",
                severidade = "Informativo",
                abrangencia = "Global",
                expiraEm = DateTimeOffset.UtcNow.AddDays(5),
            },
            comunicado.Versao);

        statusEdicao.Should().Be(HttpStatusCode.OK, because: "I33 é o passo que a mensagem do 422 manda dar");
        var novaVersao = corpoEdicao.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var (statusPublicacao, corpoPublicacao) = await EnviarAsync(HttpMethod.Put,
            $"/api/v1/interno/comunicados/{comunicado.Id}/publicacao", new { publicado = true }, novaVersao);

        statusPublicacao.Should().Be(HttpStatusCode.OK);
        corpoPublicacao.GetProperty("data").GetProperty("publicado").GetBoolean().Should().BeTrue();
        corpoPublicacao.GetProperty("data").GetProperty("encerrado").GetBoolean().Should().BeFalse();
    }

    // ================================================================ interações artigo x erro

    /// <summary>
    /// Expect: despublicar um artigo referenciado por um erro PUBLICADO remove o artigo do bloco
    /// <c>artigosRelacionados</c> de P6 — superfície pública nunca mostra rascunho, nem de lado.
    /// </summary>
    [Fact]
    public async Task Despublicar_ArtigoReferenciadoPorErroPublicado_SomeDoBlocoDeP6()
    {
        var cenario = await SemearArtigoLigadoAErroAsync();

        (await SlugsRelacionadosDoErroAsync(cenario.ErroSlug)).Should().Contain(cenario.ArtigoSlug);

        var atual = await ObterArtigoAdminAsync(cenario.ArtigoId);
        var (status, _) = await EnviarAsync(HttpMethod.Put,
            $"/api/v1/interno/artigos/{cenario.ArtigoId}/publicacao", new { publicado = false },
            atual.GetProperty("versaoConcorrencia").GetUInt32());
        status.Should().Be(HttpStatusCode.OK);

        (await SlugsRelacionadosDoErroAsync(cenario.ErroSlug)).Should().NotContain(cenario.ArtigoSlug,
            because: "B-3 vale nas duas direções da referência cruzada");
    }

    /// <summary>Expect: remover (soft delete) o artigo também o tira do bloco de P6.</summary>
    [Fact]
    public async Task Remover_ArtigoReferenciadoPorErroPublicado_SomeDoBlocoDeP6()
    {
        var cenario = await SemearArtigoLigadoAErroAsync();

        var atual = await ObterArtigoAdminAsync(cenario.ArtigoId);
        var (status, _) = await EnviarAsync(HttpMethod.Delete,
            $"/api/v1/interno/artigos/{cenario.ArtigoId}", corpo: null,
            atual.GetProperty("versaoConcorrencia").GetUInt32());
        status.Should().Be(HttpStatusCode.NoContent);

        (await SlugsRelacionadosDoErroAsync(cenario.ErroSlug)).Should().NotContain(cenario.ArtigoSlug,
            because: "artigo removido não pode continuar linkado numa página pública — seria link para 404");
    }

    /// <summary>
    /// Expect: trocar o tipo de um artigo PUBLICADO mantém o item visível na busca. B-2 promete
    /// reindexação; reindexar recalculando visibilidade errado tiraria o artigo do ar em silêncio.
    /// </summary>
    [Fact]
    public async Task TrocarTipoDeArtigoPublicado_NaoTiraOItemDaVisibilidadeDaBusca()
    {
        var cenario = await SemearArtigoLigadoAErroAsync();

        (await VisivelPublicoNoIndiceAsync(cenario.ArtigoId)).Should().BeTrue();

        var atual = await ObterArtigoAdminAsync(cenario.ArtigoId);
        var (status, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/artigos/{cenario.ArtigoId}",
            new
            {
                tipo = "Mudanca",
                titulo = atual.GetProperty("titulo").GetString(),
                corpo = atual.GetProperty("corpo").GetString(),
            },
            atual.GetProperty("versaoConcorrencia").GetUInt32());
        status.Should().Be(HttpStatusCode.OK);

        (await VisivelPublicoNoIndiceAsync(cenario.ArtigoId)).Should().BeTrue(
            because: "reindexar por troca de tipo não pode derrubar a visibilidade de um artigo publicado");
    }

    // ================================================================ apoio

    private ExpiracaoComunicadosJob CriarJob(TimeSpan intervalo) => new(
        _factory.Services.GetRequiredService<IServiceScopeFactory>(),
        TimeProvider.System,
        _factory.Services.GetRequiredService<ILogger<ExpiracaoComunicadosJob>>(),
        intervalo);

    private async Task<(int VencidoId, int VigenteId)> SemearVencidoEVigenteAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var vencido = Comunicado.CriarGlobal($"Vencido QA {Guid.NewGuid():N}", "corpo", Severidade.Alerta,
            DateTimeOffset.UtcNow.AddHours(-1));
        vencido.Publicar();

        var vigente = Comunicado.CriarGlobal($"Vigente QA {Guid.NewGuid():N}", "corpo", Severidade.Alerta,
            DateTimeOffset.UtcNow.AddDays(30));
        vigente.Publicar();

        contexto.Comunicados.AddRange(vencido, vigente);
        await contexto.SaveChangesAsync();

        return (vencido.Id, vigente.Id);
    }

    private async Task<(int Id, uint Versao)> SemearVencidoRascunhoAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        // Semeado direto: o validator de I32 proíbe criar já vencido, e o alvo do caso é a regra
        // de I35 sobre uma linha que o tempo venceu depois de criada.
        var comunicado = Comunicado.CriarGlobal($"Vencido rascunho QA {Guid.NewGuid():N}", "corpo",
            Severidade.Informativo, DateTimeOffset.UtcNow.AddHours(-2));
        contexto.Comunicados.Add(comunicado);
        await contexto.SaveChangesAsync();

        return (comunicado.Id, comunicado.Versao);
    }

    private sealed record CenarioCruzado(int ArtigoId, string ArtigoSlug, string ErroSlug);

    private async Task<CenarioCruzado> SemearArtigoLigadoAErroAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        int erroId;
        string erroSlug;
        await using (var contexto = _fixture.CriarContexto())
        {
            var erro = Erro.Criar($"QAX-{sufixo}", OrigemErro.InvoiCy, $"Erro cruzado QA {sufixo}");
            erro.SubstituirVariantes([$"variante cruzada {sufixo}"]);
            erro.Publicar();
            contexto.Erros.Add(erro);
            await contexto.SaveChangesAsync();
            erroId = erro.Id;
            erroSlug = erro.Slug;
        }

        // Artigo criado pelo caminho REAL (I25) para nascer indexado, e vinculado ao erro pelo
        // mesmo caminho — semear a junção por DbContext pularia a indexação e o cenário testaria
        // outra coisa.
        var (statusCriacao, corpoCriacao) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo = "Conceito",
            titulo = $"Artigo cruzado QA {sufixo}",
            corpo = $"Corpo cruzado {sufixo}.",
            erroIds = new[] { erroId },
        }, ifMatch: null);
        statusCriacao.Should().Be(HttpStatusCode.Created);

        var dados = corpoCriacao.GetProperty("data");
        var artigoId = dados.GetProperty("id").GetInt32();
        var artigoSlug = dados.GetProperty("slug").GetString()!;

        var (statusPublicacao, _) = await EnviarAsync(HttpMethod.Put,
            $"/api/v1/interno/artigos/{artigoId}/publicacao", new { publicado = true },
            dados.GetProperty("versaoConcorrencia").GetUInt32());
        statusPublicacao.Should().Be(HttpStatusCode.OK);

        return new CenarioCruzado(artigoId, artigoSlug, erroSlug);
    }

    private async Task<IReadOnlyList<string>> SlugsRelacionadosDoErroAsync(string erroSlug)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/erros/{erroSlug}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: $"o erro {erroSlug} está publicado");

        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        dados.TryGetProperty("artigosRelacionados", out var bloco).Should().BeTrue(
            because: "o bloco aditivo de P6 é sempre presente, nunca omitido");

        return bloco.EnumerateArray().Select(a => a.GetProperty("slug").GetString()!).ToList();
    }

    private async Task<JsonElement> ObterArtigoAdminAsync(int id)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/interno/artigos/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private async Task<bool> PublicadoAsync(int comunicadoId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT publicado FROM codex.comunicados WHERE comunicadoid = @id";
        comando.Parameters.AddWithValue("id", comunicadoId);
        return (bool)(await comando.ExecuteScalarAsync())!;
    }

    private async Task<bool> EsperarPublicadoAsync(int comunicadoId, bool esperado)
    {
        var cronometro = Stopwatch.StartNew();
        while (cronometro.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (await PublicadoAsync(comunicadoId) == esperado)
                return true;
            await Task.Delay(100);
        }
        return false;
    }

    private async Task<bool> VisivelPublicoNoIndiceAsync(int artigoId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT visivelpublico FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id";
        comando.Parameters.AddWithValue("id", artigoId);
        var valor = await comando.ExecuteScalarAsync();
        valor.Should().NotBeNull(because: $"o artigo {artigoId} precisa estar indexado");
        return (bool)valor!;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpo, uint? ifMatch)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return (resposta.StatusCode, json);
    }
}
