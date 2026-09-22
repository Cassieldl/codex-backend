using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Tests.Unit.Dominio;

/// <summary>
/// P-10 (I37/I43/I44) — o contrato do <c>AlterarSlug</c> das três entidades é deliberadamente
/// mínimo: troca o próprio campo e nada mais. Quem grava o <see cref="Redirecionamento"/>,
/// desativa a linha conflitante e reindexa a busca é o SERVICE, na mesma transação. Estes casos
/// existem para travar essa divisão — uma entidade que começasse a fazer mais do que isso
/// duplicaria a regra em dois lugares.
/// </summary>
public sealed class AlterarSlugTests
{
    [Fact]
    public void Provedor_AlterarSlug_TrocaOSlugSemTocarNoNome()
    {
        var provedor = Provedor.Criar("Prefeitura de Exemplo", "INV-001", CategoriaProvedor.Nacional);
        provedor.Slug.Should().Be("prefeitura-de-exemplo");

        provedor.AlterarSlug(SlugVo.DeValorPersistido("exemplo-sp"));

        provedor.Slug.Should().Be("exemplo-sp");
        provedor.Nome.Should().Be("Prefeitura de Exemplo", because: "trocar slug não é renomear — são duas operações de produto distintas");
    }

    [Fact]
    public void Erro_AlterarSlug_TrocaOSlugSemTocarNoCodigoNemNoTitulo()
    {
        var erro = Erro.Criar("E-123", OrigemErro.InvoiCy, "Rejeição de exemplo");

        erro.AlterarSlug(SlugVo.DeValorPersistido("e-123-rejeicao-nova"));

        erro.Slug.Should().Be("e-123-rejeicao-nova");
        erro.Codigo.Should().Be("E-123");
        erro.Titulo.Should().Be("Rejeição de exemplo");
    }

    [Fact]
    public void Artigo_AlterarSlug_TrocaOSlugSemTocarNoTitulo()
    {
        var artigo = Artigo.Criar(TipoArtigo.Conceito, "Título original", "Corpo.");

        artigo.AlterarSlug(SlugVo.DeValorPersistido("titulo-novo"));

        artigo.Slug.Should().Be("titulo-novo");
        artigo.Titulo.Should().Be("Título original");
    }

    /// <summary>
    /// A entidade NÃO conhece a reserva (a recusa é do service, §0.2) — este caso registra que a
    /// ausência de guarda aqui é decisão, não esquecimento, e que trocar o slug do reservado é
    /// mecanicamente possível: por isso a barreira precisa existir um nível acima.
    /// </summary>
    [Fact]
    public void Artigo_AlterarSlug_NaoConheceAReservaDeComoFunciona()
    {
        var artigo = Artigo.Criar(TipoArtigo.Conceito, "Como funciona", "Corpo.");

        var trocar = () => artigo.AlterarSlug(SlugVo.DeValorPersistido("outro-nome"));

        trocar.Should().NotThrow(because: "a decisão de recusar é de aplicação (ArtigoServicoInterno), não de domínio");
        artigo.Slug.Should().Be("outro-nome");
    }

    [Theory]
    [InlineData("Provedor")]
    [InlineData("Erro")]
    [InlineData("Artigo")]
    public void AlterarSlug_ComNulo_Lanca(string entidade)
    {
        Action trocar = entidade switch
        {
            "Provedor" => () => Provedor.Criar("P", "INV-002", CategoriaProvedor.Nacional).AlterarSlug(null!),
            "Erro" => () => Erro.Criar("E-1", OrigemErro.InvoiCy, "T").AlterarSlug(null!),
            _ => () => Artigo.Criar(TipoArtigo.Conceito, "T", "C").AlterarSlug(null!),
        };

        trocar.Should().Throw<ArgumentNullException>();
    }
}
