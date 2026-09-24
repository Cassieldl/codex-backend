using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// `codex.normalizar_busca` emite três variantes do texto, e a terceira remove TODO caractere não
/// alfanumérico — inclusive os espaços. Ela existe de propósito, para casar busca com separador
/// colado (`NF-e` ↔ `nfe`), mas produz um único token que cresce junto com o texto indexado.
///
/// <para>
/// `codex.termosbusca.termo` é `varchar(120)`. Enquanto o filtro de tokens teve só piso
/// (`length >= 3`) e nenhum teto, publicar conteúdo minimamente longo — um artigo, um erro com
/// variantes, um campo de layout com significado por extenso — estourava o `INSERT` com
/// `22001 value too long` e derrubava a **requisição inteira** com 500.
/// </para>
///
/// <para>
/// Encontrado ao semear o acervo de demonstração no ambiente local: um campo de layout com nome,
/// caminho XML e significado normais gerou um token de 106 caracteres — a um fôlego do limite.
/// Nenhuma suíte pegava, porque todas as fixtures usavam textos curtos.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LexicoTokenLongoTests
{
    private readonly PostgresContainerFixture _fixture;

    public LexicoTokenLongoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Texto realista de conteúdo do produto — não uma string artificial de 200 letras: o token
    /// gigante nasce da CONCATENAÇÃO sem espaços que a normalização faz, então qualquer parágrafo
    /// comum ultrapassa o limite.
    /// </summary>
    private const string TextoLongoRealista =
        "Inscrição municipal do prestador não encontrada no cadastro mobiliário do município, "
        + "impedindo a emissão da nota fiscal de serviço eletrônica neste provedor.";

    [Fact]
    public async Task TextoQueGeraTokenAcimaDoLimite_NaoDerrubaAIndexacao()
    {
        await using var contexto = _fixture.CriarContexto();

        // Confirma a premissa do teste no próprio banco: sem um token acima de 120, o caso não
        // exercita nada e passaria por construção.
        var maiorToken = await contexto.Database
            .SqlQuery<int>($"""
                SELECT COALESCE(MAX(length(t)), 0) AS "Value"
                FROM regexp_split_to_table(codex.normalizar_busca({TextoLongoRealista}), '\s+') AS t
                """)
            .SingleAsync();

        maiorToken.Should().BeGreaterThan(120,
            because: "o caso só prova algo se a normalização realmente produzir um token acima do limite da coluna");

        var itemId = await SemearItemAsync(contexto);
        var writer = new IndiceBuscaWriter(contexto);

        var acao = async () => await writer.AtualizarPesosAsync(
            TipoItemIndexado.erro, itemId, TextoLongoRealista, null, null, null, CancellationToken.None);

        await acao.Should().NotThrowAsync(
            because: "conteúdo longo é conteúdo normal do produto — indexá-lo não pode virar 500");
    }

    /// <summary>
    /// `AtualizarPesosAsync` lê a entidade de ORIGEM para montar os metadados do índice, então o
    /// erro precisa existir de verdade — indexar um id solto não exercita o caminho real.
    /// </summary>
    private static async Task<int> SemearItemAsync(Codex.Infrastructure.Data.CodexDbContext contexto)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var erro = Erro.Criar($"E-LEX-{sufixo}", OrigemErro.Prefeitura, $"Erro de léxico {sufixo}");

        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        return erro.Id;
    }

    /// <summary>
    /// O token gigante é descartado, não truncado: um pedaço arbitrário de palavras coladas não
    /// casaria com busca nenhuma e só poluiria o léxico de sugestão.
    /// </summary>
    [Fact]
    public async Task TokenAcimaDoLimite_NaoEntraNoLexico_EOsCurtosEntram()
    {
        await using var contexto = _fixture.CriarContexto();

        var marcador = $"marcadorlexico{Guid.NewGuid():N}"[..28];
        var texto = $"{marcador} {TextoLongoRealista}";

        var itemId = await SemearItemAsync(contexto);
        var writer = new IndiceBuscaWriter(contexto);

        await writer.AtualizarPesosAsync(
            TipoItemIndexado.erro, itemId, texto, null, null, null, CancellationToken.None);

        var termoCurtoIndexado = await contexto.Database
            .SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM codex.termosbusca WHERE termo = {marcador}""")
            .SingleAsync();

        termoCurtoIndexado.Should().Be(1, because: "os tokens dentro do limite continuam alimentando o léxico");

        var termosAcimaDoLimite = await contexto.Database
            .SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM codex.termosbusca WHERE length(termo) > 120""")
            .SingleAsync();

        termosAcimaDoLimite.Should().Be(0);
    }
}
