using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Moq;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// Envio modelo da versão 1.01 do ISS.NET montado pelo serviço sobre a estrutura real dos layouts 8 e
/// 9, versionada pela carga, com os campos que o teste passar.
/// </summary>
internal static class EnvioModeloComEstruturaReal
{
    public static readonly string PastaDoLayout = Path.Combine(RaizDoRepositorio(), "database", "carga", "emissor-nacional");

    public static async Task<EnvioModeloDto> ObterAsync(
        string rotuloLayout, IReadOnlyCollection<CampoEnvioModeloBrutoDto> campos, AlcanceEnvioModelo alcance, CenarioEnvioModelo cenario)
    {
        var provedor = Provedor.Criar("ISS.NET", "ISSNET", CategoriaProvedor.Nacional);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayoutId: 1, "1.01");
        versao.Publicar();

        var repositorio = new Mock<IVersaoProvedorRepositorioPublico>();
        repositorio.Setup(r => r.ObterPorSlugsAsync("iss-net", "1-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, rotuloLayout));
        repositorio.Setup(r => r.ObterEstruturaDoLayoutPublicadoAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(File.ReadAllText(Path.Combine(PastaDoLayout, $"layout-invoicy-v{rotuloLayout}-estrutura.json")));
        repositorio.Setup(r => r.ListarCamposDoEnvioModeloAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campos);

        var servico = new ProvedorServicoPublico(Mock.Of<IProvedorRepositorioPublico>(), repositorio.Object);
        return await servico.ObterEnvioModeloAsync("iss-net", "1-01", alcance, cenario, CancellationToken.None);
    }

    /// <summary>Nó pelo caminho de nomes abaixo da raiz, como <c>RPS/Tomador</c>.</summary>
    public static NoEstruturaDto No(NoEstruturaDto raiz, string caminho) =>
        caminho.Split('/').Aggregate(raiz, (no, nome) => no.Filhos.Single(filho => filho.Nome == nome));

    public static IEnumerable<NoEstruturaDto> Nos(NoEstruturaDto raiz) => new[] { raiz }.Concat(raiz.Filhos.SelectMany(Nos));

    private static string RaizDoRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Codex.slnx")))
            diretorio = diretorio.Parent;

        return diretorio?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
