using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// M28(a) — "erro COM vínculos só é público se ao menos uma versão vinculada for pública".
///
/// <para>
/// O ponto destes testes é a DISTINÇÃO, não a exclusão: dois casos diferentes produziam o MESMO
/// payload público (os três campos de rótulo nulos, que são o sinal do selo "Geral" de U9), e por
/// isso um erro específico de prefeitura cujas versões estão todas em rascunho ficava
/// indistinguível de um erro que vale para todos os provedores. Por isso cada exclusão vem
/// acompanhada da inclusão equivalente: sem o par, "não aparece" seria satisfeito por um filtro
/// que simplesmente esconde todo erro com vínculo — e cegaria o caso genuinamente geral.
/// </para>
///
/// <para>
/// Os três erros de cada caso são indexados sob o MESMO termo único (AP-QA-002), numa consulta
/// só: é a comparação lado a lado que prova a distinção, não três buscas isoladas.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class VisibilidadeVinculoErroBuscaTests
{
    private readonly PostgresContainerFixture _fixture;

    public VisibilidadeVinculoErroBuscaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private async Task<IReadOnlyList<BuscaItemDto>> BuscarAsync(string termo, bool incluirRascunho = false)
    {
        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            termo, null, null, 1, 20, incluirRascunho, CancellationToken.None);

        return resultado.Grupos.SelectMany(g => g.Itens).ToList();
    }

    /// <summary>Versão de um provedor publicado, com a publicação da VERSÃO controlada pelo caso.</summary>
    private static async Task<int> CriarVersaoAsync(CodexDbContext contexto, bool versaoPublicada)
    {
        var sufixo = NovoSufixo();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout m28par {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor m28par {sufixo}", $"M28P-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao m28par {sufixo}");
        if (versaoPublicada)
            versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return versao.Id;
    }

    /// <summary>Erro publicado e indexado sob <paramref name="termo"/>, vinculado às versões dadas (nenhuma = erro geral de U9).</summary>
    private static async Task<string> IndexarErroAsync(
        CodexDbContext contexto, string termo, string titulo, IReadOnlyCollection<int> versaoIds)
    {
        var erro = Erro.Criar($"M28P-{NovoSufixo()}", OrigemErro.Prefeitura, titulo);
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        foreach (var versaoId in versaoIds)
            contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versaoId));

        var item = ItemBusca.Criar(
            TipoItemIndexado.erro, erro.Id, titulo, $"/erros/{erro.Slug}", chave: erro.Codigo);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
        return titulo;
    }

    [Fact]
    public async Task BuscaPublica_SeparaErroGeralDeErroComVinculosTodosEmRascunho()
    {
        var termo = $"vincpar{NovoSufixo()}";
        string tituloGeral, tituloRascunho, tituloPublicado;

        await using (var contexto = _fixture.CriarContexto())
        {
            var versaoRascunho = await CriarVersaoAsync(contexto, versaoPublicada: false);
            var versaoPublicada = await CriarVersaoAsync(contexto, versaoPublicada: true);

            tituloGeral = await IndexarErroAsync(contexto, termo, $"Erro geral {termo}", []);
            tituloRascunho = await IndexarErroAsync(contexto, termo, $"Erro so rascunho {termo}", [versaoRascunho]);
            tituloPublicado = await IndexarErroAsync(contexto, termo, $"Erro com publicada {termo}", [versaoPublicada]);
        }

        var titulosPublicos = (await BuscarAsync(termo)).Select(i => i.Titulo).ToList();

        titulosPublicos.Should().Contain(tituloGeral,
            because: "erro SEM vínculo nenhum é o erro genuinamente geral de U9 — continua público");
        titulosPublicos.Should().Contain(tituloPublicado,
            because: "basta UMA versão vinculada pública para o erro ter contexto que o justifique");
        titulosPublicos.Should().NotContain(tituloRascunho,
            because: "M28(a): com vínculos e nenhum público, o erro sai da busca pública — e sobretudo não pode " +
                     "sair com os três campos nulos, que é o payload do erro GERAL");
    }

    [Fact]
    public async Task BuscaInterna_ContinuaVendoOsTres_InclusiveOQueOPublicoNaoVe()
    {
        var termo = $"vincparint{NovoSufixo()}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var versaoRascunho = await CriarVersaoAsync(contexto, versaoPublicada: false);
            var versaoPublicada = await CriarVersaoAsync(contexto, versaoPublicada: true);

            await IndexarErroAsync(contexto, termo, $"Erro geral {termo}", []);
            await IndexarErroAsync(contexto, termo, $"Erro so rascunho {termo}", [versaoRascunho]);
            await IndexarErroAsync(contexto, termo, $"Erro com publicada {termo}", [versaoPublicada]);
        }

        (await BuscarAsync(termo, incluirRascunho: true)).Should().HaveCount(3,
            because: "a redação precisa enxergar o erro em rascunho para saber que ele existe (M28, segunda parte) — " +
                     "a correção não pode cegar a ferramenta interna, que foi o erro que o P-08 ensinou");
    }

    /// <summary>
    /// O erro que só tem vínculo com versão REMOVIDA volta a ser geral: soft delete apaga o
    /// vínculo, exatamente como na hidratação de rótulos e no filtro <c>?provedor=</c>. Fixa a
    /// escolha, que não é óbvia, para que ninguém a inverta sem perceber.
    /// </summary>
    [Fact]
    public async Task VinculoComVersaoRemovida_NaoContaComoVinculo_ErroSegueGeralEPublico()
    {
        var termo = $"vincparrem{NovoSufixo()}";
        string titulo;

        await using (var contexto = _fixture.CriarContexto())
        {
            var versaoId = await CriarVersaoAsync(contexto, versaoPublicada: true);
            titulo = await IndexarErroAsync(contexto, termo, $"Erro versao removida {termo}", [versaoId]);

            var versao = await contexto.VersoesProvedor.FirstAsync(v => v.Id == versaoId);
            versao.Desativar();
            await contexto.SaveChangesAsync();
        }

        var item = (await BuscarAsync(termo)).Should().ContainSingle().Subject;

        item.Titulo.Should().Be(titulo);
        item.Versoes.Should().BeNull(because: "sem vínculo ativo, os três campos nulos são a resposta CERTA (U9)");
    }
}
