using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// PROBE DE QA — o gabarito §7.4 é eliminatório, então a TABELA precisa ser auditada, não só
/// executada. Dois riscos que <see cref="PipelineAutorizacaoTests"/> não cobre:
/// <list type="number">
/// <item>endpoint interno novo que ninguém adicionou à tabela (a suíte fica verde e o endpoint
/// nunca é exercitado);</item>
/// <item>linha com role divergente da exigida pelo <c>[Authorize]</c> real.</item>
/// </list>
/// Aqui a comparação é estrutural: enumera as rotas registradas e confronta cobertura e role.
/// </summary>
public sealed class TabelaAutorizacaoCompletaTests : IClassFixture<CodexApiFactory>
{
    private readonly CodexApiFactory _factory;

    public TabelaAutorizacaoCompletaTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TodaRotaInternaRegistrada_EstaNaTabelaDoPipelineAutorizacao_ComARoleQueOControllerExige()
    {
        using var escopo = _factory.Services.CreateScope();
        var provedorDeAcoes = escopo.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>();

        var rotasInternas = provedorDeAcoes.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => (a.AttributeRouteInfo?.Template ?? string.Empty).StartsWith("api/v1/interno", StringComparison.OrdinalIgnoreCase))
            .Select(a => (Chave: ChaveDaRota(a), Roles: RolesDaAcao(a)))
            .ToList();

        rotasInternas.Should().NotBeEmpty();

        var tabela = PipelineAutorizacaoTests.EndpointsInternos()
            .Select(linha => (
                Chave: $"{(HttpMethod)linha[0]} {NormalizarRotaDaTabela((string)linha[1])}",
                Role: (string)linha[2]))
            .ToList();

        var chavesDaTabela = tabela.Select(l => l.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var naoCobertas = rotasInternas
            .Where(r => !chavesDaTabela.Contains(r.Chave))
            .Select(r => r.Chave)
            .ToList();

        naoCobertas.Should().BeEmpty(
            because: "todo endpoint interno registrado precisa ter uma linha em PipelineAutorizacaoTests — "
                     + $"faltando: {string.Join(" | ", naoCobertas)}");

        var divergencias = new List<string>();
        foreach (var linha in tabela)
        {
            var rota = rotasInternas.FirstOrDefault(r => string.Equals(r.Chave, linha.Chave, StringComparison.OrdinalIgnoreCase));
            if (rota.Chave is null)
            {
                divergencias.Add($"{linha.Chave} está na tabela mas NÃO é uma rota registrada (linha morta).");
                continue;
            }

            // Igualdade exata: role a mais no atributo abre a rota para quem não deveria entrar.
            if (!rota.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals([linha.Role]))
                divergencias.Add($"{linha.Chave}: tabela declara '{linha.Role}', controller aceita '{string.Join("/", rota.Roles)}'.");
        }

        divergencias.Should().BeEmpty(because: string.Join(" | ", divergencias));
    }

    /// <summary>Converte a rota concreta da tabela ("/api/v1/interno/provedores/1?x=y") no template registrado ("api/v1/interno/provedores/{id:int}").</summary>
    private static string NormalizarRotaDaTabela(string rota)
    {
        var semQuery = rota.Split('?')[0].TrimStart('/');
        var segmentos = semQuery.Split('/')
            .Select(s => int.TryParse(s, out _) ? "{id:int}" : s);

        return string.Join('/', segmentos);
    }

    private static IReadOnlyCollection<string> RolesDaAcao(ControllerActionDescriptor acao) => acao.EndpointMetadata
        .OfType<IAuthorizeData>()
        .Where(d => !string.IsNullOrWhiteSpace(d.Roles))
        .SelectMany(d => d.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .ToList();

    private static string ChaveDaRota(ActionDescriptor acao)
    {
        var metodo = acao.ActionConstraints?.OfType<HttpMethodActionConstraint>()
            .SelectMany(c => c.HttpMethods)
            .FirstOrDefault() ?? "GET";

        return $"{metodo} {acao.AttributeRouteInfo?.Template ?? string.Empty}";
    }
}
