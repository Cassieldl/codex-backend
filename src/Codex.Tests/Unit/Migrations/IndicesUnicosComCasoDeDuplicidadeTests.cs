using Codex.Infrastructure.Data;
using Codex.Tests.Integration.Interno;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Unit.Migrations;

/// <summary>Todo índice único do modelo tem caso de POST duplicado ou exclusão com motivo.</summary>
public sealed class IndicesUnicosComCasoDeDuplicidadeTests
{
    // Só o modelo é lido; a conexão nunca é aberta.
    private const string ConexaoNuncaAberta = "Host=localhost;Database=codex_db;Username=codex_user;Password=nao-usada";

    private static readonly Dictionary<string, string> ExcluidosComMotivo = new(StringComparer.Ordinal)
    {
        ["uq_codex_aliasesprovedor_provedorid_alias"] = "alias repetido no mesmo corpo é recusado com 422 pelo validator, coberto por ProvedorAliasesValidacaoHttpTests.Criar_ComAliasRepetido_Devolve422 e Atualizar_ComAliasRepetido_Devolve422EMantemOsAliasesAtuais",
        ["uq_codex_exemplos_versaoprovedorid_nome"] = "coberto por ExemplosInternoHttpTests.I39_Criar_ComNomeJaUsadoNaVersao_Devolve409",
        ["uq_codex_exemplos_versaoprovedorid_slug"] = "o service sufixa o slug repetido, coberto por I39_Criar_ComNomeQueColideNoSlug_RecebeSufixoDeterministico",
        ["uq_codex_naturezasoperacao_versaoprovedorid_codigoinvoicy"] = "código repetido no mesmo corpo é recusado com 422 pelo validator, coberto por NaturezasOperacaoNaPaginaVersaoHttpTests.CodigoRepetido_Devolve422_ENaoTocaAListaAtual",
        ["uq_codex_itensbusca_tipoitem_itemid"] = "índice de busca gravado só pelo IndiceBuscaWriter, sem POST que o escreva",
        ["uq_codex_redirecionamentos_origem"] = "redirecionamento nasce da troca de slug, coberto por TrocaSlugHttpTests",
        ["uq_codex_termosbusca_termo"] = "termo gravado pela busca com ON CONFLICT, nunca colide",
        ["uq_codex_versoeslayout_ordem"] = "POST calcula a ordem; colisão por PUT coberta em PromocaoVigenteVersaoLayoutHttpTests",
        ["uq_codex_versoeslayout_estado_vigente"] = "vigente só muda por PUT, coberto em PromocaoVigenteVersaoLayoutHttpTests",
    };

    [Fact]
    public void TodoIndiceUnicoDoModelo_EstaNoCasoDePostDuplicadoOuNasExclusoes()
    {
        var doModelo = IndicesUnicosDoModelo();
        IEnumerable<object[]> casosDePostDuplicado = PostDuplicadoPorIndiceUnicoHttpTests.IndicesUnicos();
        var testados = casosDePostDuplicado
            .Select(linha => (string)linha[0])
            .ToHashSet(StringComparer.Ordinal);

        doModelo.Except(testados).Except(ExcluidosComMotivo.Keys).Should().BeEmpty(
            because: "índice único novo precisa de caso em PostDuplicadoPorIndiceUnicoHttpTests ou de exclusão com motivo aqui");

        testados.Concat(ExcluidosComMotivo.Keys).Except(doModelo).Should().BeEmpty(
            because: "nome que não existe mais no modelo esconde o índice renomeado");

        testados.Intersect(ExcluidosComMotivo.Keys).Should().BeEmpty(
            because: "índice testado não pode estar também nas exclusões");
    }

    private static HashSet<string> IndicesUnicosDoModelo()
    {
        var opcoes = new DbContextOptionsBuilder<CodexDbContext>()
            .UseNpgsql(ConexaoNuncaAberta)
            .Options;

        using var contexto = new CodexDbContext(opcoes);

        return contexto.Model.GetRelationalModel().Tables
            .SelectMany(tabela => tabela.Indexes)
            .Where(indice => indice.IsUnique)
            .Select(indice => indice.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
