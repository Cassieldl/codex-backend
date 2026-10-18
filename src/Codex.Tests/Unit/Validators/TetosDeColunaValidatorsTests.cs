using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// Todo campo gravado em coluna <c>varchar(n)</c> aceita exatamente n e recusa n+1 no validator, que a
/// API responde com 422 antes de o banco recusar com 500. Os limites estão escritos como literais
/// de propósito: importar a constante do código não pegaria um teto trocado.
/// </summary>
public sealed class TetosDeColunaValidatorsTests
{
    [Theory]
    [InlineData("provedor.criar.nome", 160)]
    [InlineData("provedor.criar.identificadorInvoicy", 80)]
    [InlineData("provedor.criar.alias", 160)]
    [InlineData("provedor.atualizar.nome", 160)]
    [InlineData("provedor.atualizar.alias", 160)]
    [InlineData("versaoProvedor.criar.rotulo", 80)]
    [InlineData("versaoProvedor.atualizar.rotulo", 80)]
    [InlineData("versaoLayout.criar.rotulo", 80)]
    [InlineData("versaoLayout.atualizar.rotulo", 80)]
    [InlineData("artigo.criar.titulo", 200)]
    [InlineData("artigo.atualizar.titulo", 200)]
    [InlineData("aviso.criarDeVersao.titulo", 200)]
    [InlineData("aviso.criarDeProvedor.titulo", 200)]
    [InlineData("aviso.atualizar.titulo", 200)]
    [InlineData("comunicado.criar.titulo", 200)]
    [InlineData("comunicado.atualizar.titulo", 200)]
    [InlineData("exemplo.criar.nome", 160)]
    [InlineData("exemplo.atualizar.nome", 160)]
    [InlineData("campoLayout.criar.nome", 120)]
    [InlineData("campoLayout.criar.caminhoXml", 160)]
    [InlineData("campoLayout.criar.grupo", 120)]
    [InlineData("campoLayout.atualizar.nome", 120)]
    [InlineData("campoLayout.atualizar.grupo", 120)]
    [InlineData("erro.criar.codigo", 40)]
    [InlineData("erro.atualizar.titulo", 200)]
    [InlineData("campoVersao.atualizar.tipo", 60)]
    [InlineData("campoVersao.atualizar.tamanho", 40)]
    [InlineData("campoVersao.atualizar.mascara", 120)]
    [InlineData("campoVersao.atualizar.destinoXml", 400)]
    [InlineData("slug.alterar", 160)]
    public async Task Campo_AceitaOLimite_ERecusaOLimiteMaisUm(string caso, int limite)
    {
        var noLimite = await ValidarAsync(caso, new string('a', limite));
        var acima = await ValidarAsync(caso, new string('a', limite + 1));

        noLimite.Errors.Should().NotContain(e => e.PropertyName.StartsWith(Propriedade(caso), StringComparison.Ordinal),
            because: $"{limite} caracteres cabem na coluna de '{caso}'");
        acima.Errors.Should().Contain(e => e.PropertyName.StartsWith(Propriedade(caso), StringComparison.Ordinal),
            because: $"{limite + 1} caracteres não cabem na coluna de '{caso}' e o banco responderia 500");
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void ErroCriar_SlugDerivadoDoCodigoETitulo_RespeitaAColuna(int tamanhoDoSlug, bool valido)
    {
        var dto = new CriarErroDto
        {
            Codigo = "E0037",
            Origem = OrigemErro.InvoiCy,
            Titulo = new string('t', tamanhoDoSlug - "E0037-".Length),
            Variantes = ["mensagem observada"],
        };

        new CriarErroDtoValidator().Validate(dto).IsValid.Should().Be(valido);
    }

    // ------------------------------------------------------------------ apoio

    private static string Propriedade(string caso) => caso.Split('.')[^1] switch
    {
        "alias" => "Aliases",
        "alterar" => "Slug",
        var campo => char.ToUpperInvariant(campo[0]) + campo[1..],
    };

    private static Task<ValidationResult> ValidarAsync(string caso, string texto) => caso switch
    {
        "provedor.criar.nome" => Validar(new CriarProvedorDtoValidator(), ProvedorCriar() with { Nome = texto }),
        "provedor.criar.identificadorInvoicy" => Validar(new CriarProvedorDtoValidator(), ProvedorCriar() with { IdentificadorInvoicy = texto }),
        "provedor.criar.alias" => Validar(new CriarProvedorDtoValidator(), ProvedorCriar() with { Aliases = [texto] }),
        "provedor.atualizar.nome" => Validar(new AtualizarProvedorDtoValidator(), ProvedorAtualizar() with { Nome = texto }),
        "provedor.atualizar.alias" => Validar(new AtualizarProvedorDtoValidator(), ProvedorAtualizar() with { Aliases = [texto] }),
        "versaoProvedor.criar.rotulo" => Validar(new CriarVersaoDtoValidator(), new CriarVersaoDto { VersaoLayoutId = 1, Rotulo = texto }),
        "versaoProvedor.atualizar.rotulo" => Validar(new AtualizarVersaoDtoValidator(), new AtualizarVersaoDto { Rotulo = texto }),
        "versaoLayout.criar.rotulo" => Validar(new CriarVersaoLayoutDtoValidator(), new CriarVersaoLayoutDto { Rotulo = texto }),
        "versaoLayout.atualizar.rotulo" => Validar(new AtualizarVersaoLayoutDtoValidator(), new AtualizarVersaoLayoutDto { Rotulo = texto }),
        "artigo.criar.titulo" => Validar(new CriarArtigoDtoValidator(), new CriarArtigoDto { Tipo = TipoArtigo.Conceito, Titulo = texto, Corpo = "Corpo" }),
        "artigo.atualizar.titulo" => Validar(new AtualizarArtigoDtoValidator(), new AtualizarArtigoDto { Tipo = TipoArtigo.Conceito, Titulo = texto, Corpo = "Corpo" }),
        "aviso.criarDeVersao.titulo" => Validar(new CriarAvisoDeVersaoDtoValidator(), new CriarAvisoDeVersaoDto { Severidade = Severidade.Informativo, Titulo = texto, Corpo = "Corpo" }),
        "aviso.criarDeProvedor.titulo" => Validar(new CriarAvisoDeProvedorDtoValidator(), new CriarAvisoDeProvedorDto { Severidade = Severidade.Informativo, Titulo = texto, Corpo = "Corpo" }),
        "aviso.atualizar.titulo" => Validar(AvisoAtualizarValidator(), new AtualizarAvisoDto { Severidade = Severidade.Informativo, Titulo = texto, Corpo = "Corpo" }),
        "comunicado.criar.titulo" => Validar(new CriarComunicadoDtoValidator(TimeProvider.System), ComunicadoCriar(texto)),
        "comunicado.atualizar.titulo" => Validar(new AtualizarComunicadoDtoValidator(TimeProvider.System), ComunicadoAtualizar(texto)),
        "exemplo.criar.nome" => Validar(new CriarExemploDtoValidator(), new CriarExemploDto { Nome = texto, Xml = "<x/>" }),
        "exemplo.atualizar.nome" => Validar(new AtualizarExemploDtoValidator(), new AtualizarExemploDto { Nome = texto, Xml = "<x/>" }),
        "campoLayout.criar.nome" => Validar(new CriarCampoLayoutDtoValidator(), CampoLayoutCriar() with { Nome = texto }),
        "campoLayout.criar.caminhoXml" => Validar(new CriarCampoLayoutDtoValidator(), CampoLayoutCriar() with { CaminhoXml = texto }),
        "campoLayout.criar.grupo" => Validar(new CriarCampoLayoutDtoValidator(), CampoLayoutCriar() with { Grupo = texto }),
        "campoLayout.atualizar.nome" => Validar(CampoLayoutAtualizarValidator(), new AtualizarCampoLayoutDto { Nome = texto }),
        "campoLayout.atualizar.grupo" => Validar(CampoLayoutAtualizarValidator(), new AtualizarCampoLayoutDto { Grupo = texto }),
        "erro.criar.codigo" => Validar(new CriarErroDtoValidator(), new CriarErroDto { Codigo = texto, Origem = OrigemErro.InvoiCy, Titulo = "T", Variantes = ["m"] }),
        "erro.atualizar.titulo" => Validar(new AtualizarErroDtoValidator(), new AtualizarErroDto { Origem = OrigemErro.InvoiCy, Titulo = texto, Variantes = ["m"] }),
        "campoVersao.atualizar.tipo" => Validar(new AtualizarCampoVersaoDtoValidator(), CampoVersaoAtualizar() with { Tipo = texto }),
        "campoVersao.atualizar.tamanho" => Validar(new AtualizarCampoVersaoDtoValidator(), CampoVersaoAtualizar() with { Tamanho = texto }),
        "campoVersao.atualizar.mascara" => Validar(new AtualizarCampoVersaoDtoValidator(), CampoVersaoAtualizar() with { Mascara = texto }),
        "campoVersao.atualizar.destinoXml" => Validar(new AtualizarCampoVersaoDtoValidator(), CampoVersaoAtualizar() with { DestinoXml = texto }),
        "slug.alterar" => Validar(new AlterarSlugDtoValidator(), new AlterarSlugDto { Slug = texto }),
        _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, "Caso sem montagem."),
    };

    private static Task<ValidationResult> Validar<T>(IValidator<T> validador, T dto) => validador.ValidateAsync(dto);

    private static CriarProvedorDto ProvedorCriar() => new()
    {
        Nome = "Provedor",
        IdentificadorInvoicy = "INV",
        Categoria = CategoriaProvedor.Nacional,
    };

    private static AtualizarProvedorDto ProvedorAtualizar() => new() { Nome = "Provedor", Categoria = CategoriaProvedor.Nacional };

    private static CriarCampoLayoutDto CampoLayoutCriar() => new() { Nome = "Campo", CaminhoXml = "//Campo", Significado = "Significado" };

    private static AtualizarCampoVersaoDto CampoVersaoAtualizar() => new() { Tipo = "texto", EstadoPreenchimento = EstadoPreenchimento.Obrigatorio };

    private static CriarComunicadoDto ComunicadoCriar(string titulo) => new()
    {
        Titulo = titulo,
        Corpo = "Corpo",
        Severidade = Severidade.Informativo,
        Abrangencia = AbrangenciaComunicado.Global,
        ExpiraEm = DateTimeOffset.UtcNow.AddDays(10),
    };

    private static AtualizarComunicadoDto ComunicadoAtualizar(string titulo) => new()
    {
        Titulo = titulo,
        Corpo = "Corpo",
        Severidade = Severidade.Informativo,
        Abrangencia = AbrangenciaComunicado.Global,
        ExpiraEm = DateTimeOffset.UtcNow.AddDays(10),
    };

    private static AtualizarAvisoDtoValidator AvisoAtualizarValidator() =>
        new(Mock.Of<IAvisoRepositorioInterno>(), Mock.Of<IContextoRequisicaoHttp>());

    private static AtualizarCampoLayoutDtoValidator CampoLayoutAtualizarValidator() =>
        new(Mock.Of<ICampoLayoutRepositorioInterno>(), Mock.Of<IVersaoLayoutRepositorioInterno>(), Mock.Of<IContextoRequisicaoHttp>());
}
