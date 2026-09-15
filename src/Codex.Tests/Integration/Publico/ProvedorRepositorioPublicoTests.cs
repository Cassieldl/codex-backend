using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using Codex.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Prova, contra Postgres real (nunca InMemory — AP-BACKEND-005), que a leitura pública de
/// provedor nunca vaza conteúdo não publicado — inclusive nos casos de cascata (AP-SECURITY-003:
/// declarado ≠ provado). Escrito pela unidade BACK-3; NÃO executável nesta máquina (Docker
/// indisponível, 500 persistente em dockerDesktopLinuxEngine) — ver handoff.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ProvedorRepositorioPublicoTests
{
    private readonly PostgresContainerFixture _fixture;

    public ProvedorRepositorioPublicoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListarAsync_NuncaRetornaProvedorDespublicado()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var publicado = Provedor.Criar($"Publicado {sufixo}", $"INV-PUB-{sufixo}", CategoriaProvedor.Nacional);
        publicado.Publicar();
        var rascunho = Provedor.Criar($"Rascunho {sufixo}", $"INV-RAS-{sufixo}", CategoriaProvedor.Nacional);
        contextoSetup.Provedores.AddRange(publicado, rascunho);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ProvedorRepositorioPublico(contexto);

        var pagina = await repositorio.ListarAsync(
            new Codex.Application.DTOs.Publico.FiltroProvedorPublico(sufixo, null, null, null, 1, 25),
            CancellationToken.None);

        pagina.Itens.Should().ContainSingle(p => p.Slug == publicado.Slug);
        pagina.Itens.Should().NotContain(p => p.Slug == rascunho.Slug,
            because: "provedor despublicado nunca pode aparecer em listagem pública (M19/U1)");
    }

    [Fact]
    public async Task ObterPorSlugAsync_RetornaProvedorDespublicado_ParaOServiceDistinguir410De404()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var rascunho = Provedor.Criar($"Rascunho {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        contextoSetup.Provedores.Add(rascunho);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ProvedorRepositorioPublico(contexto);

        var encontrado = await repositorio.ObterPorSlugAsync(rascunho.Slug, CancellationToken.None);

        encontrado.Should().NotBeNull(because: "M7 exige distinguir 404 de 410 — o repositório devolve a entidade, o service decide o status");
        encontrado!.Publicado.Should().BeFalse();
    }

    [Fact]
    public async Task ObterPorSlugAsync_QuandoDesativado_NaoRetorna()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var removido = Provedor.Criar($"Removido {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        removido.Publicar();
        removido.Desativar();
        contextoSetup.Provedores.Add(removido);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ProvedorRepositorioPublico(contexto);

        var encontrado = await repositorio.ObterPorSlugAsync(removido.Slug, CancellationToken.None);

        encontrado.Should().BeNull(because: "soft-deleted é 404 mudo, nunca 410 — diferente de despublicado");
    }

    [Fact]
    public async Task ListarAvisosPublicadosDoProvedorAsync_NuncaRetornaAvisoDespublicado()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var provedor = Provedor.Criar($"Provedor {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contextoSetup.Provedores.Add(provedor);
        await contextoSetup.SaveChangesAsync();

        var avisoPublicado = Aviso.CriarDeProvedor(provedor.Id, Severidade.Alerta, "Publicado", "corpo");
        avisoPublicado.Publicar();
        var avisoRascunho = Aviso.CriarDeProvedor(provedor.Id, Severidade.Alerta, "Rascunho", "corpo");
        contextoSetup.Avisos.AddRange(avisoPublicado, avisoRascunho);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ProvedorRepositorioPublico(contexto);

        var avisos = await repositorio.ListarAvisosPublicadosDoProvedorAsync(provedor.Id, CancellationToken.None);

        avisos.Should().ContainSingle(a => a.Titulo == "Publicado");
        avisos.Should().NotContain(a => a.Titulo == "Rascunho");
    }

    [Fact]
    public async Task VersaoProvedorRepositorioPublico_ObterPorSlugsAsync_CascataProvedorDespublicadoNaoEscondeAVersaoDoJoin()
    {
        // A query nunca filtra Publicado (repositório) — quem barra é o service. Este teste
        // prova o join em si: provedor+versão existem e são retornados juntos, mesmo quando o
        // provedor está despublicado, para o service ter os dados necessários para decidir 410.
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var maiorOrdem = contextoSetup.VersoesLayout.Any() ? contextoSetup.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout {sufixo}", maiorOrdem + 1);
        contextoSetup.VersoesLayout.Add(versaoLayout);
        await contextoSetup.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        // Nunca chama Publicar() — provedor despublicado.
        contextoSetup.Provedores.Add(provedor);
        await contextoSetup.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, "v1");
        versao.Publicar(); // versão publicada, mas o pai (provedor) não.
        contextoSetup.VersoesProvedor.Add(versao);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new VersaoProvedorRepositorioPublico(contexto);

        var encontrado = await repositorio.ObterPorSlugsAsync(provedor.Slug, versao.Slug, CancellationToken.None);

        encontrado.Should().NotBeNull();
        encontrado!.Provedor.Publicado.Should().BeFalse();
        encontrado.Versao.Publicado.Should().BeTrue();
        // O service (ProvedorServicoPublicoTests, unitário) é quem prova que isso vira 410
        // PROVEDOR_DESPUBLICADO — este teste prova só o join correto contra o banco real.
    }
}
