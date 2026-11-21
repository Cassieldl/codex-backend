using Codex.API.Extensoes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Codex.Tests.Unit.Seguranca;

/// <summary>
/// Achado <c>BAIXO-B10</c>: configuração de segurança precisa falhar na SUBIDA, não degradar em
/// silêncio (<c>rules/security.md</c> — <i>fail securely</i>). Sem Keycloak a aplicação subia
/// saudável com autenticação impossível (401 universal, sem log); <c>AllowedHosts: "*"</c> deixava
/// o host filtering desligado; e <c>Busca:TimeoutMs = 0</c> DESLIGA o <c>statement_timeout</c> do
/// Postgres, removendo a proteção de custo do endpoint anônimo.
/// </summary>
public sealed class ConfiguracaoSegurancaFailFastTests
{
    private static IConfiguration Configuracao(params (string Chave, string? Valor)[] pares) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pares.ToDictionary(par => par.Chave, par => par.Valor))
            .Build();

    private static IHostEnvironment Ambiente(string nome) => new AmbienteFake(nome);

    /// <summary>Só o <c>EnvironmentName</c> importa para o validador — o resto é cerimônia da interface.</summary>
    private sealed class AmbienteFake : IHostEnvironment
    {
        public AmbienteFake(string nome)
        {
            EnvironmentName = nome;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Codex.API";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (string Chave, string? Valor)[] ProducaoValida() =>
    [
        ("Keycloak:auth-server-url", "https://auth.exemplo.com"),
        ("Keycloak:realm", "codex"),
        ("AllowedHosts", "codex.exemplo.com"),
        ("Cors:OrigemPermitida", "https://portal.exemplo.com"),
        ("Busca:TimeoutMs", "2000"),
        ("Busca:OrcamentoRequisicaoMs", "2000"),
    ];

    [Fact]
    public void Producao_ComConfiguracaoCompleta_NaoLanca()
    {
        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(ProducaoValida()), Ambiente(Environments.Production));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Keycloak:auth-server-url", "")]
    [InlineData("Keycloak:realm", "")]
    public void Producao_SemKeycloak_Lanca(string chaveApagada, string valorApagado)
    {
        var pares = ProducaoValida()
            .Select(par => par.Chave == chaveApagada ? (par.Chave, (string?)valorApagado) : par)
            .ToArray();

        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(pares), Ambiente(Environments.Production));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Keycloak*");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("codex.exemplo.com;*")]
    [InlineData("")]
    public void Producao_ComAllowedHostsCuringaOuVazio_Lanca(string allowedHosts)
    {
        var pares = ProducaoValida()
            .Select(par => par.Chave == "AllowedHosts" ? (par.Chave, (string?)allowedHosts) : par)
            .ToArray();

        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(pares), Ambiente(Environments.Production));

        act.Should().Throw<InvalidOperationException>().WithMessage("*AllowedHosts*");
    }

    [Theory]
    [InlineData("0")]     // desliga o statement_timeout no Postgres
    [InlineData("-1")]    // faz todo SET LOCAL falhar
    [InlineData("249")]   // abaixo do piso
    [InlineData("60000")] // acima do teto
    [InlineData("abc")]
    public void TimeoutDaBuscaForaDoIntervalo_Lanca_EmQualquerAmbiente(string timeoutMs)
    {
        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(("Busca:TimeoutMs", timeoutMs)), Ambiente(Environments.Development));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Busca:TimeoutMs*");
    }

    [Fact]
    public void TimeoutDaBuscaAusente_NaoLanca_PorqueODefaultDeCodigoEhSeguro()
    {
        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(ProducaoValida().Where(par => par.Chave != "Busca:TimeoutMs").ToArray()),
            Ambiente(Environments.Production));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("0")]     // desliga o orçamento de parede da cascata de autocorreção (MED-B05)
    [InlineData("-1")]
    [InlineData("249")]   // abaixo do piso
    [InlineData("60000")] // acima do teto
    [InlineData("abc")]
    public void OrcamentoDaBuscaForaDoIntervalo_Lanca_EmQualquerAmbiente(string orcamentoMs)
    {
        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(("Busca:OrcamentoRequisicaoMs", orcamentoMs)), Ambiente(Environments.Development));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Busca:OrcamentoRequisicaoMs*");
    }

    [Fact]
    public void OrcamentoDaBuscaAusente_NaoLanca_PorqueODefaultDeCodigoEhSeguro()
    {
        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(ProducaoValida().Where(par => par.Chave != "Busca:OrcamentoRequisicaoMs").ToArray()),
            Ambiente(Environments.Production));

        act.Should().NotThrow();
    }

    [Fact]
    public void Development_SemNenhumaConfiguracao_NaoLanca()
    {
        // Development roda sem Keycloak e sem hosts declarados de propósito — o fail-fast é para
        // os ambientes onde a configuração vem do ambiente e pode simplesmente não chegar.
        var act = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(
            Configuracao(), Ambiente(Environments.Development));

        act.Should().NotThrow();
    }

    [Fact]
    public void AppsettingsDeProducaoDoRepositorio_ContinuaSemCuringaEmAllowedHosts()
    {
        // Gabarito do arquivo real: AllowedHosts: "*" foi o valor commitado que o achado
        // apontou. Se alguém devolver o curinga ao appsettings.json, isto reprova.
        var configuracao = new ConfigurationBuilder()
            .SetFileProvider(new PhysicalFileProvider(DiretorioDoProjetoApi()))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        configuracao["AllowedHosts"].Should().NotBeNullOrWhiteSpace();
        configuracao["AllowedHosts"]!.Split(';').Should().NotContain("*");
    }

    /// <summary>
    /// Medido no smoke ponta a ponta: com a Authority em HTTP e ambiente Production, a aplicação
    /// SOBE e morre em toda requisição — o JwtBearer só resolve a Authority no primeiro request e
    /// lança "Authority must use HTTPS" dentro do middleware de autenticação, que roda antes de
    /// qualquer rota. Até `GET /saude/vivo` responde 500, então o orquestrador mata e recria a
    /// instância em laço sem nunca dizer o motivo. Falhar na subida troca esse laço mudo por uma
    /// mensagem.
    /// </summary>
    [Theory]
    [InlineData("http://auth.exemplo.com")]
    [InlineData("HTTP://auth.exemplo.com")]
    public void AuthoritySemHttpsForaDeDevelopment_RecusaSubir(string authority)
    {
        var configuracao = Configuracao([
            .. ProducaoValida().Where(par => par.Chave != "Keycloak:auth-server-url"),
            ("Keycloak:auth-server-url", authority),
        ]);

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().Throw<InvalidOperationException>().WithMessage("*HTTPS*");
    }

    /// <summary>
    /// O caso legítimo: Keycloak numa rede interna, com o TLS terminando antes dele. Continua
    /// possível — mas só declarando, nunca por omissão.
    /// </summary>
    [Fact]
    public void AuthoritySemHttps_ComDispensaExplicita_Sobe()
    {
        var configuracao = Configuracao([
            .. ProducaoValida().Where(par => par.Chave != "Keycloak:auth-server-url"),
            ("Keycloak:auth-server-url", "http://keycloak:8080"),
            ("Keycloak:RequireHttpsMetadata", "false"),
        ]);

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().NotThrow();
    }

    /// <summary>Valor que apenas "parece" falso não dispensa nada — só o `false` literal.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("nao")]
    [InlineData("")]
    public void AuthoritySemHttps_ComDispensaMalEscrita_ContinuaRecusando(string valor)
    {
        var configuracao = Configuracao([
            .. ProducaoValida().Where(par => par.Chave != "Keycloak:auth-server-url"),
            ("Keycloak:auth-server-url", "http://keycloak:8080"),
            ("Keycloak:RequireHttpsMetadata", valor),
        ]);

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().Throw<InvalidOperationException>().WithMessage("*HTTPS*");
    }

    [Fact]
    public void AuthorityEmHttpNoDevelopment_ContinuaValida()
    {
        var configuracao = Configuracao(("Keycloak:auth-server-url", "http://localhost:8080"), ("Keycloak:realm", "codex"));

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Development"));

        acao.Should().NotThrow(because: "o Keycloak local roda em HTTP e é o fluxo normal de desenvolvimento");
    }

    /// <summary>
    /// <c>"*"</c> liga AllowAnyOrigin; vazio, caminho ou barra final sobem e bloqueiam o portal sem
    /// diagnóstico, porque nunca casam com o header Origin do navegador.
    /// </summary>
    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("http://portal.exemplo.com")]
    [InlineData("https://portal.exemplo.com/")]
    [InlineData("https://portal.exemplo.com/app")]
    public void OrigemCorsInvalidaForaDeDevelopment_RecusaSubir(string origem)
    {
        var configuracao = Configuracao([
            .. ProducaoValida().Where(par => par.Chave != "Cors:OrigemPermitida"),
            ("Cors:OrigemPermitida", origem),
        ]);

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().Throw<InvalidOperationException>().WithMessage("*Cors:OrigemPermitida*");
    }

    /// <summary>O ambiente de smoke roda em Production com o portal em http://localhost:5173.</summary>
    [Fact]
    public void OrigemCorsHttpEmLoopback_Sobe()
    {
        var configuracao = Configuracao([
            .. ProducaoValida().Where(par => par.Chave != "Cors:OrigemPermitida"),
            ("Cors:OrigemPermitida", "http://localhost:5173"),
        ]);

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().NotThrow();
    }

    /// <summary>
    /// Authority em HTTPS e descoberta em HTTP, sem dispensa: o JwtBearer lança no primeiro request
    /// e toda rota responde 500, inclusive a sonda de liveness. Tem de falhar na partida.
    /// </summary>
    [Fact]
    public void MetadataAddressSemHttpsForaDeDevelopment_RecusaSubir()
    {
        var configuracao = Configuracao([
            .. ProducaoValida(),
            ("Keycloak:MetadataAddress", "http://keycloak.interno:8080/realms/codex/.well-known/openid-configuration"),
        ]);

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().Throw<InvalidOperationException>().WithMessage("*Keycloak:MetadataAddress*");
    }

    /// <summary>
    /// O exemplo de produção versionado passa na partida como está e também com o bloco opcional
    /// do Keycloak interno descomentado, que é o caminho que levava a 500 em todas as rotas.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExemploDeProducaoDoRepositorio_PassaNaValidacaoDePartida(bool comKeycloakInterno)
    {
        var configuracao = Configuracao(LerExemploDeProducao(comKeycloakInterno));

        (configuracao["Keycloak:MetadataAddress"] is not null).Should().Be(comKeycloakInterno,
            because: "o caso com Keycloak interno precisa de fato exercitar o MetadataAddress do exemplo");

        var acao = () => ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(configuracao, Ambiente("Production"));

        acao.Should().NotThrow();
    }

    private static (string Chave, string? Valor)[] LerExemploDeProducao(bool descomentarKeycloakInterno)
    {
        const string prefixoComentadoDoKeycloak = "# Keycloak__";

        return File.ReadAllLines(Path.Combine(DiretorioRaizDoRepositorio(), ".env.production.example"))
            .Select(linha => descomentarKeycloakInterno && linha.StartsWith(prefixoComentadoDoKeycloak, StringComparison.Ordinal)
                ? linha[2..]
                : linha)
            .Where(linha => !string.IsNullOrWhiteSpace(linha) && !linha.TrimStart().StartsWith('#'))
            .Select(linha => linha.Split('=', 2))
            .Select(partes => (partes[0].Trim().Replace("__", ":", StringComparison.Ordinal), (string?)partes[1].Trim()))
            .ToArray();
    }

    private static string DiretorioDoProjetoApi() => Path.Combine(DiretorioRaizDoRepositorio(), "src", "Codex.API");

    private static string DiretorioRaizDoRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Codex.slnx")))
            diretorio = diretorio.Parent;

        diretorio.Should().NotBeNull(because: "o teste precisa localizar a raiz do repositório para ler os arquivos reais");

        return diretorio!.FullName;
    }
}
