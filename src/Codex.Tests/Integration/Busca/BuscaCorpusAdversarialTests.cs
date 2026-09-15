using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Corpus adversarial exigido por analise-backend.md §9: acento, caixa, plural, separador
/// colado, código compactado, typo com autocorreção, trecho de várias linhas e <c>?provedor=</c>
/// com <c>foraDoFiltro</c>. Cada cenário combina uma palavra comum do português (para exercitar
/// de verdade o stemmer/unaccent) com um token único gerado por teste — o token único garante
/// isolamento entre testes que compartilham o mesmo container Postgres (coleção), a palavra
/// comum garante que a normalização realmente está sendo testada, não só a igualdade de string.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class BuscaCorpusAdversarialTests
{
    private readonly PostgresContainerFixture _fixture;

    public BuscaCorpusAdversarialTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Linha de projeção sintética. <paramref name="tipoItem"/> existe porque o vínculo com
    /// provedor NÃO é o mesmo para todo tipo: <c>provedor</c>/<c>versaoprovedor</c>/<c>aviso</c>
    /// carregam o escalar <c>provedorId</c>, enquanto <c>erro</c> é sempre gravado pelo writer com
    /// <c>provedorId = null</c> — o vínculo dele é indireto (<c>errosversoes</c>) e resolvido por
    /// consulta publicação-aware. Um item <c>erro</c> com <c>provedorId</c> preenchido é estado
    /// impossível em produção, então cenário de filtro por provedor usa um tipo que de fato o
    /// carrega.
    /// </summary>
    private static async Task<int> IndexarItemAsync(
        CodexDbContext contexto,
        string pesoA,
        int? provedorId = null,
        TipoItemIndexado tipoItem = TipoItemIndexado.erro)
    {
        var itemId = Random.Shared.Next(1_000_000, int.MaxValue);
        var item = ItemBusca.Criar(tipoItem, itemId, $"Item {itemId}", $"/erros/item-{itemId}", provedorId: provedorId);
        item.AtualizarPesos(pesoA, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);
        await contexto.SaveChangesAsync();
        return itemId;
    }

    [Fact]
    public async Task Busca_InsensivelAAcento_AliquotaSemAcentoAchaAliquotaComAcento()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"alíquota casocascata{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"aliquota casocascata{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("exata");
        resultado.TotalGeral.Should().Be(1);
    }

    [Fact]
    public async Task Busca_InsensivelACaixa_MaiusculaAchaMinuscula()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"REJEICAO casocascata{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"rejeicao casocascata{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("exata");
        resultado.TotalGeral.Should().Be(1);
    }

    [Fact]
    public async Task Busca_TratraPlural_NotasAchaNota()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"nota casocascata{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"notas casocascata{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.TotalGeral.Should().Be(1, because: "o stemmer português deve equiparar singular e plural");
    }

    [Fact]
    public async Task Busca_SeparadorColado_AliquotaIssAchaAliquotaIsolado()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"Aliquota/ISS casocascata{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"aliquota casocascata{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.TotalGeral.Should().Be(1,
            because: "separador colado precisa virar espaço (forma quebrada de codex.normalizar_busca) — regressão já observada neste projeto");
    }

    [Fact]
    public async Task Busca_CodigoCompactado_E124AchaEHifen124()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"E-124 casocascata{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"E124 casocascata{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.TotalGeral.Should().Be(1, because: "forma compactada equipara 'E124' e 'E-124'");
    }

    [Fact]
    public async Task Busca_TypoComAutocorrecao_DevolveCorrecaoExplicita()
    {
        var sufixo = NovoSufixo();
        var termoCorreto = $"cascataaliquota{sufixo}";
        var termoComTypo = $"cascataaliqouta{sufixo}";

        await using (var setup = _fixture.CriarContexto())
        {
            await IndexarItemAsync(setup, termoCorreto);
            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO codex.termosbusca (termo, ocorrencias) VALUES ({termoCorreto}, 5) ON CONFLICT (termo) DO NOTHING");
        }

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            termoComTypo, null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("aproximada");
        resultado.Correcao.Should().NotBeNull();
        resultado.Correcao!.TermoCorrigido.Should().Contain(termoCorreto);
        resultado.TotalGeral.Should().Be(1);
    }

    [Fact]
    public async Task Busca_TrechoDeVariasLinhas_NaoLancaErroDeSintaxeEProduzFragmento()
    {
        var sufixo = NovoSufixo();
        // Vocabulário deliberadamente sem "nota"/"aliquota": esses termos são o alvo exato de
        // Busca_TratraPlural_NotasAchaNota e Busca_SeparadorColado_AliquotaIssAchaAliquotaIsolado,
        // que caem no passo PARCIAL (OR entre lexemas) — se este texto contivesse essas palavras,
        // qualquer teste da coleção (mesmo container Postgres) que caísse em PARCIAL acharia esta
        // linha por engano, contaminando o TotalGeral (bug de isolamento de teste, não de produção).
        var textoRejeicao =
            $"Rejeicao ao processar arquivo XML casocascata{sufixo}\n" +
            "Motivo: cadastro informado diverge do registro municipal\n" +
            "Verifique o codigo do servico antes de reenviar";

        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, textoRejeicao);

        await using var contexto = _fixture.CriarContexto();
        var consulta = CriarConsulta(contexto);

        var acao = async () => await consulta.BuscarAsync(textoRejeicao, null, null, 1, 20, incluirRascunho: false, CancellationToken.None);
        await acao.Should().NotThrowAsync(because: "websearch_to_tsquery aceita entrada humana arbitrária, inclusive múltiplas linhas, sem lançar erro de sintaxe");

        var resultado = await consulta.BuscarAsync(textoRejeicao, null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.TotalGeral.Should().Be(1);
        resultado.Grupos.SelectMany(g => g.Itens).Single().Trechos.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Busca_ComFiltroDeProvedor_SeparaDentroDeForaDoFiltro()
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        // Publicados de propósito: as duas linhas de projeção nascem com publicado = true, e
        // provedor em rascunho com linha publicada é estado impossível em produção (a cascata de
        // visibilidade sincroniza os dois). Sem Publicar(), este cenário exercitaria um provedor
        // que a superfície pública não pode nem confirmar que existe.
        var provedorDentro = Provedor.Criar($"Provedor Dentro {sufixo}", $"INV-DENTRO-{sufixo}", CategoriaProvedor.Nacional);
        provedorDentro.Publicar();
        var provedorFora = Provedor.Criar($"Provedor Fora {sufixo}", $"INV-FORA-{sufixo}", CategoriaProvedor.Nacional);
        provedorFora.Publicar();
        contexto.Provedores.AddRange(provedorDentro, provedorFora);
        await contexto.SaveChangesAsync();

        var termoComum = $"casocascatafiltro{sufixo}";
        await IndexarItemAsync(contexto, termoComum, provedorDentro.Id, TipoItemIndexado.provedor);
        await IndexarItemAsync(contexto, termoComum, provedorFora.Id, TipoItemIndexado.provedor);

        await using var contextoConsulta = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contextoConsulta).BuscarAsync(
            termoComum, provedorDentro.Slug, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.FiltroProvedor.Should().NotBeNull();
        resultado.FiltroProvedor!.Slug.Should().Be(provedorDentro.Slug);
        resultado.TotalGeral.Should().Be(1, because: "só o item do provedor filtrado entra em TotalGeral/Grupos");
        resultado.ForaDoFiltro.Should().NotBeNull();
        resultado.ForaDoFiltro!.Total.Should().Be(1, because: "o item do outro provedor existe, mas fica fora do filtro aplicado");
    }
}
