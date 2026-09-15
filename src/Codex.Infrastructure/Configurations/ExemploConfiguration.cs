using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>Modelada, não migrada na 001 (§2.3/§6.5 do modelo de dados). Sem coluna ambiente (U3/M13).</summary>
public sealed class ExemploConfiguration : IEntityTypeConfiguration<Exemplo>
{
    public void Configure(EntityTypeBuilder<Exemplo> builder)
    {
        builder.ToTable("exemplos", CodexDbContext.Schema, t => t.ExcludeFromMigrations());

        builder.HasKey(x => x.Id).HasName("pk_codex_exemplos");
        builder.Property(x => x.Id).HasColumnName("exemploid").UseIdentityAlwaysColumn();

        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao");
        builder.Property(x => x.Xml).HasColumnName("xml").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => new { x.VersaoProvedorId, x.Nome }).IsUnique().HasDatabaseName("uq_codex_exemplos_versaoprovedorid_nome");
        builder.HasIndex(x => x.VersaoProvedorId).HasDatabaseName("idx_codex_exemplos_versaoprovedorid");
    }
}
