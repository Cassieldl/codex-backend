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
/// PROBE DE QA (2ª rodada) — M28(a): "erro COM vínculos só é público se ao menos uma versão
/// vinculada for pública".
///
/// <para>A suíte cobre o caso MISTO (uma versão publicada + uma em rascunho, em
/// <c>RotulosResultadoBuscaTests.BuscarAsync_VersaoAplicavelDespublicada_SoApareceNaBuscaInterna</c>).
/// O caso que M28 foi criada para impedir — TODAS as versões vinculadas em rascunho — não é
/// exercitado em lugar nenhum, e <c>M28</c> não aparece em <c>analise-backend.md</c> nem em
/// <c>modelo-dados.md</c>.</para>
///
/// <para>Consequência que este probe mede: o erro específico de provedor fica indistinguível, no
/// payload público, de um erro GERAL de U9 — os três campos nulos são justamente o sinal do selo
/// "Geral".</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class M28ErroSemVersaoPublicaProbeTests
{
    private readonly PostgresContainerFixture _fixture;

    public M28ErroSemVersaoPublicaProbeTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<(int ProvedorId, int VersaoId)> CriarProvedorComVersaoEmRascunhoAsync(CodexDbContext contexto)
    {
        var sufixo = NovoSufixo();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout m28 {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        // Provedor PUBLICADO, versão em RASCUNHO: isola a regra em cima da versão, sem depender
        // da cascata do provedor.
        var provedor = Provedor.Criar($"Provedor m28 {sufixo}", $"M28-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao m28 {sufixo}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return (provedor.Id, versao.Id);
    }

    /// <summary>Erro publicado, de origem Prefeitura (U9 exige ao menos uma versão), vinculado SÓ a versões em rascunho.</summary>
    private static async Task<Erro> IndexarErroComTodasAsVersoesEmRascunhoAsync(
        CodexDbContext contexto, string termo, int versaoId, int provedorId)
    {
        var erro = Erro.Criar($"M28-{NovoSufixo()}", OrigemErro.Prefeitura, $"Erro m28 {termo}");
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versaoId));

        var item = ItemBusca.Criar(
            TipoItemIndexado.erro, erro.Id, erro.Titulo, $"/erros/{erro.Slug}",
            chave: erro.Codigo, provedorIds: [provedorId]);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
        return erro;
    }

    private async Task<IReadOnlyList<BuscaItemDto>> BuscarItensAsync(string termo, bool incluirRascunho = false)
    {
        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            termo, null, null, 1, 20, incluirRascunho, CancellationToken.None);

        return resultado.Grupos.SelectMany(g => g.Itens).ToList();
    }

    /// <summary>
    /// O erro é específico de um provedor (tem vínculo), mas a única versão vinculada está em
    /// rascunho. Devolver os três campos nulos faz a UI carimbar "Geral" — afirmando ao leitor
    /// público que o erro vale para todos os provedores.
    /// </summary>
    [Fact]
    public async Task ErroComTodasAsVersoesVinculadasEmRascunho_NaoPodeSerExibidoComoErroGeral()
    {
        var termo = $"probem28{NovoSufixo()}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var (provedorId, versaoId) = await CriarProvedorComVersaoEmRascunhoAsync(contexto);
            await IndexarErroComTodasAsVersoesEmRascunhoAsync(contexto, termo, versaoId, provedorId);
        }

        var itens = await BuscarItensAsync(termo);

        // A premissa original deste caso ("o item aparece") era o COMPORTAMENTO MEDIDO antes da
        // correção, e é incompatível com o caso irmão abaixo, que exige ausência no público — os
        // dois não podem valer juntos. O que este caso proíbe é o PAYLOAD que colapsa os dois
        // sentidos: se o item aparecer, não pode vir com os três campos nulos. A asserção é a
        // mesma; só a premissa deixou de exigir presença. Registrado como achado no handoff.
        var pareceErroGeral = itens.Any(
            item => item.ProvedorNome is null && item.VersaoRotulo is null && item.Versoes is null);

        pareceErroGeral.Should().BeFalse(
            because: "os três campos nulos são o sinal de 'erro geral' (U9). Um erro de origem Prefeitura "
                     + "vinculado a UMA versão, que por acaso está em rascunho, não é um erro geral — "
                     + "M28(a) manda não publicá-lo enquanto nenhuma versão vinculada for pública");
    }

    /// <summary>
    /// Leitura direta da regra M28(a) sobre a busca pública: enquanto nenhuma versão vinculada
    /// estiver publicada, o erro não deveria aparecer para o público (mas deve continuar visível
    /// na busca interna, para a redação).
    /// </summary>
    [Fact]
    public async Task ErroComTodasAsVersoesVinculadasEmRascunho_NaoApareceNaBuscaPublica_MasApareceNaInterna()
    {
        var termo = $"probem28b{NovoSufixo()}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var (provedorId, versaoId) = await CriarProvedorComVersaoEmRascunhoAsync(contexto);
            await IndexarErroComTodasAsVersoesEmRascunhoAsync(contexto, termo, versaoId, provedorId);
        }

        (await BuscarItensAsync(termo, incluirRascunho: true)).Should().ContainSingle(
            because: "a redação precisa enxergar o erro para saber que ele existe (M28, segunda parte)");

        (await BuscarItensAsync(termo)).Should().BeEmpty(
            because: "M28(a): erro COM vínculos só é público se ao menos uma versão vinculada for pública");
    }
}
