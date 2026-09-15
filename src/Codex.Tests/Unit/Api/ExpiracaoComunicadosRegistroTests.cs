using Codex.API.Extensoes;
using Codex.API.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Codex.Tests.Unit.Api;

/// <summary>
/// Registro CONDICIONAL do job de expiração (analise-backend.md §10.3). "Desligado" precisa
/// significar <b>não existe no host</b>, não "existe e não faz nada" — é o que sustenta o teste do
/// predicado de vigência (§10.4), que só prova a rede de segurança se o job comprovadamente não
/// estiver rodando por baixo.
///
/// <para>
/// A decisão é inspecionada na própria <see cref="IServiceCollection"/> montada por
/// <see cref="ComunicadosExtensoes.AddComunicados"/>, e não num host de teste: configuração
/// injetada por <c>WebApplicationFactory</c> só existe DEPOIS que o <c>Program.cs</c> já rodou
/// suas chamadas de registro, então um host de teste responderia sempre com o valor do
/// <c>appsettings</c>, jamais com o do caso.
/// </para>
/// </summary>
public sealed class ExpiracaoComunicadosRegistroTests
{
    [Fact]
    public void ExpiracaoHabilitadaFalse_NaoRegistraOHostedService()
    {
        var servicos = Registrar(("Comunicados:ExpiracaoHabilitada", "false"));

        JobsRegistrados(servicos).Should().Be(0,
            because: "desligado é não existir no host — 'roda mas não faz nada' deixaria timing de job por baixo dos testes de predicado");
    }

    [Fact]
    public void ExpiracaoHabilitadaTrue_RegistraOHostedService()
    {
        var servicos = Registrar(("Comunicados:ExpiracaoHabilitada", "true"));

        JobsRegistrados(servicos).Should().Be(1,
            because: "sem o registro, a convergência de estado nunca acontece e 'Encerrado' vira só um cálculo de leitura");
    }

    [Fact]
    public void SemAChaveDeConfiguracao_OJobNasceLigado()
    {
        var servicos = Registrar();

        JobsRegistrados(servicos).Should().Be(1,
            because: "esquecer a chave em produção não pode silenciar a expiração — o default seguro é convergir");
    }

    [Fact]
    public void ServicoDeExpiracao_ERegistradoIndependenteDoJob()
    {
        var servicos = Registrar(("Comunicados:ExpiracaoHabilitada", "false"));

        servicos.Should().Contain(descritor => descritor.ServiceType == typeof(Codex.Application.Interfaces.IExpiracaoComunicadosService),
            because: "a regra continua disponível para execução manual/teste mesmo com o agendamento desligado");
    }

    private static ServiceCollection Registrar(params (string Chave, string Valor)[] configuracoes)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(configuracoes.ToDictionary(par => par.Chave, par => (string?)par.Valor))
            .Build();

        var servicos = new ServiceCollection();
        servicos.AddComunicados(configuracao);

        return servicos;
    }

    private static int JobsRegistrados(IServiceCollection servicos) => servicos.Count(descritor =>
        descritor.ServiceType == typeof(IHostedService)
        && descritor.ImplementationFactory is not null
        && descritor.ImplementationType is null
        && EhDoJob(descritor));

    /// <summary>
    /// O registro usa factory (o job recebe o intervalo já resolvido), então não há
    /// <c>ImplementationType</c> para comparar — a identificação é pelo tipo de retorno declarado
    /// da factory, que continua sendo <see cref="ExpiracaoComunicadosJob"/>.
    /// </summary>
    private static bool EhDoJob(ServiceDescriptor descritor) =>
        descritor.ImplementationFactory!.GetType().GetGenericArguments().Contains(typeof(ExpiracaoComunicadosJob));
}
