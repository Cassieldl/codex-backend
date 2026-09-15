using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using Codex.Tests.Integration;
using FluentAssertions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Prova, contra Postgres real, que a leitura pública de erro nunca vaza conteúdo não publicado
/// — inclusive a cascata de "versão aplicável" (U9: erro pode não ter nenhuma versão pública
/// aplicável, o que é válido, nunca um vazamento). Escrito por BACK-3; NÃO executável nesta
/// máquina (Docker indisponível) — ver handoff.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ErroRepositorioPublicoTests
{
    private readonly PostgresContainerFixture _fixture;

    public ErroRepositorioPublicoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListarAsync_NuncaRetornaErroDespublicado()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var publicado = Erro.Criar($"ERR-PUB-{sufixo}", OrigemErro.Schema, $"Erro publicado {sufixo}");
        publicado.Publicar();
        var rascunho = Erro.Criar($"ERR-RAS-{sufixo}", OrigemErro.Schema, $"Erro rascunho {sufixo}");
        contextoSetup.Erros.AddRange(publicado, rascunho);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ErroRepositorioPublico(contexto);

        var pagina = await repositorio.ListarAsync(new FiltroErroPublico(null, null, null, 1, 25), CancellationToken.None);

        pagina.Itens.Should().Contain(e => e.Slug == publicado.Slug);
        pagina.Itens.Should().NotContain(e => e.Slug == rascunho.Slug,
            because: "erro despublicado nunca pode aparecer em listagem pública (M19/U1)");
    }

    [Fact]
    public async Task ListarVersoesAplicaveisPublicadasAsync_ExcluiVersaoCujoProvedorEstaDespublicado()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var maiorOrdem = contextoSetup.VersoesLayout.Any() ? contextoSetup.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout {sufixo}", maiorOrdem + 1);
        contextoSetup.VersoesLayout.Add(versaoLayout);
        await contextoSetup.SaveChangesAsync();

        var provedorPublicado = Provedor.Criar($"Publicado {sufixo}", $"INV-PUB-{sufixo}", CategoriaProvedor.Nacional);
        provedorPublicado.Publicar();
        var provedorDespublicado = Provedor.Criar($"Despublicado {sufixo}", $"INV-DES-{sufixo}", CategoriaProvedor.Nacional);
        contextoSetup.Provedores.AddRange(provedorPublicado, provedorDespublicado);
        await contextoSetup.SaveChangesAsync();

        var versaoValida = VersaoProvedor.Criar(provedorPublicado.Id, versaoLayout.Id, "v1");
        versaoValida.Publicar();
        var versaoComProvedorDespublicado = VersaoProvedor.Criar(provedorDespublicado.Id, versaoLayout.Id, "v1");
        versaoComProvedorDespublicado.Publicar(); // a própria versão está publicada — só o pai não está
        contextoSetup.VersoesProvedor.AddRange(versaoValida, versaoComProvedorDespublicado);
        await contextoSetup.SaveChangesAsync();

        var erro = Erro.Criar($"ERR-{sufixo}", OrigemErro.Prefeitura, $"Erro {sufixo}");
        erro.Publicar();
        contextoSetup.Erros.Add(erro);
        await contextoSetup.SaveChangesAsync();

        contextoSetup.ErrosVersoes.AddRange(
            ErroVersao.Criar(erro.Id, versaoValida.Id),
            ErroVersao.Criar(erro.Id, versaoComProvedorDespublicado.Id));
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ErroRepositorioPublico(contexto);

        var aplicaveis = await repositorio.ListarVersoesAplicaveisPublicadasAsync(erro.Id, CancellationToken.None);

        aplicaveis.Should().ContainSingle(v => v.VersaoProvedorId == versaoValida.Id);
        aplicaveis.Should().NotContain(v => v.VersaoProvedorId == versaoComProvedorDespublicado.Id,
            because: "versão de provedor despublicado nunca é aplicável publicamente, mesmo ela mesma estando marcada como publicada (cascata M19)");
    }

    [Fact]
    public async Task ListarVersoesAplicaveisPublicadasAsync_SemNenhumaAplicavel_RetornaVazioSemLancar()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var erro = Erro.Criar($"ERR-{sufixo}", OrigemErro.InvoiCy, $"Erro geral {sufixo}");
        erro.Publicar();
        contextoSetup.Erros.Add(erro);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ErroRepositorioPublico(contexto);

        var aplicaveis = await repositorio.ListarVersoesAplicaveisPublicadasAsync(erro.Id, CancellationToken.None);

        aplicaveis.Should().BeEmpty(because: "U9 — erro sem nenhuma versão aplicável pública é um resultado válido");
    }

    [Fact]
    public async Task ListarCamposRelacionadosAsync_AncoraSoExisteQuandoCampoVersaoAtivoNaVersaoAplicavel()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var maiorOrdem = contextoSetup.VersoesLayout.Any() ? contextoSetup.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout {sufixo}", maiorOrdem + 1);
        contextoSetup.VersoesLayout.Add(versaoLayout);
        await contextoSetup.SaveChangesAsync();

        var campoLayout = CampoLayout.Criar(versaoLayout.Id, $"Campo {sufixo}", $"/caminho/{sufixo}", "significado", 1);
        contextoSetup.CamposLayout.Add(campoLayout);
        await contextoSetup.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contextoSetup.Provedores.Add(provedor);
        await contextoSetup.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, "v1");
        versao.Publicar();
        contextoSetup.VersoesProvedor.Add(versao);
        await contextoSetup.SaveChangesAsync();

        var erro = Erro.Criar($"ERR-{sufixo}", OrigemErro.Prefeitura, $"Erro {sufixo}");
        erro.Publicar();
        contextoSetup.Erros.Add(erro);
        await contextoSetup.SaveChangesAsync();
        contextoSetup.ErrosCampos.Add(ErroCampo.Criar(erro.Id, campoLayout.Id));
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ErroRepositorioPublico(contexto);
        var versaoAplicavel = new VersaoAplicavelBrutaDto
        {
            VersaoProvedorId = versao.Id,
            ProvedorSlug = provedor.Slug,
            ProvedorNome = provedor.Nome,
            VersaoSlug = versao.Slug,
            VersaoRotulo = versao.Rotulo,
        };

        // Cenário 1 — sem CampoVersao criado ainda: campo relacionado aparece, mas sem âncora (M10 — texto simples).
        var semAncora = await repositorio.ListarCamposRelacionadosAsync(erro.Id, [versaoAplicavel], CancellationToken.None);
        semAncora.Should().ContainSingle(c => c.Slug == campoLayout.Slug);
        semAncora.Single().Ancoras.Should().BeEmpty();

        // Cenário 2 — cria o CampoVersao correspondente: agora a âncora deve aparecer.
        await using var contextoEscrita = _fixture.CriarContexto();
        contextoEscrita.CamposVersao.Add(CampoVersao.Criar(versao.Id, campoLayout.Id, "texto", EstadoPreenchimento.Obrigatorio));
        await contextoEscrita.SaveChangesAsync();

        await using var contextoLeitura = _fixture.CriarContexto();
        var repositorioLeitura = new ErroRepositorioPublico(contextoLeitura);
        var comAncora = await repositorioLeitura.ListarCamposRelacionadosAsync(erro.Id, [versaoAplicavel], CancellationToken.None);

        comAncora.Single().Ancoras.Should().ContainSingle(
            a => a.ProvedorSlug == provedor.Slug && a.VersaoSlug == versao.Slug);
    }
}
