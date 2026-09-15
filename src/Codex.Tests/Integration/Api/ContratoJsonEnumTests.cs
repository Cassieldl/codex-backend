using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Contrato HTTP dos campos de ENUM na escrita interna, exercitado com JSON LITERAL escrito à mão
/// — no formato exato que o frontend emite.
///
/// <para>
/// Por que JSON literal e não objeto anônimo: teste que serializa o payload em C# não prova
/// contrato HTTP. <c>JsonContent.Create(new { categoria = CategoriaProvedor.Nacional })</c> emite
/// o ordinal <c>0</c>, que binda mesmo sem <c>JsonStringEnumConverter</c> registrado — o bug fica
/// invisível. O cliente real é TypeScript e emite <c>"categoria": "Nacional"</c> (união de
/// literais <c>'Nacional' | 'Proprio'</c>). Só a string crua no corpo reproduz o wire format do
/// navegador.
/// </para>
///
/// <para>
/// Cada grupo de enum tem os DOIS lados: o nome válido precisa BINDAR (não pode virar
/// <c>MALFORMED_REQUEST</c>) e o nome inexistente precisa FALHAR com <c>400</c> +
/// <c>details[].field</c> apontando o campo. O lado negativo é o que impede o positivo de ser
/// vacuamente verde: ele prova que esta asserção detecta de fato a falha de desserialização.
/// </para>
///
/// <para>
/// Roda sem Postgres (<see cref="CodexApiFactory"/>): a desserialização e o filtro de validação
/// acontecem antes de qualquer acesso a banco, então o que vem depois (422 de regra, 404, 500 por
/// falta de banco) é irrelevante para o que está sendo provado aqui — só importa se o binder
/// aceitou ou rejeitou o corpo.
/// </para>
/// </summary>
public sealed class ContratoJsonEnumTests : IClassFixture<CodexApiFactory>
{
    private const string CodigoMalformado = "MALFORMED_REQUEST";

    private readonly CodexApiFactory _factory;

    public ContratoJsonEnumTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Um caso por grupo de enum dos DTOs internos: rota, método, role, o campo que carrega o enum
    /// e o corpo JSON completo (todos os <c>required</c> presentes — <c>required</c> ausente também
    /// produziria falha de desserialização e contaminaria a asserção).
    /// </summary>
    public static TheoryData<string, string, string, string, string, string> CasosDeEnum() => new()
    {
        // enum                    método  rota                                          role                       campo                  valor válido
        {
            "CategoriaProvedor", "POST", "/api/v1/interno/provedores", "provedores-adicionar",
            "categoria", "Nacional"
        },
        {
            "Severidade", "POST", "/api/v1/interno/provedores/1/avisos", "avisos-adicionar",
            "severidade", "Alerta"
        },
        {
            "EstadoPreenchimento", "POST", "/api/v1/interno/versoes/1/campos", "camposversao-adicionar",
            "estadoPreenchimento", "Condicional"
        },
        {
            "OrigemErro", "POST", "/api/v1/interno/erros", "erros-adicionar",
            "origem", "InvoiCy"
        },
        {
            "EstadoCampoLayout", "PUT", "/api/v1/interno/campos-layout/1", "campolayout-atualizar",
            "estado", "Ativo"
        },
        {
            "EstadoVersaoLayout", "PUT", "/api/v1/interno/layout/versoes/1", "versoeslayout-atualizar",
            "estado", "Vigente"
        },
    };

    [Theory]
    [MemberData(nameof(CasosDeEnum))]
    public async Task EnumComoStringNoJsonLiteral_Binda(
        string grupoDeEnum, string metodo, string rota, string role, string campo, string valorValido)
    {
        var (_, codigo) = await EnviarAsync(metodo, rota, CorpoPara(rota, campo, valorValido), role);

        codigo.Should().NotBe(CodigoMalformado,
            because: "o frontend manda {0} como string (\"{1}\"); se isto falhar na desserialização, " +
                     "a escrita de {2} está quebrada ponta a ponta", campo, valorValido, grupoDeEnum);
    }

    [Theory]
    [MemberData(nameof(CasosDeEnum))]
    public async Task EnumComValorInexistente_Retorna400ComOCampoNosDetalhes(
        string grupoDeEnum, string metodo, string rota, string role, string campo, string valorValido)
    {
        _ = grupoDeEnum;
        _ = valorValido;

        var (status, codigo, detalhes) = await EnviarDetalhadoAsync(
            metodo, rota, CorpoPara(rota, campo, "ValorQueNaoExisteNoEnum"), role);

        status.Should().Be(HttpStatusCode.BadRequest,
            because: "valor fora do enum é corpo MALFORMADO (400), não validação de negócio (422)");
        codigo.Should().Be(CodigoMalformado);

        detalhes.Should().Contain(campo,
            because: "sem o campo em details[] o formulário anuncia erro sem conseguir destacar o campo");
    }

    /// <summary>
    /// Simetria com o binding de query string, que já aceita o nome do enum em qualquer caixa
    /// (<c>TypeConverter</c> do ASP.NET Core, <c>ignoreCase: true</c>). Medido, não presumido: é a
    /// razão pela qual a desserialização do CORPO também é case-insensitive — caso contrário
    /// <c>?categoria=nacional</c> funcionaria e <c>{"categoria":"nacional"}</c> não, no mesmo campo
    /// da mesma entidade.
    /// </summary>
    [Fact]
    public async Task EnumEmCaixaDiferente_Binda_IgualAoBindingDeQueryString()
    {
        var corpo = """
            {
              "nome": "Provedor Caixa Baixa",
              "identificadorInvoicy": "CXB-1",
              "categoria": "nacional",
              "aliases": []
            }
            """;

        var (_, codigo) = await EnviarAsync("POST", "/api/v1/interno/provedores", corpo, "provedores-adicionar");

        codigo.Should().NotBe(CodigoMalformado, because: "a desserialização de enum é case-insensitive");
    }

    /// <summary>
    /// O outro lado da simetria, medido em vez de presumido: a query string ACEITA o nome do enum
    /// em caixa diferente. É esta medição que sustenta a escolha de manter a desserialização do
    /// corpo case-insensitive — as duas portas de entrada do mesmo campo se comportam igual.
    /// </summary>
    [Fact]
    public async Task QueryStringComEnumEmCaixaDiferente_Binda()
    {
        var status = await BuscarComCategoriaAsync("nacional");

        status.Should().NotBe(HttpStatusCode.BadRequest,
            because: "o TypeConverter de enum do ASP.NET Core casa nome de enum ignorando caixa");
    }

    /// <summary>Contraprova do case-insensitive: a tolerância é de CAIXA, não de valor inventado.</summary>
    [Fact]
    public async Task QueryStringComValorForaDoEnum_NaoEhToleradaPeloCaseInsensitive()
    {
        var status = await BuscarComCategoriaAsync("naoexiste");

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpStatusCode> BuscarComCategoriaAsync(string categoria)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/interno/provedores?categoria={categoria}&tamanhoPagina=25");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "provedores-buscar");

        using var resposta = await cliente.SendAsync(requisicao);

        return resposta.StatusCode;
    }

    [Fact]
    public async Task JsonSintaticamenteInvalido_Retorna400MalformedRequest()
    {
        var (status, codigo, _) = await EnviarDetalhadoAsync(
            "POST", "/api/v1/interno/provedores", "{\"nome\": \"sem fechar\"", "provedores-adicionar");

        status.Should().Be(HttpStatusCode.BadRequest);
        codigo.Should().Be(CodigoMalformado);
    }

    /// <summary>
    /// Guarda de não-regressão do 422 legítimo: a correção do <c>400</c> para corpo malformado não
    /// pode reclassificar validação de negócio (FluentValidation), que continua <c>422</c> com
    /// <c>VALIDATION_ERROR</c>. O corpo aqui é JSON perfeitamente válido — só reprova em regra.
    /// </summary>
    [Fact]
    public async Task CorpoBemFormadoQueReprovaEmRegraDeNegocio_ContinuaSendo422()
    {
        var corpo = """
            {
              "rotulo": "",
              "descricao": null
            }
            """;

        var (status, codigo, detalhes) = await EnviarDetalhadoAsync(
            "POST", "/api/v1/interno/layout/versoes", corpo, "versoeslayout-adicionar");

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "rules/api.md: 422 é validação de negócio, e o corpo está bem formado");
        codigo.Should().Be("VALIDATION_ERROR");
        detalhes.Should().Contain("rotulo");
    }

    /// <summary>
    /// Corpo com enum válido E campo que reprova em regra: o binder aceita, então quem responde é
    /// o FluentValidation (422). Prova que as duas classificações coexistem na mesma requisição
    /// sem uma engolir a outra.
    /// </summary>
    [Fact]
    public async Task EnumValidoComOutroCampoInvalido_Retorna422_NaoMalformado()
    {
        var corpo = """
            {
              "codigo": "",
              "origem": "Schema",
              "titulo": "",
              "variantes": []
            }
            """;

        var (status, codigo, detalhes) = await EnviarDetalhadoAsync(
            "POST", "/api/v1/interno/erros", corpo, "erros-adicionar");

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        codigo.Should().Be("VALIDATION_ERROR");
        detalhes.Should().Contain("codigo");
    }

    /// <summary>
    /// Mensagem de erro nunca vaza tipo interno .NET. A mensagem crua da <c>JsonException</c> diz
    /// "could not be converted to Codex.Domain.Enums.CategoriaProvedor" e traz offset de byte —
    /// detalhe interno proibido em resposta de API (rules/security.md A05).
    /// </summary>
    [Fact]
    public async Task DetalheDeCorpoMalformado_NaoVazaNomeDeTipoInternoNemNamespace()
    {
        var corpo = """
            {
              "nome": "Provedor",
              "identificadorInvoicy": "VAZ-1",
              "categoria": "NaoExiste",
              "aliases": []
            }
            """;

        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/provedores")
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "provedores-adicionar");

        using var resposta = await cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        texto.Should().NotContain("Codex.Domain");
        texto.Should().NotContain("CategoriaProvedor");
        texto.Should().NotContain("BytePositionInLine");
        texto.Should().NotContain("System.Text.Json");
    }

    // ---------------------------------------------------------------- infraestrutura do teste

    /// <summary>
    /// Corpo JSON completo por rota, com o campo de enum injetado como STRING. Os <c>required</c>
    /// de cada DTO estão todos presentes de propósito: <c>required</c> ausente é falha de
    /// desserialização e faria o caso positivo falhar por motivo alheio ao enum.
    /// </summary>
    private static string CorpoPara(string rota, string campo, string valorDoEnum) => rota switch
    {
        "/api/v1/interno/provedores" => $$"""
            {
              "nome": "Provedor de Contrato",
              "identificadorInvoicy": "CTR-1",
              "{{campo}}": "{{valorDoEnum}}",
              "aliases": []
            }
            """,

        "/api/v1/interno/provedores/1/avisos" => $$"""
            {
              "{{campo}}": "{{valorDoEnum}}",
              "titulo": "Aviso de contrato",
              "corpo": "Corpo do aviso de contrato.",
              "vigenciaInicio": null
            }
            """,

        "/api/v1/interno/versoes/1/campos" => $$"""
            {
              "campoLayoutId": 1,
              "tipo": "string",
              "{{campo}}": "{{valorDoEnum}}",
              "condicao": "Quando aplicável.",
              "ordem": 1
            }
            """,

        "/api/v1/interno/erros" => $$"""
            {
              "codigo": "E-CTR-1",
              "{{campo}}": "{{valorDoEnum}}",
              "titulo": "Erro de contrato",
              "variantes": ["mensagem observada"],
              "versoesProvedorIds": [],
              "camposLayoutIds": []
            }
            """,

        "/api/v1/interno/campos-layout/1" => $$"""
            {
              "{{campo}}": "{{valorDoEnum}}"
            }
            """,

        "/api/v1/interno/layout/versoes/1" => $$"""
            {
              "{{campo}}": "{{valorDoEnum}}"
            }
            """,

        _ => throw new ArgumentOutOfRangeException(nameof(rota), rota, "Rota sem corpo JSON definido no teste."),
    };

    private async Task<(HttpStatusCode Status, string? Codigo)> EnviarAsync(
        string metodo, string rota, string corpoJson, string role)
    {
        var (status, codigo, _) = await EnviarDetalhadoAsync(metodo, rota, corpoJson, role);
        return (status, codigo);
    }

    private async Task<(HttpStatusCode Status, string? Codigo, IReadOnlyList<string> CamposComErro)> EnviarDetalhadoAsync(
        string metodo, string rota, string corpoJson, string role)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(new HttpMethod(metodo), rota)
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, role);

        // PUT interno exige If-Match; sem ele a ação devolve 400 IF_MATCH_OBRIGATORIO, que não é
        // falha de desserialização mas confundiria a leitura do resultado.
        if (metodo == "PUT")
            requisicao.Headers.TryAddWithoutValidation("If-Match", "1");

        using var resposta = await cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(texto))
            return (resposta.StatusCode, null, []);

        JsonElement raiz;
        try
        {
            raiz = JsonDocument.Parse(texto).RootElement.Clone();
        }
        catch (JsonException)
        {
            return (resposta.StatusCode, null, []);
        }

        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("error", out var erro))
            return (resposta.StatusCode, null, []);

        var codigo = erro.TryGetProperty("code", out var propriedadeCodigo)
            ? propriedadeCodigo.GetString()
            : null;

        var campos = erro.TryGetProperty("details", out var detalhes) && detalhes.ValueKind == JsonValueKind.Array
            ? detalhes.EnumerateArray()
                .Select(d => d.TryGetProperty("field", out var campo) ? campo.GetString() : null)
                .Where(c => c is not null)
                .Select(c => c!)
                .ToList()
            : [];

        return (resposta.StatusCode, codigo, campos);
    }
}
