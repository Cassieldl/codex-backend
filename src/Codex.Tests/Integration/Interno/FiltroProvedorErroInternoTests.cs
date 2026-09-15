using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Pertinência erro↔provedor na área INTERNA (I18, filtro <c>?provedor=</c>).
///
/// <para>
/// O par com a superfície pública é assimétrico de propósito: aqui rascunho CONTA (é a razão de
/// existir da tela interna), mas vínculo por versão ou provedor REMOVIDO não conta — mesmo
/// predicado da busca interna. Sem esse recorte, o mesmo join que vazava pertinência no público
/// (QA-05) continuava recortando erro sob provedor por causa de um vínculo apagado.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class FiltroProvedorErroInternoTests
{
    private readonly PostgresContainerFixture _fixture;

    public FiltroProvedorErroInternoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private sealed record Cenario(string ProvedorSlug, int VersaoId, string ErroTitulo);

    private async Task<Cenario> MontarAsync(bool versaoRemovida)
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout intfil {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        // Provedor e versão deliberadamente em RASCUNHO: a área interna tem de vê-los.
        var provedor = Provedor.Criar($"Provedor intfil {sufixo}", $"INTF-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao intfil {sufixo}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var erro = Erro.Criar($"INTF-{sufixo}", OrigemErro.Prefeitura, $"Erro intfil {sufixo}");
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versao.Id));
        await contexto.SaveChangesAsync();

        if (versaoRemovida)
        {
            versao.Desativar();
            await contexto.SaveChangesAsync();
        }

        return new Cenario(provedor.Slug, versao.Id, erro.Titulo);
    }

    private async Task<IReadOnlyCollection<ErroAdminListaItemDto>> ListarAsync(string provedorSlug)
    {
        await using var contexto = _fixture.CriarContexto();
        var repositorio = new ErroRepositorioInterno(contexto);

        var pagina = await repositorio.ListarAsync(
            new FiltroErroInterno(null, provedorSlug, null, null, 1, 100), CancellationToken.None);

        return [.. pagina.Itens];
    }

    [Fact]
    public async Task ErroEmRascunhoLigadoAVersaoEmRascunho_ApareceSobOFiltroInterno()
    {
        var cenario = await MontarAsync(versaoRemovida: false);

        (await ListarAsync(cenario.ProvedorSlug)).Should().ContainSingle()
            .Which.Titulo.Should().Be(cenario.ErroTitulo,
                because: "a tela interna existe para trabalhar o que ainda não foi publicado");
    }

    [Fact]
    public async Task ErroLigadoSoAVersaoRemovida_NaoApareceSobOFiltroInterno()
    {
        var cenario = await MontarAsync(versaoRemovida: true);

        (await ListarAsync(cenario.ProvedorSlug)).Should().BeEmpty(
            because: "soft delete apaga o vínculo — 'rascunho conta' não é o mesmo que 'removido conta'");
    }
}
