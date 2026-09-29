using Codex.Application.DTOs.Interno;
using Codex.Application.Services.Interno;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Todo <c>CampoVersaoAdminDto</c> carrega o NOME do campo canônico, não só o id.
///
/// <para>
/// A tabela de campos da versão exibe esse nome na coluna "Campo" e o usa no rótulo acessível de
/// cada ação. Sem ele a coluna ficava vazia nas oito linhas e os dezesseis botões anunciavam
/// "Editar campo undefined" / "Remover campo undefined" — com HTTP 200 e sem erro no console.
/// </para>
///
/// <para>
/// Terceira ocorrência da mesma classe (campo que o cliente lê e o servidor nunca enviou), depois
/// de <c>versoes</c> na listagem pública e <c>avisos</c> no detalhe de versão. Esta é a que mente
/// em silêncio em vez de quebrar, então o detector precisa afirmar o VALOR do nome — conferir que
/// a propriedade existe é o que o compilador já faz.
/// </para>
///
/// <para>
/// Os três caminhos que devolvem o DTO entram no caso: listar, criar e atualizar. Criar e
/// atualizar remontavam o DTO à mão no service e por isso divergiam da listagem.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CampoVersaoNomeCanonicoTests
{
    private readonly PostgresContainerFixture _fixture;

    public CampoVersaoNomeCanonicoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListarAgregadoAsync_TrazONomeDoCampoCanonico()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var cenario = await SemearAsync(contextoSetup, sufixo);

        contextoSetup.CamposVersao.Add(CampoVersao.Criar(
            cenario.VersaoProvedorId, cenario.CampoLayoutId, "string", EstadoPreenchimento.Obrigatorio, 1));
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var agregado = await CriarServico(contexto).ListarAgregadoAsync(cenario.VersaoProvedorId, CancellationToken.None);

        agregado.Campos.Select(c => c.CampoLayoutNome).Should().Equal(cenario.CampoLayoutNome);
    }

    [Fact]
    public async Task CriarAsync_DevolveONomeDoCampoCanonico()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var cenario = await SemearAsync(contextoSetup, sufixo);

        await using var contexto = _fixture.CriarContexto();
        var criado = await CriarServico(contexto).CriarAsync(
            cenario.VersaoProvedorId,
            new CriarCampoVersaoDto
            {
                CampoLayoutId = cenario.CampoLayoutId,
                Tipo = "string",
                EstadoPreenchimento = EstadoPreenchimento.Obrigatorio,
                Ordem = 1,
            },
            CancellationToken.None);

        criado.CampoLayoutNome.Should().Be(cenario.CampoLayoutNome);
    }

    [Fact]
    public async Task AtualizarAsync_DevolveONomeDoCampoCanonico()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var cenario = await SemearAsync(contextoSetup, sufixo);

        var campo = CampoVersao.Criar(
            cenario.VersaoProvedorId, cenario.CampoLayoutId, "string", EstadoPreenchimento.Obrigatorio, 1);
        contextoSetup.CamposVersao.Add(campo);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var atualizado = await CriarServico(contexto).AtualizarAsync(
            campo.Id,
            new AtualizarCampoVersaoDto
            {
                Tipo = "numero",
                EstadoPreenchimento = EstadoPreenchimento.Opcional,
                Ordem = 2,
            },
            campo.Versao,
            CancellationToken.None);

        atualizado.CampoLayoutNome.Should().Be(cenario.CampoLayoutNome);
    }

    private static CampoVersaoServicoInterno CriarServico(Codex.Infrastructure.Data.CodexDbContext contexto) =>
        new(new CampoVersaoRepositorioInterno(contexto), new VersaoProvedorRepositorioInterno(contexto));

    private static async Task<Cenario> SemearAsync(
        Codex.Infrastructure.Data.CodexDbContext contexto, string sufixo)
    {
        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout nome canonico {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var nomeCanonico = $"Inscricao Municipal {sufixo}";
        var campoLayout = CampoLayout.Criar(
            versaoLayout.Id, nomeCanonico, $"caminho/nome/{sufixo}", "significado", 1);
        contexto.CamposLayout.Add(campoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor nome canonico {sufixo}", $"NOM-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"7.{sufixo[..2]}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return new Cenario(versao.Id, campoLayout.Id, nomeCanonico);
    }

    private sealed record Cenario(int VersaoProvedorId, int CampoLayoutId, string CampoLayoutNome);
}
