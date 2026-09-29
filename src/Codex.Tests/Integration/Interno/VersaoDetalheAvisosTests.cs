using Codex.Application.Services.Interno;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// O detalhe interno de versão precisa trazer os avisos daquela versão.
///
/// <para>
/// O cliente já os lia — `useVersaoAdminPorId` faz `versao.avisos.map(...)` dentro do `queryFn` —
/// e o DTO não os tinha. O efeito não foi um campo vazio: o `map` sobre `undefined` lançava
/// **dentro da query**, então a requisição voltava **200** e a tela inteira caía no estado de erro
/// ("Não foi possível carregar esta informação agora"). A tela mais densa da área interna —
/// campos, avisos e exemplos de uma versão — nunca abria, sem um único erro no console do
/// servidor nem no do navegador.
/// </para>
///
/// <para>
/// Mesma classe do campo `versoes` ausente na listagem pública: contrato que o cliente assumiu, o
/// servidor nunca cumpriu, e nenhuma das duas suítes podia perceber sozinha. Por isso o caso vive
/// aqui, contra Postgres real.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class VersaoDetalheAvisosTests
{
    private readonly PostgresContainerFixture _fixture;

    public VersaoDetalheAvisosTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ObterDetalheAsync_TrazOsAvisosDaVersao_PublicadosERascunho()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var (versaoId, _) = await SemearVersaoComAvisosAsync(contextoSetup, sufixo);

        await using var contexto = _fixture.CriarContexto();
        var servico = CriarServico(contexto);

        var detalhe = await servico.ObterDetalheAsync(versaoId, CancellationToken.None);

        // A área interna enxerga rascunho — diferente da superfície pública, que só vê publicado.
        detalhe.Avisos.Select(a => a.Titulo).Should().BeEquivalentTo(
            [$"Aviso publicado {sufixo}", $"Aviso rascunho {sufixo}"]);
    }

    /// <summary>
    /// Aviso de OUTRA versão e aviso de escopo PROVEDOR não pertencem a este agregado — o primeiro
    /// é de outra tela, o segundo aparece na página do provedor.
    /// </summary>
    [Fact]
    public async Task ObterDetalheAsync_NaoTrazAvisoDeOutroEscopo()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var (versaoId, provedorId) = await SemearVersaoComAvisosAsync(contextoSetup, sufixo);

        var avisoDoProvedor = Aviso.CriarDeProvedor(
            provedorId, Severidade.Informativo, $"Aviso do provedor {sufixo}", "corpo");
        contextoSetup.Avisos.Add(avisoDoProvedor);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var servico = CriarServico(contexto);

        var detalhe = await servico.ObterDetalheAsync(versaoId, CancellationToken.None);

        detalhe.Avisos.Should().NotContain(a => a.Titulo == $"Aviso do provedor {sufixo}");
    }

    private static VersaoProvedorServicoInterno CriarServico(
        Codex.Infrastructure.Data.CodexDbContext contexto) =>
        new(
            new VersaoProvedorRepositorioInterno(contexto),
            new ProvedorRepositorioInterno(contexto),
            new AvisoRepositorioInterno(contexto),
            new Codex.Infrastructure.Busca.IndiceBuscaWriter(contexto),
            new UnidadeDeTrabalhoInterna(contexto));

    private static async Task<(int VersaoId, int ProvedorId)> SemearVersaoComAvisosAsync(
        Codex.Infrastructure.Data.CodexDbContext contexto,
        string sufixo)
    {
        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout avisos {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor avisos {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"9.{sufixo[..2]}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var publicado = Aviso.CriarDeVersao(
            versao.Id, Severidade.Alerta, $"Aviso publicado {sufixo}", "corpo do publicado");
        publicado.Publicar();
        var rascunho = Aviso.CriarDeVersao(
            versao.Id, Severidade.Informativo, $"Aviso rascunho {sufixo}", "corpo do rascunho");

        contexto.Avisos.AddRange(publicado, rascunho);
        await contexto.SaveChangesAsync();

        return (versao.Id, provedor.Id);
    }
}
