using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using FluentAssertions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// A listagem pública de provedores precisa trazer as versões publicadas de cada provedor.
///
/// <para>
/// O frontend consome esse campo em dois lugares — os chips de versão da lista e o seletor
/// "ir direto a uma versão" da home — e o DTO simplesmente não o tinha. O tipo do cliente o
/// declarava mesmo assim (com o comentário "assumido, pendente de confirmação"), e os mocks de
/// teste, escritos contra esse tipo, sempre entregaram o campo: suíte verde nos dois lados, e na
/// tela real um <c>TypeError</c> ao ler <c>versoes.slice</c> derrubando a rota inteira, mais o
/// seletor da home mudo porque lá o acesso era opcional.
/// </para>
///
/// <para>
/// Por isso o contrato se prova aqui, contra o Postgres real: é o único lado que não pode
/// concordar com a suposição do outro.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ProvedorListaVersoesTests
{
    private readonly PostgresContainerFixture _fixture;

    public ProvedorListaVersoesTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListarAsync_TrazVersoesPublicadas_MaisRecentePrimeiro()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var provedor = await SemearProvedorComVersoesAsync(contextoSetup, sufixo);

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ProvedorRepositorioPublico(contexto);

        var pagina = await repositorio.ListarAsync(
            new FiltroProvedorPublico(sufixo, null, null, null, 1, 25),
            CancellationToken.None);

        var item = pagina.Itens.Should().ContainSingle(p => p.Slug == provedor.Slug).Subject;

        // Três rótulos, e a ordem esperada não coincide nem com a de inserção nem com a inversa:
        // com dois, metade das ordenações erradas passaria por acidente.
        item.Versoes.Select(v => v.Rotulo).Should().Equal(
            $"3.00 {sufixo}",
            $"1.00 {sufixo}",
            $"2.00 {sufixo}");
    }

    [Fact]
    public async Task ListarAsync_NuncaExpoeVersaoDespublicada()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var provedor = await SemearProvedorComVersoesAsync(contextoSetup, sufixo, comRascunho: true);

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ProvedorRepositorioPublico(contexto);

        var pagina = await repositorio.ListarAsync(
            new FiltroProvedorPublico(sufixo, null, null, null, 1, 25),
            CancellationToken.None);

        var item = pagina.Itens.Should().ContainSingle(p => p.Slug == provedor.Slug).Subject;

        item.Versoes.Should().NotContain(v => v.Rotulo.StartsWith("9.99"),
            because: "versão em rascunho nunca aparece em superfície pública, nem embutida na listagem");
    }

    /// <summary>
    /// As três versões nascem com <c>CriadoEm</c> explicitamente distinto e fora da ordem de
    /// inserção — semear em sequência daria o mesmo instante para todas e a asserção de ordem
    /// passaria pela ordem física da tabela, sem provar a ordenação.
    /// </summary>
    private static async Task<Provedor> SemearProvedorComVersoesAsync(
        Codex.Infrastructure.Data.CodexDbContext contexto,
        string sufixo,
        bool comRascunho = false)
    {
        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout lista {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor Lista {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var agora = DateTimeOffset.UtcNow;
        var criadasEm = new Dictionary<string, DateTimeOffset>
        {
            [$"1.00 {sufixo}"] = agora.AddDays(-2),
            [$"2.00 {sufixo}"] = agora.AddDays(-3),
            [$"3.00 {sufixo}"] = agora.AddDays(-1),
        };

        foreach (var (rotulo, criadoEm) in criadasEm)
        {
            var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, rotulo);
            versao.Publicar();
            contexto.VersoesProvedor.Add(versao);
            await contexto.SaveChangesAsync();

            contexto.Entry(versao).Property(v => v.CriadoEm).CurrentValue = criadoEm;
            await contexto.SaveChangesAsync();
        }

        if (comRascunho)
        {
            contexto.VersoesProvedor.Add(VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"9.99 {sufixo}"));
            await contexto.SaveChangesAsync();
        }

        return provedor;
    }
}
