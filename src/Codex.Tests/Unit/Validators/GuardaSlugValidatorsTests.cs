using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;
using FluentValidation;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// Guarda de <c>Slug.DeTexto</c>: todo campo de que um slug é derivado precisa reprovar em 422
/// quando a normalização sobra vazia. Sem ela, <c>Slug.DeTexto</c> lança
/// <see cref="ArgumentException"/> lá dentro do <c>Criar()</c> da entidade e o cliente recebe
/// <c>500</c> — erro de servidor para um caso que é, inteiramente, de entrada inválida.
///
/// <para>
/// O predicado é <c>Slug.PodeDerivarDe</c>, que roda a MESMA normalização de
/// <c>Slug.DeTexto</c> — nunca uma regex paralela do tipo <c>[a-zA-Z0-9]</c>: essa aproximação
/// reprovaria texto legítimo composto só de letras acentuadas, que a normalização converte para
/// ASCII sem problema (o caso "Ç" abaixo).
/// </para>
/// </summary>
public sealed class GuardaSlugValidatorsTests
{
    /// <summary>Entradas cuja normalização sobra vazia — o conjunto que produzia 500.</summary>
    public static TheoryData<string> TextosQueNaoGeramSlug() => new() { "...", "///", "---", "!!! ???", "@#$%" };

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarProvedor_ComNomeSemLetraNemDigito_Reprova(string nome)
    {
        var resultado = new CriarProvedorDtoValidator().Validate(new CriarProvedorDto
        {
            Nome = nome,
            IdentificadorInvoicy = "INV-001",
            Categoria = CategoriaProvedor.Nacional,
        });

        DeveReprovar(resultado, nameof(CriarProvedorDto.Nome));
    }

    [Fact]
    public void CriarProvedor_ComNomeSoDeLetraAcentuada_Aprova()
    {
        var resultado = new CriarProvedorDtoValidator().Validate(new CriarProvedorDto
        {
            Nome = "Ç",
            IdentificadorInvoicy = "INV-001",
            Categoria = CategoriaProvedor.Nacional,
        });

        resultado.IsValid.Should().BeTrue(
            because: "'Ç' normaliza para 'c' e gera slug válido — um predicado por regex ASCII reprovaria por engano");
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarErro_ComCodigoETituloSemLetraNemDigito_Reprova(string texto)
    {
        var resultado = new CriarErroDtoValidator().Validate(new CriarErroDto
        {
            Codigo = texto,
            Origem = OrigemErro.InvoiCy,
            Titulo = texto,
            Variantes = ["mensagem observada"],
        });

        resultado.IsValid.Should().BeFalse(because: "o slug do erro é derivado de \"{codigo}-{titulo}\"");
    }

    /// <summary>
    /// O slug do erro nasce do PAR: um campo sozinho sem letra nem dígito ainda produz slug
    /// válido, e reprovar aqui seria inventar regra que o domínio não tem.
    /// </summary>
    [Fact]
    public void CriarErro_ComCodigoSemLetraMasTituloValido_Aprova()
    {
        var resultado = new CriarErroDtoValidator().Validate(new CriarErroDto
        {
            Codigo = "---",
            Origem = OrigemErro.InvoiCy,
            Titulo = "Rejeição de exemplo",
            Variantes = ["mensagem observada"],
        });

        resultado.IsValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarArtigo_ComTituloSemLetraNemDigito_Reprova(string titulo)
    {
        var resultado = new CriarArtigoDtoValidator().Validate(new CriarArtigoDto
        {
            Tipo = TipoArtigo.Conceito,
            Titulo = titulo,
            Corpo = "Corpo do artigo.",
        });

        DeveReprovar(resultado, nameof(CriarArtigoDto.Titulo));
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarVersaoLayout_ComRotuloSemLetraNemDigito_Reprova(string rotulo)
    {
        var resultado = new CriarVersaoLayoutDtoValidator().Validate(new CriarVersaoLayoutDto { Rotulo = rotulo });

        DeveReprovar(resultado, nameof(CriarVersaoLayoutDto.Rotulo));
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarCampoLayout_ComCaminhoXmlSemLetraNemDigito_Reprova(string caminhoXml)
    {
        var resultado = new CriarCampoLayoutDtoValidator().Validate(new CriarCampoLayoutDto
        {
            Nome = "Campo",
            CaminhoXml = caminhoXml,
            Significado = "Significado do campo.",
        });

        DeveReprovar(resultado, nameof(CriarCampoLayoutDto.CaminhoXml));
    }

    /// <summary>
    /// Sexto sítio de <c>Slug.DeTexto</c>, encontrado varrendo as CHAMADAS e não a lista de
    /// entidades: <c>VersaoProvedor.Criar</c> deriva o slug do rótulo igual às outras cinco.
    /// </summary>
    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarVersaoProvedor_ComRotuloSemLetraNemDigito_Reprova(string rotulo)
    {
        var resultado = new CriarVersaoDtoValidator().Validate(new CriarVersaoDto
        {
            VersaoLayoutId = 1,
            Rotulo = rotulo,
        });

        DeveReprovar(resultado, nameof(CriarVersaoDto.Rotulo));
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void CriarExemplo_ComNomeSemLetraNemDigito_Reprova(string nome)
    {
        var resultado = new CriarExemploDtoValidator().Validate(new CriarExemploDto
        {
            Nome = nome,
            Xml = "<nfe/>",
        });

        DeveReprovar(resultado, nameof(CriarExemploDto.Nome));
    }

    /// <summary>
    /// Sétimo sítio: o exemplo usava uma regex ASCII paralela (<c>[a-zA-Z0-9]</c>) em vez do
    /// predicado do domínio, e por isso divergia dos irmãos — <c>{"nome":"Çã"}</c> era 422 aqui e
    /// 201 em provedor, para o mesmo conceito.
    /// </summary>
    [Fact]
    public void CriarExemplo_ComNomeSoDeLetraAcentuada_Aprova()
    {
        var resultado = new CriarExemploDtoValidator().Validate(new CriarExemploDto
        {
            Nome = "Çã",
            Xml = "<nfe/>",
        });

        resultado.IsValid.Should().BeTrue(
            because: "'Çã' normaliza para 'ca' e gera slug válido — o provedor irmão já aceitava");
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void AtualizarExemplo_ComNomeSemLetraNemDigito_Reprova(string nome)
    {
        var resultado = new AtualizarExemploDtoValidator().Validate(new AtualizarExemploDto
        {
            Nome = nome,
            Xml = "<nfe/>",
        });

        DeveReprovar(resultado, nameof(AtualizarExemploDto.Nome));
    }

    [Theory]
    [MemberData(nameof(TextosQueNaoGeramSlug))]
    public void AlterarSlug_ComSlugSemLetraNemDigito_Reprova(string slug)
    {
        var resultado = new AlterarSlugDtoValidator().Validate(new AlterarSlugDto { Slug = slug });

        DeveReprovar(resultado, nameof(AlterarSlugDto.Slug));
    }

    [Fact]
    public void AlterarSlug_ComTextoLivreQueNormaliza_Aprova()
    {
        var resultado = new AlterarSlugDtoValidator().Validate(new AlterarSlugDto { Slug = "Prefeitura de São Paulo" });

        resultado.IsValid.Should().BeTrue(
            because: "quem normaliza é o backend (Slug.DeTexto no service) — o validator só barra o que não sobrevive à normalização");
    }

    [Fact]
    public void AlterarSlug_AcimaDoTetoDaMenorColuna_Reprova()
    {
        var resultado = new AlterarSlugDtoValidator().Validate(new AlterarSlugDto { Slug = new string('a', 161) });

        DeveReprovar(resultado, nameof(AlterarSlugDto.Slug));
    }

    private static void DeveReprovar(FluentValidation.Results.ValidationResult resultado, string propriedade)
    {
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(erro => erro.PropertyName == propriedade);
    }
}
