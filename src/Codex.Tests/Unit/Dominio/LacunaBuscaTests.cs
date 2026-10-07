using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

public sealed class LacunaBuscaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registrar_ApararOTextoEGuardarOEstadoAberta()
    {
        var lacuna = LacunaBusca.Registrar("  Alíquota ISS  ", "  Parceiro X ", " CH-123 ", "sub-1", " Maria ");

        lacuna.Termo.Should().Be("Alíquota ISS");
        lacuna.Parceiro.Should().Be("Parceiro X");
        lacuna.NumeroChamado.Should().Be("CH-123");
        lacuna.RegistradoPorId.Should().Be("sub-1");
        lacuna.RegistradoPorNome.Should().Be("Maria");
        lacuna.Estado.Should().Be(EstadoLacunaBusca.Aberta);
        lacuna.ResolvidaEm.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_ParceiroEChamadoVazios_ViramNulo(string? vazio)
    {
        var lacuna = LacunaBusca.Registrar("termo", vazio, vazio, "sub-1", vazio);

        lacuna.Parceiro.Should().BeNull();
        lacuna.NumeroChamado.Should().BeNull();
        lacuna.RegistradoPorNome.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public void Registrar_TermoAbaixoDoMinimo_Lanca(string termo)
    {
        var acao = () => LacunaBusca.Registrar(termo, null, null, "sub-1", null);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Registrar_TermoNoTeto_Passa_ENoTetoMaisUm_Lanca()
    {
        LacunaBusca.Registrar(new string('a', 300), null, null, "sub-1", null).Termo.Should().HaveLength(300);

        var acao = () => LacunaBusca.Registrar(new string('a', 301), null, null, "sub-1", null);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Registrar_SemIdentificadorDeUsuario_Lanca(string? usuarioId)
    {
        var acao = () => LacunaBusca.Registrar("termo", null, null, usuarioId!, "Maria");

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Registrar_NomeAcimaDoTeto_ETruncadoNuncaRecusado()
    {
        var lacuna = LacunaBusca.Registrar("termo", null, null, "sub-1", new string('n', 400));

        lacuna.RegistradoPorNome.Should().HaveLength(LimitesLacunaBusca.UsuarioNome);
    }

    [Fact]
    public void Registrar_NomeComEmojiNaFronteiraDoCorte_NaoDeixaSurrogateSolto()
    {
        // O emoji ocupa duas unidades UTF-16 e começa exatamente na última posição que caberia.
        var nome = new string('n', LimitesLacunaBusca.UsuarioNome - 1) + "😀";

        var lacuna = LacunaBusca.Registrar("termo", null, null, "sub-1", nome);

        var codificador = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true);
        var bytes = () => codificador.GetBytes(lacuna.RegistradoPorNome!);
        bytes.Should().NotThrow(because: "o Npgsql grava UTF-8 estrito e recusa um par substituto partido");
        lacuna.RegistradoPorNome!.Length.Should().BeLessThanOrEqualTo(LimitesLacunaBusca.UsuarioNome);
    }

    [Theory]
    [InlineData("Alíquota ISS", "aliquota iss")]
    [InlineData("  ALÍQUOTA    iss ", "aliquota iss")]
    [InlineData("aliquota\tiss", "aliquota iss")]
    [InlineData("Inscrição Municipal", "inscricao municipal")]
    [InlineData("E-1023", "e-1023")]
    public void NormalizarTermo_RemoveAcentoCaixaEEspacosRepetidos_MantendoAPontuacao(string termo, string esperado)
    {
        LacunaBusca.NormalizarTermo(termo).Should().Be(esperado);
    }

    [Fact]
    public void NormalizarTermo_NaoFundeCodigoComEHifenComCodigoColado()
    {
        LacunaBusca.NormalizarTermo("E-1023").Should().NotBe(LacunaBusca.NormalizarTermo("e1023"));
    }

    [Fact]
    public void Resolver_GravaEstadoNotaQuandoEQuem()
    {
        var lacuna = LacunaBusca.Registrar("termo", null, null, "sub-1", "Maria");

        lacuna.AlterarEstado(EstadoLacunaBusca.Resolvida, "  Artigo criado  ", "sub-2", "João", Agora);

        lacuna.Estado.Should().Be(EstadoLacunaBusca.Resolvida);
        lacuna.Nota.Should().Be("Artigo criado");
        lacuna.ResolvidaEm.Should().Be(Agora);
        lacuna.ResolvidoPorId.Should().Be("sub-2");
        lacuna.ResolvidoPorNome.Should().Be("João");
    }

    [Fact]
    public void Descartar_TambemGravaQuemEQuando()
    {
        var lacuna = LacunaBusca.Registrar("termo", null, null, "sub-1", "Maria");

        lacuna.AlterarEstado(EstadoLacunaBusca.Descartada, null, "sub-2", null, Agora);

        lacuna.Estado.Should().Be(EstadoLacunaBusca.Descartada);
        lacuna.Nota.Should().BeNull();
        lacuna.ResolvidaEm.Should().Be(Agora);
        lacuna.ResolvidoPorNome.Should().BeNull();
    }

    [Fact]
    public void Reabrir_LimpaAResolucaoInteira()
    {
        var lacuna = LacunaBusca.Registrar("termo", null, null, "sub-1", "Maria");
        lacuna.AlterarEstado(EstadoLacunaBusca.Resolvida, "feito", "sub-2", "João", Agora);

        lacuna.AlterarEstado(EstadoLacunaBusca.Aberta, "ignorada", "sub-3", "Ana", Agora.AddDays(1));

        lacuna.Estado.Should().Be(EstadoLacunaBusca.Aberta);
        lacuna.Nota.Should().BeNull();
        lacuna.ResolvidaEm.Should().BeNull();
        lacuna.ResolvidoPorId.Should().BeNull();
        lacuna.ResolvidoPorNome.Should().BeNull();
    }

    [Theory]
    [InlineData(EstadoLacunaBusca.Aberta)]
    [InlineData(EstadoLacunaBusca.Resolvida)]
    public void AlterarEstado_ParaOMesmoEstado_Lanca(EstadoLacunaBusca estado)
    {
        var lacuna = LacunaBusca.Registrar("termo", null, null, "sub-1", "Maria");
        if (estado != EstadoLacunaBusca.Aberta)
            lacuna.AlterarEstado(estado, null, "sub-2", "João", Agora);

        var acao = () => lacuna.AlterarEstado(estado, null, "sub-3", "Ana", Agora);

        acao.Should().Throw<TransicaoDeEstadoInvalidaException>();
    }
}
