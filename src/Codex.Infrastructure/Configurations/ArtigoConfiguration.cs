using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>Migrada na 002 (decisoes.md U-1/U-3, modelo-dados.md §2.2).</summary>
public sealed class ArtigoConfiguration : IEntityTypeConfiguration<Artigo>
{
    public void Configure(EntityTypeBuilder<Artigo> builder)
    {
        builder.ToTable("artigos", CodexDbContext.Schema, t =>
        {
            t.HasCheckConstraint("ck_codex_artigos_tipo", "tipo IN ('Conceito', 'Procedimento', 'Mudanca')");
        });

        builder.HasKey(x => x.Id).HasName("pk_codex_artigos");
        builder.Property(x => x.Id).HasColumnName("artigoid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Corpo).HasColumnName("corpo").IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.VerificadoEm).HasColumnName("verificadoem");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_codex_artigos_slug");
        builder.HasIndex(x => x.Tipo).HasDatabaseName("idx_codex_artigos_tipo");
        builder.HasIndex(x => x.Publicado).HasDatabaseName("idx_codex_artigos_publicado");
    }
}
