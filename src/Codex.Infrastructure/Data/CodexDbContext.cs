using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Data;

/// <summary>
/// DbContext único do Codex — schema <c>codex</c> (PostgreSQL). Todas as entidades do caminho
/// crítico (modelo-dados.md §4) mais as editoriais, que a 001 deixou "modeladas, não migradas" e a
/// **002 migrou** (`AdicionarConteudoEditorial`): comunicado, artigo e as duas juntoras.
///
/// <para>
/// Segue com <c>ExcludeFromMigrations()</c> apenas <c>Exemplo</c> — modelado na 001 e previsto
/// para a demanda 003. Ao promovê-lo, atenção: <c>ExcludeFromMigrations()</c> <b>não é reversível
/// por <c>dotnet ef migrations add</c></b> — o differ só vê a anotação mudar e gera migration sem
/// <c>CreateTable</c>, que aplica sem criar tabela e só falha no primeiro <c>INSERT</c>. Ver
/// <c>rules/database.md</c>, "Gotchas de EF Core + Npgsql".
/// </para>
/// </summary>
public sealed class CodexDbContext : DbContext
{
    public const string Schema = "codex";

    public CodexDbContext(DbContextOptions<CodexDbContext> options)
        : base(options)
    {
    }

    // Caminho crítico (migram na 001)
    public DbSet<VersaoLayout> VersoesLayout => Set<VersaoLayout>();
    public DbSet<CampoLayout> CamposLayout => Set<CampoLayout>();
    public DbSet<Provedor> Provedores => Set<Provedor>();
    public DbSet<AliasProvedor> AliasesProvedor => Set<AliasProvedor>();
    public DbSet<VersaoProvedor> VersoesProvedor => Set<VersaoProvedor>();
    public DbSet<CampoVersao> CamposVersao => Set<CampoVersao>();
    public DbSet<Aviso> Avisos => Set<Aviso>();
    public DbSet<AvisoCampo> AvisosCampos => Set<AvisoCampo>();
    public DbSet<Erro> Erros => Set<Erro>();
    public DbSet<VarianteMensagem> VariantesMensagem => Set<VarianteMensagem>();
    public DbSet<ErroVersao> ErrosVersoes => Set<ErroVersao>();
    public DbSet<ErroCampo> ErrosCampos => Set<ErroCampo>();
    public DbSet<ItemBusca> ItensBusca => Set<ItemBusca>();
    public DbSet<TermoBusca> TermosBusca => Set<TermoBusca>();
    public DbSet<Redirecionamento> Redirecionamentos => Set<Redirecionamento>();

    // Migradas na 002 (AdicionarConteudoEditorial) — saíram de ExcludeFromMigrations().
    public DbSet<Comunicado> Comunicados => Set<Comunicado>();
    public DbSet<Artigo> Artigos => Set<Artigo>();
    public DbSet<ArtigoCampoLayout> ArtigosCamposLayout => Set<ArtigoCampoLayout>();
    public DbSet<ArtigoErro> ArtigosErros => Set<ArtigoErro>();
    public DbSet<Exemplo> Exemplos => Set<Exemplo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodexDbContext).Assembly);
    }
}
