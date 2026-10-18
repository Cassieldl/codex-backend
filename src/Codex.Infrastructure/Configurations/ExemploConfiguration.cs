using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>
/// Migrada na 003 (U-1/M-a, modelo-dados.md §1) — saiu de <c>ExcludeFromMigrations()</c>. Sem
/// coluna <c>ambiente</c> (U3/M13). O CHECK de tamanho do XML (256 KiB, <c>octet_length</c>) é
/// guarda de catástrofe no banco, DELIBERADAMENTE diferente do teto de negócio (64 KiB,
/// caracteres, FluentValidation) — ver modelo-dados.md §1.5. Não igualar os dois números.
/// </summary>
public sealed class ExemploConfiguration : IEntityTypeConfiguration<Exemplo>
{
    public void Configure(EntityTypeBuilder<Exemplo> builder)
    {
        builder.ToTable("exemplos", CodexDbContext.Schema, t => t.HasCheckConstraint(
            "ck_codex_exemplos_xml_tamanho", "octet_length(xml) <= 262144"));

        builder.HasKey(x => x.Id).HasName("pk_codex_exemplos");
        builder.Property(x => x.Id).HasColumnName("exemploid").UseIdentityAlwaysColumn();

        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(LimitesExemplo.Slug).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(LimitesExemplo.Nome).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao");
        builder.Property(x => x.Xml).HasColumnName("xml").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.VersaoProvedorId, x.Nome }).IsUnique().HasDatabaseName("uq_codex_exemplos_versaoprovedorid_nome");
        builder.HasIndex(x => new { x.VersaoProvedorId, x.Slug }).IsUnique().HasDatabaseName("uq_codex_exemplos_versaoprovedorid_slug");
        builder.HasIndex(x => x.VersaoProvedorId).HasDatabaseName("idx_codex_exemplos_versaoprovedorid");

        builder.HasOne<VersaoProvedor>()
            .WithMany()
            .HasForeignKey(x => x.VersaoProvedorId)
            .HasConstraintName("fk_codex_exemplos_versaoprovedorid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
