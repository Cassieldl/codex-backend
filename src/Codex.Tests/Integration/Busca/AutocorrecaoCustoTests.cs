using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Teto de custo da autocorreção no <c>/busca</c> ANÔNIMO (MED-B05).
///
/// <para>
/// O único cap de custo declarado era o <c>SET LOCAL statement_timeout</c>, e ele vale por
/// STATEMENT, não por requisição. O número de statements era função do termo do usuário: um termo
/// de 299 caracteres (dentro do cap de tamanho) rende até 60 tokens candidatos × 3 consultas cada
/// — medido em ~4,2 s de Postgres por requisição com léxico de 60 mil termos, crescendo linear com
/// o léxico. Endpoint anônimo, sem autenticação.
/// </para>
///
/// <para>
/// O observável do cap é quantos tokens a resposta declara ter corrigido: a autocorreção é
/// EXPLÍCITA por contrato (nunca corrige em silêncio), então cada token efetivamente corrigido
/// aparece em <c>correcao.tokens</c>. Mais tokens ali = mais consultas gastas.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class AutocorrecaoCustoTests
{
    /// <summary>Espelha <c>ConsultaBuscaPostgres.CapTokensAutocorrecao</c> — se um mudar sem o outro, este teste falha, que é o objetivo.</summary>
    private const int CapEsperado = 6;

    private const int TokensNoTermo = 8;

    private readonly PostgresContainerFixture _fixture;

    public AutocorrecaoCustoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task TermoComMaisTokensCorrigiveisQueOCap_CorrigeSoAteOCap()
    {
        var sufixo = NovoSufixo();

        // Pares: o termo CORRETO entra no léxico e no item indexado; o DIGITADO tem um caractere
        // trocado (levenshtein 1), fica ausente do léxico e por isso é candidato a correção.
        var corretos = Enumerable.Range(0, TokensNoTermo).Select(i => $"zcusto{i}abc{sufixo}").ToArray();
        var digitados = Enumerable.Range(0, TokensNoTermo).Select(i => $"zcusto{i}abd{sufixo}").ToArray();

        await using (var setup = _fixture.CriarContexto())
        {
            var itemId = Random.Shared.Next(1_000_000, int.MaxValue);
            var item = ItemBusca.Criar(TipoItemIndexado.erro, itemId, $"Item custo {sufixo}", $"/erros/item-{itemId}");
            item.AtualizarPesos(string.Join(' ', corretos), null, null, null);
            item.MarcarPublicado(true);
            setup.ItensBusca.Add(item);
            await setup.SaveChangesAsync();

            foreach (var correto in corretos)
            {
                await setup.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO codex.termosbusca (termo, ocorrencias) VALUES ({correto}, 5) ON CONFLICT (termo) DO NOTHING");
            }
        }

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            string.Join(' ', digitados), null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("aproximada",
            because: "sanidade do arranjo: FORTE e PARCIAL falham com os tokens digitados, então a autocorreção É exercitada");
        resultado.Correcao.Should().NotBeNull();

        resultado.Correcao!.Tokens.Should().HaveCount(CapEsperado,
            because: $"o termo tinha {TokensNoTermo} tokens corrigíveis e o cap é {CapEsperado} — sem ele, cada token " +
                     "extra custa 3 consultas numa requisição anônima, e o statement_timeout não limita a requisição");
    }

    /// <summary>
    /// Par de inclusão: o cap não pode transformar a autocorreção em enfeite. A consulta real tem
    /// mediana de 2 a 4 palavras e continua sendo corrigida por inteiro.
    /// </summary>
    [Fact]
    public async Task TermoDeDoisTokens_ContinuaSendoCorrigidoPorInteiro()
    {
        var sufixo = NovoSufixo();
        var corretos = new[] { $"zcustoAabc{sufixo}", $"zcustoBabc{sufixo}" };
        var digitados = new[] { $"zcustoAabd{sufixo}", $"zcustoBabd{sufixo}" };

        await using (var setup = _fixture.CriarContexto())
        {
            var itemId = Random.Shared.Next(1_000_000, int.MaxValue);
            var item = ItemBusca.Criar(TipoItemIndexado.erro, itemId, $"Item custo par {sufixo}", $"/erros/item-{itemId}");
            item.AtualizarPesos(string.Join(' ', corretos), null, null, null);
            item.MarcarPublicado(true);
            setup.ItensBusca.Add(item);
            await setup.SaveChangesAsync();

            foreach (var correto in corretos)
            {
                await setup.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO codex.termosbusca (termo, ocorrencias) VALUES ({correto}, 5) ON CONFLICT (termo) DO NOTHING");
            }
        }

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            string.Join(' ', digitados), null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("aproximada");
        resultado.Correcao!.Tokens.Should().HaveCount(2);
        resultado.TotalGeral.Should().Be(1);
    }
}
