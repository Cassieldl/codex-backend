using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// Validação de comunicado (I32/I33, analise-backend.md §9). O relógio é fixo: a regra "expiração
/// no futuro" comparada contra <c>DateTimeOffset.UtcNow</c> real só seria testável com margens
/// arbitrárias, que é como nascem os testes intermitentes.
/// </summary>
public sealed class ComunicadoValidatorsTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly CriarComunicadoDtoValidator _criar = new(new RelogioFixo(Agora));
    private readonly AtualizarComunicadoDtoValidator _atualizar = new(new RelogioFixo(Agora));

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Corpo_RespeitaAFronteiraDeDoisMilCaracteres(int tamanho, bool deveriaPassar)
    {
        var resultado = _criar.Validate(CriarDto(corpo: new string('a', tamanho)));

        resultado.IsValid.Should().Be(deveriaPassar,
            because: "decisoes.md B-6 fixou 2000 — a fronteira precisa ser provada dos DOIS lados, senão ninguém percebe o off-by-one");
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Corpo_TemAMesmaFronteiraNaAtualizacao(int tamanho, bool deveriaPassar)
    {
        var resultado = _atualizar.Validate(AtualizarDto(corpo: new string('a', tamanho)));

        resultado.IsValid.Should().Be(deveriaPassar,
            because: "limite que vale só na criação deixa o comunicado crescer pela edição");
    }

    [Fact]
    public void Corpo_Vazio_Reprova()
    {
        _criar.Validate(CriarDto(corpo: "   ")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Titulo_AcimaDeDuzentosCaracteres_Reprova()
    {
        _criar.Validate(CriarDto(titulo: new string('t', 201))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ExpiraEm_NoPassadoDaReferencia_Reprova()
    {
        _criar.Validate(CriarDto(expiraEm: Agora.AddSeconds(-1))).IsValid.Should().BeFalse(
            because: "comunicado que nasce vencido não chega a ficar no ar em nenhum instante");
    }

    [Fact]
    public void ExpiraEm_ExatamenteAgora_Reprova()
    {
        _criar.Validate(CriarDto(expiraEm: Agora)).IsValid.Should().BeFalse(
            because: "'expirado' é expiraEm <= agora — a fronteira do validator tem de casar com a do predicado de leitura");
    }

    [Fact]
    public void ExpiraEm_NoFuturo_ContinuaValidoNaEdicaoDeUmComunicadoJaEncerrado()
    {
        _atualizar.Validate(AtualizarDto(expiraEm: Agora.AddDays(1))).IsValid.Should().BeTrue(
            because: "estender a expiração é o próximo passo que o 422 de B-4 manda o editor dar");
    }

    [Theory]
    [InlineData(AbrangenciaComunicado.Global, null, null, true)]
    [InlineData(AbrangenciaComunicado.Global, 7, null, false)]
    [InlineData(AbrangenciaComunicado.Global, null, 9, false)]
    [InlineData(AbrangenciaComunicado.Provedor, 7, null, true)]
    [InlineData(AbrangenciaComunicado.Provedor, null, null, false)]
    [InlineData(AbrangenciaComunicado.Provedor, 7, 9, false)]
    [InlineData(AbrangenciaComunicado.ProvedorVersao, 7, 9, true)]
    [InlineData(AbrangenciaComunicado.ProvedorVersao, 7, null, false)]
    [InlineData(AbrangenciaComunicado.ProvedorVersao, null, 9, false)]
    public void Escopo_ReplicaOCheckDeConsistenciaDoBanco(
        AbrangenciaComunicado abrangencia, int? provedorId, int? versaoProvedorId, bool deveriaPassar)
    {
        var resultado = _criar.Validate(CriarDto(abrangencia: abrangencia, provedorId: provedorId, versaoProvedorId: versaoProvedorId));

        resultado.IsValid.Should().Be(deveriaPassar,
            because: "sem esta camada a violação chega ao editor como 500 de CHECK, sem dizer qual campo está errado");
    }

    [Fact]
    public void Escopo_ComIdInvalido_Reprova()
    {
        _criar.Validate(CriarDto(abrangencia: AbrangenciaComunicado.Provedor, provedorId: 0)).IsValid.Should().BeFalse();
    }

    private static CriarComunicadoDto CriarDto(
        string? titulo = null,
        string? corpo = null,
        DateTimeOffset? expiraEm = null,
        AbrangenciaComunicado abrangencia = AbrangenciaComunicado.Global,
        int? provedorId = null,
        int? versaoProvedorId = null) => new()
        {
            Titulo = titulo ?? "Instabilidade na emissão",
            Corpo = corpo ?? "Emissão intermitente desde as 9h.",
            Severidade = Severidade.Critico,
            Abrangencia = abrangencia,
            ProvedorId = provedorId,
            VersaoProvedorId = versaoProvedorId,
            ExpiraEm = expiraEm ?? Agora.AddDays(7),
        };

    private static AtualizarComunicadoDto AtualizarDto(
        string? corpo = null,
        DateTimeOffset? expiraEm = null) => new()
        {
            Titulo = "Instabilidade na emissão",
            Corpo = corpo ?? "Emissão intermitente desde as 9h.",
            Severidade = Severidade.Critico,
            Abrangencia = AbrangenciaComunicado.Global,
            ProvedorId = null,
            VersaoProvedorId = null,
            ExpiraEm = expiraEm ?? Agora.AddDays(7),
        };

    /// <summary>Relógio parado no instante do teste — evita depender do pacote de testing do TimeProvider por três linhas de código.</summary>
    private sealed class RelogioFixo : TimeProvider
    {
        private readonly DateTimeOffset _agora;

        public RelogioFixo(DateTimeOffset agora)
        {
            _agora = agora;
        }

        public override DateTimeOffset GetUtcNow() => _agora;
    }
}
