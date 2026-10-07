using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace Codex.Tests.Unit.Seguranca;

/// <summary>
/// O realm de desenvolvimento declara exatamente as roles que a API exige. Role que o código pede
/// e o realm não tem deixa o usuário de dev sem acesso ao endpoint, em silêncio; foi assim que o
/// arquivo ficou com 36 roles contra as 47 que a API usava. A lista esperada é lida dos
/// <c>[Authorize(Roles = ...)]</c> reais, nunca escrita à mão.
/// </summary>
public sealed class RealmKeycloakRolesTests
{
    private const string ClientDoBackend = "codex-backend";

    [Fact]
    public void RolesDoRealm_SaoExatamenteAsQueOsControllersExigem()
    {
        var exigidas = RolesExigidasPelosControllers();
        var doRealm = RolesDeClientDoRealm();

        exigidas.Should().NotBeEmpty();
        doRealm.Should().BeEquivalentTo(exigidas,
            because: "role faltando no realm deixa o usuário de dev sem o endpoint; sobrando é lixo que ninguém exige");
    }

    [Fact]
    public void UsuarioDeDev_TemTodasAsRolesDoRealm()
    {
        var doRealm = RolesDeClientDoRealm();
        var doUsuario = RolesDoUsuarioDeDev();

        doUsuario.Should().BeEquivalentTo(doRealm,
            because: "o usuário de dev precisa exercitar todos os endpoints — role criada e não atribuída não serve a ninguém");
    }

    private static HashSet<string> RolesExigidasPelosControllers()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Codex.API.Controllers", StringComparison.Ordinal) == true);

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var controller in controllers)
        {
            var atributos = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)));

            foreach (var atributo in atributos.Where(a => !string.IsNullOrWhiteSpace(a.Roles)))
            {
                foreach (var role in atributo.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    roles.Add(role);
            }
        }

        return roles;
    }

    private static HashSet<string> RolesDeClientDoRealm()
    {
        using var documento = LerRealm();

        return documento.RootElement.GetProperty("roles").GetProperty("client").GetProperty(ClientDoBackend)
            .EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> RolesDoUsuarioDeDev()
    {
        using var documento = LerRealm();

        var usuario = documento.RootElement.GetProperty("users").EnumerateArray()
            .Single(u => u.GetProperty("username").GetString() == "codex.dev");

        return usuario.GetProperty("clientRoles").GetProperty(ClientDoBackend)
            .EnumerateArray().Select(r => r.GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    private static JsonDocument LerRealm()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            var caminho = Path.Combine(diretorio.FullName, "docker", "keycloak", "realm-codex.json");
            if (File.Exists(caminho))
                return JsonDocument.Parse(File.ReadAllText(caminho));

            diretorio = diretorio.Parent;
        }

        throw new FileNotFoundException("docker/keycloak/realm-codex.json não encontrado acima de " + AppContext.BaseDirectory);
    }
}
