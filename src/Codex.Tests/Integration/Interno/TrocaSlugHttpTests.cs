using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// P-10 ponta a ponta por HTTP real (I37/I43/I44, analise-backend.md §3.2): trocar o slug, gravar
/// o redirecionamento, desativar a linha que o ciclo de renomeações torna sem sentido (B-4),
/// colapsar a cadeia e reindexar a própria linha de busca — tudo na mesma transação.
///
/// <para>
/// Os desfechos são lidos do JSON e do BANCO, nunca do objeto C#: o que sustenta o produto é o
/// 308 servido ao leitor e a linha de <c>codex.redirecionamentos</c>, não o retorno do service.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class TrocaSlugHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-publicar,"
        + "versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-publicar,"
        + "erros-buscar,erros-adicionar,erros-atualizar,erros-publicar,"
        + "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-publicar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    /// <summary>Artigo reservado semeado por esta classe, se houver — o slug é global e único.</summary>
    private int? _reservadoSemeadoId;

    public TrocaSlugHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);

        // Sem auto-follow: o caso PROVA o 308 e o Location. Com follow, o cliente entregaria o
        // 200 do destino e o teste passaria mesmo sem redirect nenhum configurado.
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_reservadoSemeadoId is { } id)
            await RemoverArtigoAsync(id);

        _cliente.Dispose();
        _factory.Dispose();
    }

    // ------------------------------------------------------------------ I37 — provedor

    [Fact]
    public async Task I37_TrocarSlug_GravaRedirecionamentoEOSlugAntigoPassaAResponder308()
    {
        var provedor = await CriarProvedorPublicadoAsync();
        var slugNovo = $"slug-novo-{NovoSufixo()}";

        var (status, corpo, resposta) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/slug", new { slug = slugNovo }, provedor.Versao);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("slug").GetString().Should().Be(slugNovo);
        resposta.Headers.ETag.Should().NotBeNull(because: "a troca muda o xmin — sem ETag nova o cliente perde o If-Match seguinte");

        var redirecionamento = await ObterRedirecionamentoAtivoAsync($"/provedores/{provedor.Slug}");
        redirecionamento.Should().Be($"/provedores/{slugNovo}",
            because: "o destino é guardado no formato PÚBLICO — a tradução para rota de API é da borda HTTP (F1)");

        using var respostaPublica = await _cliente.GetAsync($"/api/v1/provedores/{provedor.Slug}");
        respostaPublica.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        respostaPublica.Headers.Location!.ToString().Should().Be($"/api/v1/provedores/{slugNovo}");

        using var respostaNova = await _cliente.GetAsync($"/api/v1/provedores/{slugNovo}");
        respostaNova.StatusCode.Should().Be(HttpStatusCode.OK, because: "o slug vivo resolve direto, sem passar pela tabela de redirect");
    }

    /// <summary>
    /// O slug chega como texto livre do formulário e quem normaliza é o backend — o frontend nunca
    /// é a última linha de defesa (rules/security.md).
    /// </summary>
    [Fact]
    public async Task I37_TrocarSlug_NormalizaOTextoLivreNoBackend()
    {
        var provedor = await CriarProvedorPublicadoAsync();
        var sufixo = NovoSufixo();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/slug",
            new { slug = $"  Prefeitura de São Paulo {sufixo}  " }, provedor.Versao);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("slug").GetString().Should().Be($"prefeitura-de-sao-paulo-{sufixo}");
    }

    [Fact]
    public async Task I37_TrocarSlug_SemIfMatch_Devolve400()
    {
        var provedor = await CriarProvedorPublicadoAsync();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/slug", new { slug = $"qualquer-{NovoSufixo()}" });

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task I37_TrocarSlug_ParaOMesmoSlug_Devolve422ComCodigoProprio()
    {
        var provedor = await CriarProvedorPublicadoAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/slug", new { slug = provedor.Slug }, provedor.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "slug igual ao atual não viola UNIQUE nenhum — o UPDATE passaria em silêncio e o redirect apontaria de um caminho para ele mesmo");
        CodigoDe(corpo).Should().Be(CodigosErro.SlugIgualAoAtual);

        (await ContarRedirecionamentosAsync($"/provedores/{provedor.Slug}")).Should().Be(0,
            because: "a recusa é anterior a qualquer escrita");
    }

    [Fact]
    public async Task I37_TrocarSlug_ParaSlugDeOutroProvedor_Devolve409()
    {
        var primeiro = await CriarProvedorPublicadoAsync();
        var segundo = await CriarProvedorPublicadoAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{segundo.Id}/slug", new { slug = primeiro.Slug }, segundo.Versao);

        status.Should().Be(HttpStatusCode.Conflict);
        CodigoDe(corpo).Should().Be("REGISTRO_DUPLICADO");
        MensagemDe(corpo).Should().NotContain("uq_codex_",
            because: "nome de constraint é detalhe interno do schema (CWE-209)");

        var slugPersistido = await ObterSlugDoProvedorAsync(segundo.Id);
        slugPersistido.Should().Be(segundo.Slug, because: "a transação inteira volta atrás — inclusive a linha da entidade");
    }

    [Fact]
    public async Task I37_TrocarSlug_DeProvedorInexistente_Devolve404()
    {
        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, "/api/v1/interno/provedores/999999/slug", new { slug = "qualquer-coisa" }, 1u);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Ciclo A→B→A (B-4) — o caso que o colapso de cadeia sozinho resolve errado. Sem desativar a
    /// linha cuja ORIGEM volta a ficar viva, o <c>UPDATE ... WHERE destino = B</c> reescreveria
    /// <c>{A→B}</c> como <c>{A→A}</c>: um redirect que nunca é alcançado (o slug vivo resolve
    /// antes) e que mente sobre o que é.
    ///
    /// <para>
    /// O terceiro passo (A→C) é o que prova que a segunda saída de <c>A</c> convive com a linha
    /// desativada do passo 1 — <c>uq_codex_redirecionamentos_origem</c> é único GLOBAL, sem filtro
    /// por <c>desativadoem</c>, então um INSERT cego morreria em 409 aqui.
    /// </para>
    /// </summary>
    [Fact]
    public async Task I37_CicloDeRenomeacoes_DesativaAOrigemQueRevive_ENuncaGeraRedirectParaSiMesmo()
    {
        var provedor = await CriarProvedorPublicadoAsync();
        var slugA = provedor.Slug;
        var slugB = $"ciclo-b-{NovoSufixo()}";
        var slugC = $"ciclo-c-{NovoSufixo()}";

        var versaoAposB = await TrocarSlugDoProvedorAsync(provedor.Id, provedor.Versao, slugB);
        var versaoAposVoltaParaA = await TrocarSlugDoProvedorAsync(provedor.Id, versaoAposB, slugA);

        var linhaDeA = await ObterRedirecionamentoAsync($"/provedores/{slugA}");
        linhaDeA.Should().NotBeNull();
        linhaDeA!.Value.Destino.Should().Be($"/provedores/{slugB}",
            because: "a linha do passo 1 não pode ter sido reescrita para apontar de A para A");
        linhaDeA.Value.Ativo.Should().BeFalse(because: "A voltou a ser o slug vivo — não parte mais para lugar nenhum");

        var linhaDeB = await ObterRedirecionamentoAsync($"/provedores/{slugB}");
        linhaDeB.Should().NotBeNull();
        linhaDeB!.Value.Destino.Should().Be($"/provedores/{slugA}");
        linhaDeB.Value.Ativo.Should().BeTrue();

        using (var respostaDeB = await _cliente.GetAsync($"/api/v1/provedores/{slugB}"))
        {
            respostaDeB.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
            respostaDeB.Headers.Location!.ToString().Should().Be($"/api/v1/provedores/{slugA}");
        }

        using (var respostaDeA = await _cliente.GetAsync($"/api/v1/provedores/{slugA}"))
        {
            respostaDeA.StatusCode.Should().Be(HttpStatusCode.OK,
                because: "A é o slug vivo — nunca alcança a tabela de redirect");
        }

        // Terceiro passo: A→C. A origem /provedores/A já existe (desativada, do passo 1).
        await TrocarSlugDoProvedorAsync(provedor.Id, versaoAposVoltaParaA, slugC);

        var linhaDeAAposC = await ObterRedirecionamentoAsync($"/provedores/{slugA}");
        linhaDeAAposC!.Value.Destino.Should().Be($"/provedores/{slugC}");
        linhaDeAAposC.Value.Ativo.Should().BeTrue();

        var linhaDeBAposC = await ObterRedirecionamentoAsync($"/provedores/{slugB}");
        linhaDeBAposC!.Value.Destino.Should().Be($"/provedores/{slugC}",
            because: "colapso de cadeia (E2): quem apontava para A, que virou slug morto, passa a apontar para C");
        linhaDeBAposC.Value.Ativo.Should().BeTrue();

        foreach (var slugAbandonado in new[] { slugA, slugB })
        {
            using var resposta = await _cliente.GetAsync($"/api/v1/provedores/{slugAbandonado}");
            resposta.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
            resposta.Headers.Location!.ToString().Should().Be($"/api/v1/provedores/{slugC}",
                because: "nenhum salto duplo: os dois slugs mortos levam direto ao vivo");
        }
    }

    /// <summary>
    /// Critério 2 de §3.1 — a decisão B amarrada a P-10: a URL da VERSÃO (linha filha, que a troca
    /// de slug não reindexa) já sai correta na busca, sem chamar reconstrução nenhuma.
    /// </summary>
    [Fact]
    public async Task I37_TrocarSlug_BuscaDaVersaoFilhaJaRespondeComAUrlNova_SemReindexar()
    {
        var provedor = await CriarProvedorPublicadoAsync();
        var versao = await CriarVersaoPublicadaAsync(provedor.Id);
        var slugNovo = $"slug-novo-{NovoSufixo()}";

        await TrocarSlugDoProvedorAsync(provedor.Id, provedor.Versao, slugNovo);

        var urlDaVersao = await BuscarUrlAsync(versao.TermoDeBusca, "versaoprovedor");
        urlDaVersao.Should().Be($"/provedores/{slugNovo}/{versao.Slug}",
            because: "a URL é derivada do slug corrente do provedor na hidratação, não lida de itensbusca.url");

        var urlDoProvedor = await BuscarUrlAsync(provedor.TermoDeBusca, "provedor");
        urlDoProvedor.Should().Be($"/provedores/{slugNovo}");
    }

    /// <summary>
    /// A reindexação da própria linha usa <c>PesosBusca.DoProvedor</c>, que lê os ALIASES. Se a
    /// entidade for carregada sem a coleção, o peso B sai só com o nome e a troca de slug apaga os
    /// apelidos do índice em silêncio — o provedor some da busca por alias.
    /// </summary>
    [Fact]
    public async Task I37_TrocarSlug_PreservaOsAliasesNoIndiceDeBusca()
    {
        var alias = $"apelidobusca{NovoSufixo()}";
        var provedor = await CriarProvedorPublicadoAsync(alias);

        (await BuscarUrlAsync(alias, "provedor")).Should().NotBeNull(
            because: "controle positivo: o alias já indexava antes da troca");

        var slugNovo = $"slug-novo-{NovoSufixo()}";
        await TrocarSlugDoProvedorAsync(provedor.Id, provedor.Versao, slugNovo);

        (await BuscarUrlAsync(alias, "provedor")).Should().Be($"/provedores/{slugNovo}");
    }

    // ------------------------------------------------------------------ I43 — erro

    [Fact]
    public async Task I43_TrocarSlugDeErro_GravaRedirecionamentoEOSlugAntigoResponde308()
    {
        var erro = await CriarErroPublicadoAsync();
        var slugNovo = $"erro-novo-{NovoSufixo()}";

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}/slug", new { slug = slugNovo }, erro.Versao);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("slug").GetString().Should().Be(slugNovo);

        (await ObterRedirecionamentoAtivoAsync($"/erros/{erro.Slug}")).Should().Be($"/erros/{slugNovo}");

        using var resposta = await _cliente.GetAsync($"/api/v1/erros/{erro.Slug}");
        resposta.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        resposta.Headers.Location!.ToString().Should().Be($"/api/v1/erros/{slugNovo}");
    }

    [Fact]
    public async Task I43_TrocarSlugDeErro_ParaOMesmoSlug_Devolve422()
    {
        var erro = await CriarErroPublicadoAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}/slug", new { slug = erro.Slug }, erro.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodigoDe(corpo).Should().Be(CodigosErro.SlugIgualAoAtual);
    }

    /// <summary>
    /// As três rotas de troca de slug compartilham guarda de If-Match e de colisão, mas só I37
    /// tinha detector: assimetria entre irmãos é onde a regressão passa sem ninguém ver.
    /// </summary>
    [Fact]
    public async Task I43_TrocarSlugDeErro_SemIfMatch_Devolve400()
    {
        var erro = await CriarErroPublicadoAsync();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}/slug", new { slug = $"erro-novo-{NovoSufixo()}" });

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task I43_TrocarSlugDeErro_ParaSlugDeOutroErro_Devolve409()
    {
        var primeiro = await CriarErroPublicadoAsync();
        var segundo = await CriarErroPublicadoAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{segundo.Id}/slug", new { slug = primeiro.Slug }, segundo.Versao);

        status.Should().Be(HttpStatusCode.Conflict);
        CodigoDe(corpo).Should().Be("REGISTRO_DUPLICADO");
        MensagemDe(corpo).Should().NotContain("uq_codex_",
            because: "nome de constraint é detalhe interno do schema (CWE-209)");
    }

    [Fact]
    public async Task I43_TrocarSlugDeErro_AcimaDoTetoDaColuna_Devolve422()
    {
        var erro = await CriarErroPublicadoAsync();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}/slug", new { slug = new string('a', 161) }, erro.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "o DTO é compartilhado e vale o teto da coluna mais estreita — 161 caracteres não cabe em nenhuma");
    }

    // ------------------------------------------------------------------ I44 — artigo

    [Fact]
    public async Task I44_TrocarSlugDeArtigo_GravaRedirecionamentoEOSlugAntigoResponde308()
    {
        var artigo = await CriarArtigoPublicadoAsync();
        var slugNovo = $"artigo-novo-{NovoSufixo()}";

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{artigo.Id}/slug", new { slug = slugNovo }, artigo.Versao);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("slug").GetString().Should().Be(slugNovo);

        (await ObterRedirecionamentoAtivoAsync($"/artigos/{artigo.Slug}")).Should().Be($"/artigos/{slugNovo}",
            because: "o CHECK de tipoitem passou a aceitar 'artigo' (F4) — antes a linha nem entrava");

        using var resposta = await _cliente.GetAsync($"/api/v1/artigos/{artigo.Slug}");
        resposta.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        resposta.Headers.Location!.ToString().Should().Be($"/api/v1/artigos/{slugNovo}");
    }

    [Fact]
    public async Task I44_TrocarSlugDeArtigo_SemIfMatch_Devolve400()
    {
        var artigo = await CriarArtigoPublicadoAsync();

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{artigo.Id}/slug", new { slug = $"artigo-novo-{NovoSufixo()}" });

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task I44_TrocarSlugDeArtigo_ParaSlugDeOutroArtigo_Devolve409()
    {
        var primeiro = await CriarArtigoPublicadoAsync();
        var segundo = await CriarArtigoPublicadoAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{segundo.Id}/slug", new { slug = primeiro.Slug }, segundo.Versao);

        status.Should().Be(HttpStatusCode.Conflict);
        CodigoDe(corpo).Should().Be("REGISTRO_DUPLICADO");
        MensagemDe(corpo).Should().NotContain("uq_codex_",
            because: "nome de constraint é detalhe interno do schema (CWE-209)");
    }

    [Fact]
    public async Task I44_TrocarSlugDoArtigoReservado_Devolve422ComSlugReservado()
    {
        var reservado = await GarantirArtigoReservadoAsync();

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{reservado.Id}/slug",
            new { slug = $"outro-nome-{NovoSufixo()}" }, reservado.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "trocar o slug do reservado deixaria a rota fixa /como-funciona órfã, em silêncio (§0.2)");
        CodigoDe(corpo).Should().Be(CodigosErro.SlugReservadoNaoPodeMudar);

        var slugPersistido = await ObterSlugDoArtigoAsync(reservado.Id);
        slugPersistido.Should().Be(SlugsReservadosArtigo.ComoFunciona);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record RecursoCriado(int Id, string Slug, uint Versao, string TermoDeBusca);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string? CodigoDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("code").GetString();

    private static string? MensagemDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("message").GetString();

    private async Task<RecursoCriado> CriarProvedorPublicadoAsync(string? alias = null)
    {
        var sufixo = NovoSufixo();
        var termo = $"provedorslug{sufixo}";

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor {termo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = alias is null ? Array.Empty<string>() : [alias],
        });

        status.Should().Be(HttpStatusCode.Created);
        var criado = corpo.GetProperty("data");
        var id = criado.GetProperty("id").GetInt32();

        var versao = await PublicarAsync(
            $"/api/v1/interno/provedores/{id}/publicacao", criado.GetProperty("versaoConcorrencia").GetUInt32());

        return new RecursoCriado(id, criado.GetProperty("slug").GetString()!, versao, termo);
    }

    private async Task<RecursoCriado> CriarVersaoPublicadaAsync(int provedorId)
    {
        var sufixo = NovoSufixo();
        var termo = $"versaoslug{sufixo}";
        var versaoLayoutId = await SemearVersaoLayoutAsync();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo = $"Versao {termo}",
        });

        status.Should().Be(HttpStatusCode.Created);
        var criada = corpo.GetProperty("data");
        var id = criada.GetProperty("id").GetInt32();

        var versao = await PublicarAsync(
            $"/api/v1/interno/versoes/{id}/publicacao", criada.GetProperty("versaoConcorrencia").GetUInt32());

        return new RecursoCriado(id, criada.GetProperty("slug").GetString()!, versao, termo);
    }

    private async Task<RecursoCriado> CriarErroPublicadoAsync()
    {
        var sufixo = NovoSufixo();
        var termo = $"erroslug{sufixo}";

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo = $"Erro {termo}",
            variantes = new[] { $"mensagem {termo}" },
        });

        status.Should().Be(HttpStatusCode.Created);
        var criado = corpo.GetProperty("data");
        var id = criado.GetProperty("id").GetInt32();

        var versao = await PublicarAsync(
            $"/api/v1/interno/erros/{id}/publicacao", criado.GetProperty("versaoConcorrencia").GetUInt32());

        return new RecursoCriado(id, criado.GetProperty("slug").GetString()!, versao, termo);
    }

    private async Task<RecursoCriado> CriarArtigoPublicadoAsync()
    {
        var sufixo = NovoSufixo();
        var termo = $"artigoslug{sufixo}";

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo = nameof(TipoArtigo.Conceito),
            titulo = $"Artigo {termo}",
            corpo = $"Corpo do artigo {termo}.",
        });

        status.Should().Be(HttpStatusCode.Created);
        var criado = corpo.GetProperty("data");
        var id = criado.GetProperty("id").GetInt32();

        var versao = await PublicarAsync(
            $"/api/v1/interno/artigos/{id}/publicacao", criado.GetProperty("versaoConcorrencia").GetUInt32());

        return new RecursoCriado(id, criado.GetProperty("slug").GetString()!, versao, termo);
    }

    private async Task<RecursoCriado> GarantirArtigoReservadoAsync()
    {
        int id;

        await using (var contexto = _fixture.CriarContexto())
        {
            var existente = await contexto.Artigos
                .FirstOrDefaultAsync(a => a.Slug == SlugsReservadosArtigo.ComoFunciona);

            if (existente is null)
            {
                var artigo = Artigo.Criar(TipoArtigo.Conceito, "Como funciona", "Corpo do artigo obrigatório.");
                contexto.Artigos.Add(artigo);
                await contexto.SaveChangesAsync();
                id = artigo.Id;
                _reservadoSemeadoId = id;
            }
            else
            {
                id = existente.Id;
            }
        }

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/artigos/{id}");
        status.Should().Be(HttpStatusCode.OK);

        var dados = corpo.GetProperty("data");
        dados.GetProperty("reservado").GetBoolean().Should().BeTrue(
            because: "sem isso o caso poderia estar exercitando um artigo comum e passando por engano");

        return new RecursoCriado(
            id, SlugsReservadosArtigo.ComoFunciona, dados.GetProperty("versaoConcorrencia").GetUInt32(), string.Empty);
    }

    private async Task<int> SemearVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout troca slug {NovoSufixo()}", maiorOrdem + 1);
        versaoLayout.Publicar();
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        return versaoLayout.Id;
    }

    private async Task<uint> PublicarAsync(string rota, uint versaoAtual)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, rota, new { publicado = true }, versaoAtual);
        status.Should().Be(HttpStatusCode.OK);

        return corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
    }

    private async Task<uint> TrocarSlugDoProvedorAsync(int provedorId, uint versaoAtual, string slugNovo)
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedorId}/slug", new { slug = slugNovo }, versaoAtual);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("slug").GetString().Should().Be(slugNovo);

        return corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
    }

    /// <summary>URL do único item daquele tipo devolvido pela busca pública — <c>null</c> quando a busca não achou nada.</summary>
    private async Task<string?> BuscarUrlAsync(string termo, string tipo)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/busca?q={termo}&tipo={tipo}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        var grupos = corpo.GetProperty("data").GetProperty("grupos");

        foreach (var grupo in grupos.EnumerateArray())
        {
            if (grupo.GetProperty("tipo").GetString() != tipo)
                continue;

            var itens = grupo.GetProperty("itens");
            itens.GetArrayLength().Should().Be(1, because: "o termo é exclusivo do caso");

            return itens[0].GetProperty("url").GetString();
        }

        return null;
    }

    private async Task<string?> ObterRedirecionamentoAtivoAsync(string origem)
    {
        var linha = await ObterRedirecionamentoAsync(origem);
        linha.Should().NotBeNull();
        linha!.Value.Ativo.Should().BeTrue();

        return linha.Value.Destino;
    }

    private async Task<(string Destino, bool Ativo)?> ObterRedirecionamentoAsync(string origem)
    {
        await using var contexto = _fixture.CriarContexto();

        var linha = await contexto.Redirecionamentos.AsNoTracking()
            .Where(r => r.Origem == origem)
            .Select(r => new { r.Destino, r.DesativadoEm })
            .SingleOrDefaultAsync();

        return linha is null ? null : (linha.Destino, linha.DesativadoEm is null);
    }

    private async Task<int> ContarRedirecionamentosAsync(string origem)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Redirecionamentos.AsNoTracking().CountAsync(r => r.Origem == origem);
    }

    private async Task<string> ObterSlugDoProvedorAsync(int id)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Provedores.AsNoTracking().Where(p => p.Id == id).Select(p => p.Slug).SingleAsync();
    }

    private async Task<string> ObterSlugDoArtigoAsync(int id)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Artigos.AsNoTracking().Where(a => a.Id == id).Select(a => a.Slug).SingleAsync();
    }

    /// <summary>
    /// Hard delete: o soft delete deixaria o slug reservado ocupado e
    /// <c>ContratoArtigoHttpTests.P8_ArtigoReservadoAusente_Retorna404</c> passaria a ver um
    /// artigo existente. Quem semeia recurso global e único limpa (gotcha de rules/tests.md).
    /// </summary>
    private async Task RemoverArtigoAsync(int id)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            DELETE FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id;
            DELETE FROM codex.artigos WHERE artigoid = @id;
            """;
        comando.Parameters.AddWithValue("id", id);

        await comando.ExecuteNonQueryAsync();
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
