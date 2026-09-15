using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Validators;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// §9/risco #2 do DBA — linha de defesa de FORMA (obrigatório/proibido conforme <c>estado</c>) e
/// a comparação "ordem do destino &gt; ordem de origem" (via repositórios mockados, mesma
/// invariante coberta em <c>Dominio/CampoLayoutTests.Descontinuar_*</c>).
/// </summary>
public sealed class AtualizarCampoLayoutDtoValidatorTests
{
    private readonly Mock<ICampoLayoutRepositorioInterno> _campoLayoutRepositorio = new();
    private readonly Mock<IVersaoLayoutRepositorioInterno> _versaoLayoutRepositorio = new();
    private readonly Mock<IContextoRequisicaoHttp> _contextoRequisicao = new();

    private AtualizarCampoLayoutDtoValidator CriarSut() =>
        new(_campoLayoutRepositorio.Object, _versaoLayoutRepositorio.Object, _contextoRequisicao.Object);

    private static CampoLayout CriarCampoComOrigem(int ordemOrigem)
    {
        var origem = VersaoLayout.Criar($"Origem-{Guid.NewGuid():N}", ordemOrigem);
        var campo = CampoLayout.Criar(origem.Id, "Número", $"tomador/numero-{Guid.NewGuid():N}", "sig", 1);

        typeof(CampoLayout).GetProperty(nameof(CampoLayout.VersaoLayoutOrigem))!.SetValue(campo, origem);

        return campo;
    }

    [Fact]
    public async Task Validate_EstadoDescontinuado_SemVersaoLayoutDescontinuadoId_RetornaErro()
    {
        var dto = new AtualizarCampoLayoutDto { Estado = EstadoCampoLayout.Descontinuado, VersaoLayoutDescontinuadoId = null };

        var resultado = await CriarSut().ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarCampoLayoutDto.VersaoLayoutDescontinuadoId));
    }

    [Fact]
    public async Task Validate_EstadoAtivo_ComVersaoLayoutDescontinuadoIdPreenchido_RetornaErro()
    {
        var dto = new AtualizarCampoLayoutDto { Estado = EstadoCampoLayout.Ativo, VersaoLayoutDescontinuadoId = 5 };

        var resultado = await CriarSut().ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarCampoLayoutDto.VersaoLayoutDescontinuadoId));
    }

    [Fact]
    public async Task Validate_SemEstadoInformado_SemVersaoLayoutDescontinuadoId_EhValido()
    {
        var dto = new AtualizarCampoLayoutDto { Nome = "Novo nome" };

        var resultado = await CriarSut().ValidateAsync(dto);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EstadoDescontinuado_ComOrdemDoDestinoMenorOuIgualAOrigem_RetornaErro()
    {
        var campo = CriarCampoComOrigem(ordemOrigem: 5);
        var versaoDestino = VersaoLayout.Criar($"Destino-{Guid.NewGuid():N}", 5); // igual, não maior

        _contextoRequisicao.Setup(c => c.ObterIdDaRota(It.IsAny<string>())).Returns(1);
        _campoLayoutRepositorio.Setup(r => r.ObterPorIdAsync(1, false, true, It.IsAny<CancellationToken>())).ReturnsAsync(campo);
        _versaoLayoutRepositorio.Setup(r => r.ObterPorIdAsync(It.IsAny<int>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(versaoDestino);

        var dto = new AtualizarCampoLayoutDto { Estado = EstadoCampoLayout.Descontinuado, VersaoLayoutDescontinuadoId = versaoDestino.Id };

        var resultado = await CriarSut().ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarCampoLayoutDto.VersaoLayoutDescontinuadoId));
    }

    [Fact]
    public async Task Validate_EstadoDescontinuado_ComOrdemDoDestinoMaiorQueOrigem_EhValido()
    {
        var campo = CriarCampoComOrigem(ordemOrigem: 5);
        var versaoDestino = VersaoLayout.Criar($"Destino-{Guid.NewGuid():N}", 6); // maior

        _contextoRequisicao.Setup(c => c.ObterIdDaRota(It.IsAny<string>())).Returns(1);
        _campoLayoutRepositorio.Setup(r => r.ObterPorIdAsync(1, false, true, It.IsAny<CancellationToken>())).ReturnsAsync(campo);
        _versaoLayoutRepositorio.Setup(r => r.ObterPorIdAsync(It.IsAny<int>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(versaoDestino);

        var dto = new AtualizarCampoLayoutDto { Estado = EstadoCampoLayout.Descontinuado, VersaoLayoutDescontinuadoId = versaoDestino.Id };

        var resultado = await CriarSut().ValidateAsync(dto);

        resultado.IsValid.Should().BeTrue();
    }
}
