using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Prova (não só declara) o risco #1 do DBA (modelo-dados.md §5.6.1, analise-backend.md §6.6):
/// a cascata de visibilidade de <c>codex.itensbusca</c> não é garantida por nenhum
/// CHECK/trigger de banco — é 100% responsabilidade de <see cref="IndiceBuscaWriter"/>. Cada
/// cenário aqui despublica/republica uma entidade REAL, chama
/// <see cref="IndiceBuscaWriter.RecalcularVisibilidadeAsync"/> e verifica o efeito através da
/// busca de ponta a ponta (<see cref="ConsultaBuscaPostgres"/>), pública e interna — nunca só
/// inspecionando a coluna <c>publicado</c> diretamente, para também exercitar o índice GIN
/// parcial correto em cada superfície.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CascataPublicacaoMultinivelTests
{
    private readonly PostgresContainerFixture _fixture;

    public CascataPublicacaoMultinivelTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static IndiceBuscaWriter CriarWriter(CodexDbContext contexto) => new(contexto);

    private sealed record Cenario(int ProvedorId, int VersaoProvedorId, int AvisoProvedorId, int AvisoVersaoId, string Termo);

    /// <summary>
    /// Monta provedor + versão + aviso de provedor + aviso de versão, todos publicados, e as
    /// quatro linhas correspondentes em <c>itensbusca</c> (já visíveis) — replica o estado que a
    /// escrita de conteúdo (fora do escopo desta unidade) deixaria após indexar tudo publicado.
    /// </summary>
    private async Task<Cenario> MontarCenarioPublicadoAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var termo = $"buscacascata{sufixo}";

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdemAtual = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout cascata {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor Cascata {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão cascata {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var avisoProvedor = Aviso.CriarDeProvedor(provedor.Id, Severidade.Informativo, "Aviso de provedor", "corpo do aviso de provedor");
        avisoProvedor.Publicar();
        var avisoVersao = Aviso.CriarDeVersao(versao.Id, Severidade.Informativo, "Aviso de versão", "corpo do aviso de versão");
        avisoVersao.Publicar();
        contexto.Avisos.AddRange(avisoProvedor, avisoVersao);
        await contexto.SaveChangesAsync();

        var itemProvedor = ItemBusca.Criar(TipoItemIndexado.provedor, provedor.Id, "Provedor Cascata", $"/provedores/{provedor.Slug}", provedorId: provedor.Id, provedorIds: [provedor.Id]);
        itemProvedor.AtualizarPesos(termo, null, null, null);
        itemProvedor.MarcarPublicado(true);

        var itemVersao = ItemBusca.Criar(TipoItemIndexado.versaoprovedor, versao.Id, "Versão Cascata", $"/provedores/{provedor.Slug}/{versao.Slug}", provedorId: provedor.Id, versaoProvedorId: versao.Id, provedorIds: [provedor.Id]);
        itemVersao.AtualizarPesos(termo, null, null, null);
        itemVersao.MarcarPublicado(true);

        var itemAvisoProvedor = ItemBusca.Criar(TipoItemIndexado.aviso, avisoProvedor.Id, "Aviso de Provedor Cascata", $"/provedores/{provedor.Slug}#avisos", provedorId: provedor.Id, provedorIds: [provedor.Id]);
        itemAvisoProvedor.AtualizarPesos(null, null, termo, null);
        itemAvisoProvedor.MarcarPublicado(true);

        var itemAvisoVersao = ItemBusca.Criar(TipoItemIndexado.aviso, avisoVersao.Id, "Aviso de Versão Cascata", $"/provedores/{provedor.Slug}/{versao.Slug}#avisos", provedorId: provedor.Id, versaoProvedorId: versao.Id, provedorIds: [provedor.Id]);
        itemAvisoVersao.AtualizarPesos(null, null, termo, null);
        itemAvisoVersao.MarcarPublicado(true);

        contexto.ItensBusca.AddRange(itemProvedor, itemVersao, itemAvisoProvedor, itemAvisoVersao);
        await contexto.SaveChangesAsync();

        return new Cenario(provedor.Id, versao.Id, avisoProvedor.Id, avisoVersao.Id, termo);
    }

    private async Task<HashSet<string>> TiposEncontradosAsync(string termo, bool incluirRascunho)
    {
        await using var contexto = _fixture.CriarContexto();
        var consulta = CriarConsulta(contexto);

        var resultado = await consulta.BuscarAsync(termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 50, incluirRascunho, CancellationToken.None);

        return resultado.Grupos.SelectMany(g => g.Itens).Select(i => i.Titulo).ToHashSet();
    }

    [Fact]
    public async Task DespublicarProvedor_RemoveProvedorVersaoEAmbosAvisos_DaBuscaPublica_MasNaoDaInterna()
    {
        var cenario = await MontarCenarioPublicadoAsync();

        // Estado inicial: tudo publicado — os quatro aparecem em ambas as superfícies.
        (await TiposEncontradosAsync(cenario.Termo, incluirRascunho: false)).Should().HaveCount(4);
        (await TiposEncontradosAsync(cenario.Termo, incluirRascunho: true)).Should().HaveCount(4);

        // Despublica SÓ o provedor, mesma transação lógica: SaveChanges do domínio + cascata do writer.
        await using (var contexto = _fixture.CriarContexto())
        {
            var provedorRastreado = await contexto.Provedores.FirstAsync(p => p.Id == cenario.ProvedorId);
            provedorRastreado.Despublicar();
            await contexto.SaveChangesAsync();

            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.Provedor, cenario.ProvedorId, CancellationToken.None);
        }

        var publicosAposDespublicar = await TiposEncontradosAsync(cenario.Termo, incluirRascunho: false);
        publicosAposDespublicar.Should().BeEmpty(
            because: "provedor, versão, aviso de provedor e aviso de versão dependem do provedor publicado — todos saem da busca pública imediatamente após o commit, sem passo assíncrono");

        var internosAposDespublicar = await TiposEncontradosAsync(cenario.Termo, incluirRascunho: true);
        internosAposDespublicar.Should().HaveCount(4, because: "a busca interna enxerga rascunho — despublicar o provedor não remove as linhas do índice, só da superfície pública");

        // Republica o provedor — os quatro voltam à busca pública sem reconstrução manual do índice.
        await using (var contexto = _fixture.CriarContexto())
        {
            var provedorRastreado = await contexto.Provedores.FirstAsync(p => p.Id == cenario.ProvedorId);
            provedorRastreado.Publicar();
            await contexto.SaveChangesAsync();

            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.Provedor, cenario.ProvedorId, CancellationToken.None);
        }

        var publicosAposRepublicar = await TiposEncontradosAsync(cenario.Termo, incluirRascunho: false);
        publicosAposRepublicar.Should().HaveCount(4, because: "republicar o provedor deve restaurar a visibilidade de toda a cadeia sem reconstrução manual do índice");
    }

    [Fact]
    public async Task DespublicarVersao_RemoveSoVersaoEAvisoDeVersao_ProvedorEAvisoDeProvedorPermanecem()
    {
        var cenario = await MontarCenarioPublicadoAsync();

        (await TiposEncontradosAsync(cenario.Termo, incluirRascunho: false)).Should().HaveCount(4);

        await using (var contexto = _fixture.CriarContexto())
        {
            var versaoRastreada = await contexto.VersoesProvedor.FirstAsync(v => v.Id == cenario.VersaoProvedorId);
            versaoRastreada.Despublicar();
            await contexto.SaveChangesAsync();

            await CriarWriter(contexto).RecalcularVisibilidadeAsync(TipoItemBusca.VersaoProvedor, cenario.VersaoProvedorId, CancellationToken.None);
        }

        var publicosAposDespublicarVersao = await TiposEncontradosAsync(cenario.Termo, incluirRascunho: false);

        publicosAposDespublicarVersao.Should().HaveCount(2,
            because: "só a versão e o aviso de versão dependem da versão publicada — provedor e aviso de provedor continuam publicados e visíveis");
        publicosAposDespublicarVersao.Should().Contain("Provedor Cascata");
        publicosAposDespublicarVersao.Should().Contain("Aviso de Provedor Cascata");
        publicosAposDespublicarVersao.Should().NotContain("Versão Cascata");
        publicosAposDespublicarVersao.Should().NotContain("Aviso de Versão Cascata");
    }
}
