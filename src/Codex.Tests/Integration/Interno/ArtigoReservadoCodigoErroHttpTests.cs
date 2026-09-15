using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// `analise-backend.md` §5.2 exige que I27 e I28 recusem o artigo reservado com o código
/// <c>ARTIGO_RESERVADO</c> — não com o genérico <c>VALIDACAO_NEGOCIO</c>.
///
/// <para>
/// Por que existe: o código específico não estava no ar. A recusa funcionava (422 correto, artigo
/// preservado), mas o cliente recebia o mesmo código de qualquer outra violação de regra, e
/// nenhum teste notava porque nenhum afirmava o código. Achado da unidade BACK-C ao resolver o
/// caso simétrico do comunicado expirado.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ArtigoReservadoCodigoErroHttpTests : IAsyncLifetime
{
    private const string RolesDeArtigo =
        "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-publicar,artigos-remover";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    /// <summary>Id do artigo reservado semeado por este teste, para a limpeza — ver <see cref="DisposeAsync"/>.</summary>
    private int? _reservadoSemeadoId;

    public ArtigoReservadoCodigoErroHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    /// <summary>
    /// O slug reservado é global e único: deixá-lo no banco faria
    /// <c>ContratoArtigoHttpTests.P8_ArtigoReservadoAusente_Retorna404_NuncaGone</c> passar a ver
    /// um artigo existente e falhar — aconteceu de verdade na primeira execução desta classe.
    /// Quem semeia estado global limpa; sem isso a suíte fica dependente da ordem das classes.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_reservadoSemeadoId is { } id)
            await RemoverArtigoReservadoAsync(id);

        _cliente.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task I27_RemoverArtigoReservado_Recusa_ComCodigoProprioNoEnvelope()
    {
        var (id, versao) = await GarantirArtigoReservadoAsync();

        var (status, corpo) = await EnviarAsync(HttpMethod.Delete, $"/api/v1/interno/artigos/{id}", ifMatch: versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodigoDe(corpo).Should().Be(CodigosErro.ArtigoReservado,
            because: "o cliente precisa distinguir 'este artigo é obrigatório' de uma violação de regra qualquer — §5.2");
    }

    [Fact]
    public async Task I28_DespublicarArtigoReservado_Recusa_ComCodigoProprioNoEnvelope()
    {
        var (id, versao) = await GarantirArtigoReservadoAsync();

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{id}/publicacao", new { publicado = false }, versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodigoDe(corpo).Should().Be(CodigosErro.ArtigoReservado,
            because: "despublicar o reservado derruba a rota /como-funciona compilada no frontend — mesma recusa de I27, mesmo código");
    }

    [Fact]
    public async Task I28_PublicarArtigoReservado_ContinuaPermitido()
    {
        var (id, versao) = await GarantirArtigoReservadoAsync();

        var (status, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{id}/publicacao", new { publicado = true }, versao);

        status.Should().Be(HttpStatusCode.OK,
            because: "publicar nunca quebra link — a reserva só barra o caminho que tira o artigo do ar (§5.2)");
    }

    [Fact]
    public void ViolacaoSemCodigoProprio_ContinuaNoGenerico()
    {
        new RegraDeNegocioException("Qualquer invariante de domínio.").Codigo
            .Should().Be(RegraDeNegocioException.CodigoGenerico,
                because: "a sobrecarga de um argumento é a que os 5 usos anteriores já faziam — dar código específico a todos eles seria inventar contrato que ninguém pediu");
    }

    // ------------------------------------------------------------------ apoio

    private static string? CodigoDe(JsonElement corpo) =>
        corpo.GetProperty("error").GetProperty("code").GetString();

    /// <summary>
    /// O slug reservado é único no banco e o container é compartilhado entre classes de teste:
    /// semeia só se ainda não existir, e devolve o <c>xmin</c> corrente lendo o recurso por HTTP.
    /// </summary>
    private async Task<(int Id, uint Versao)> GarantirArtigoReservadoAsync()
    {
        int id;

        await using (var contexto = _fixture.CriarContexto())
        {
            var existente = contexto.Artigos.FirstOrDefault(a => a.Slug == SlugsReservadosArtigo.ComoFunciona);

            if (existente is null)
            {
                var artigo = Artigo.Criar(TipoArtigo.Conceito, "Como funciona", "Corpo do artigo obrigatório.");
                artigo.Slug.Should().Be(SlugsReservadosArtigo.ComoFunciona,
                    because: "o slug reservado nasce do título — se a slugificação mudar, este cenário para de exercitar a reserva");

                contexto.Artigos.Add(artigo);
                await contexto.SaveChangesAsync();
                id = artigo.Id;
                _reservadoSemeadoId = id;
            }
            else
            {
                id = existente.Id;
            }
        }

        var (status, corpo) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/artigos/{id}");
        status.Should().Be(HttpStatusCode.OK);

        var dados = corpo.GetProperty("data");
        dados.GetProperty("reservado").GetBoolean().Should().BeTrue(
            because: "sem isso o cenário poderia estar exercitando um artigo comum e passando por engano");

        return (id, dados.GetProperty("versaoConcorrencia").GetUInt32());
    }

    /// <summary>
    /// Hard delete — o soft delete de I27 deixaria o slug ocupado (ele é único na tabela) e o
    /// artigo continuaria existindo para o teste de ausência. A linha de projeção some junto:
    /// <c>itensbusca</c> não tem FK para <c>artigos</c>, então ficaria órfã.
    /// </summary>
    private async Task RemoverArtigoReservadoAsync(int id)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            DELETE FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id;
            DELETE FROM codex.artigos WHERE artigoid = @id;
            """;
        comando.Parameters.AddWithValue("id", id);

        await comando.ExecuteNonQueryAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeArtigo);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
