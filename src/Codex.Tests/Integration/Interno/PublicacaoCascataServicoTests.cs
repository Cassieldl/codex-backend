using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// analise-backend.md §3.4/§6.6: prova (não só declara) que os services de escrita internos desta
/// unidade — não o <c>IndiceBuscaWriter</c> isolado, já coberto por
/// <c>Integration/Busca/CascataPublicacaoMultinivelTests.cs</c> (fora do escopo desta unidade) —
/// realmente disparam a cascata de publicação na MESMA transação da escrita de conteúdo, via
/// <see cref="IUnidadeDeTrabalho"/>. Container próprio (Postgres real via Testcontainers).
/// </summary>
public sealed class PublicacaoCascataServicoTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();

    public Task InitializeAsync() => _postgres.InitializeAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private ServiceProvider ConstruirProvedorDeServicos()
    {
        var servicos = new ServiceCollection();
        servicos.AddDbContext<Codex.Infrastructure.Data.CodexDbContext>(options =>
            options.UseNpgsql(_postgres.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", Codex.Infrastructure.Data.CodexDbContext.Schema)));

        servicos.AddScoped<IProvedorRepositorioInterno, Codex.Infrastructure.Repositories.ProvedorRepositorioInterno>();
        servicos.AddScoped<IAvisoRepositorioInterno, Codex.Infrastructure.Repositories.AvisoRepositorioInterno>();
        servicos.AddScoped<IIndiceBuscaWriter, Codex.Infrastructure.Busca.IndiceBuscaWriter>();
        servicos.AddScoped<IUnidadeDeTrabalho, Codex.Infrastructure.Repositories.UnidadeDeTrabalhoInterna>();
        servicos.AddScoped<IProvedorServicoInterno, Codex.Application.Services.Interno.ProvedorServicoInterno>();

        return servicos.BuildServiceProvider();
    }

    [Fact]
    public async Task CriarEPublicarProvedor_ViaServico_PropagaParaItensBuscaNaMesmaOperacao()
    {
        await using var provedorDeServicos = ConstruirProvedorDeServicos();
        using var escopo = provedorDeServicos.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<IProvedorServicoInterno>();

        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var criado = await servico.CriarAsync(new CriarProvedorDto
        {
            Nome = $"Provedor Serviço {sufixo}",
            IdentificadorInvoicy = $"INV-{sufixo}",
            Categoria = CategoriaProvedor.Nacional,
            Aliases = ["alias um", "alias dois"],
        }, CancellationToken.None);

        // Após CriarAsync, já deve existir a linha de projeção (AtualizarPesosAsync), ainda não publicada.
        var publicadoAposCriar = await ObterPublicadoDoItemBuscaAsync(TipoItemIndexado.provedor, criado.Id);
        publicadoAposCriar.Should().Be(false, because: "provedor recém-criado nasce despublicado (M19)");

        var pesoBAposCriar = await ObterPesoBDoItemBuscaAsync(TipoItemIndexado.provedor, criado.Id);
        pesoBAposCriar.Should().Contain(criado.Nome).And.Contain("alias um").And.Contain("alias dois");

        var publicado = await servico.AtualizarPublicacaoAsync(criado.Id, publicado: true, criado.VersaoConcorrencia, CancellationToken.None);
        publicado.Publicado.Should().BeTrue();

        var publicadoAposPublicar = await ObterPublicadoDoItemBuscaAsync(TipoItemIndexado.provedor, criado.Id);
        publicadoAposPublicar.Should().Be(true,
            because: "AtualizarPublicacaoAsync precisa propagar para codex.itensbusca na mesma operação, via IUnidadeDeTrabalho + IIndiceBuscaWriter.RecalcularVisibilidadeAsync");
    }

    [Fact]
    public async Task CriarAvisoDeProvedor_ViaServico_CriaLinhaDeProjecaoDespublicada()
    {
        await using var provedorDeServicos = ConstruirProvedorDeServicos();
        using var escopo = provedorDeServicos.CreateScope();
        var provedorServico = escopo.ServiceProvider.GetRequiredService<IProvedorServicoInterno>();

        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var provedor = await provedorServico.CriarAsync(new CriarProvedorDto
        {
            Nome = $"Provedor Aviso {sufixo}",
            IdentificadorInvoicy = $"INV-{sufixo}",
            Categoria = CategoriaProvedor.Nacional,
        }, CancellationToken.None);

        var avisoServico = new Codex.Application.Services.Interno.AvisoServicoInterno(
            escopo.ServiceProvider.GetRequiredService<IAvisoRepositorioInterno>(),
            escopo.ServiceProvider.GetRequiredService<IProvedorRepositorioInterno>(),
            new RepositorioVersaoProvedorInternoInexistente(),
            escopo.ServiceProvider.GetRequiredService<IIndiceBuscaWriter>(),
            escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>());

        var aviso = await avisoServico.CriarDeProvedorAsync(provedor.Id, new CriarAvisoDeProvedorDto
        {
            Severidade = Severidade.Alerta,
            Titulo = "Aviso via serviço",
            Corpo = "Corpo do aviso via serviço",
        }, CancellationToken.None);

        var publicado = await ObterPublicadoDoItemBuscaAsync(TipoItemIndexado.aviso, aviso.Id);
        publicado.Should().Be(false);

        var pesoC = await ObterPesoCDoItemBuscaAsync(TipoItemIndexado.aviso, aviso.Id);
        pesoC.Should().Contain("Corpo do aviso via serviço");
    }

    private async Task<bool?> ObterPublicadoDoItemBuscaAsync(TipoItemIndexado tipoItem, int itemId)
    {
        await using var contexto = _postgres.CriarContexto();
        return await contexto.ItensBusca.AsNoTracking()
            .Where(i => i.TipoItem == tipoItem && i.ItemId == itemId)
            .Select(i => (bool?)i.Publicado)
            .FirstOrDefaultAsync();
    }

    private async Task<string?> ObterPesoBDoItemBuscaAsync(TipoItemIndexado tipoItem, int itemId)
    {
        await using var contexto = _postgres.CriarContexto();
        return await contexto.ItensBusca.AsNoTracking()
            .Where(i => i.TipoItem == tipoItem && i.ItemId == itemId)
            .Select(i => i.PesoB)
            .FirstOrDefaultAsync();
    }

    private async Task<string?> ObterPesoCDoItemBuscaAsync(TipoItemIndexado tipoItem, int itemId)
    {
        await using var contexto = _postgres.CriarContexto();
        return await contexto.ItensBusca.AsNoTracking()
            .Where(i => i.TipoItem == tipoItem && i.ItemId == itemId)
            .Select(i => i.PesoC)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// <see cref="AvisoServicoInterno"/> exige <see cref="IVersaoProvedorRepositorioInterno"/> no
    /// construtor mas este teste só exercita o caminho de aviso de PROVEDOR (nunca chama o
    /// repositório de versão) — stub mínimo em vez de registrar mais um DbContext scope.
    /// </summary>
    private sealed class RepositorioVersaoProvedorInternoInexistente : IVersaoProvedorRepositorioInterno
    {
        public Task<VersaoProvedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct) =>
            throw new NotSupportedException("Não usado neste teste (só aviso de escopo provedor).");

        public Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<VersaoProvedor>> ListarTodosAtivosAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task AdicionarAsync(VersaoProvedor versao, CancellationToken ct) => throw new NotSupportedException();

        public Task SalvarAlteracoesAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
