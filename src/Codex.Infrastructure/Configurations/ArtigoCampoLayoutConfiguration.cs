using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>Migrada na 002 (decisoes.md U-1, modelo-dados.md §2.3).</summary>
public sealed class ArtigoCampoLayoutConfiguration : IEntityTypeConfiguration<ArtigoCampoLayout>
{
    public void Configure(EntityTypeBuilder<ArtigoCampoLayout> builder)
    {
        builder.ToTable("artigoscamposlayout", CodexDbContext.Schema);

        builder.HasKey(x => new { x.ArtigoId, x.CampoLayoutId }).HasName("pk_codex_artigoscamposlayout");

        builder.Property(x => x.ArtigoId).HasColumnName("artigoid");
        builder.Property(x => x.CampoLayoutId).HasColumnName("campolayoutid");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.CampoLayoutId).HasDatabaseName("idx_codex_artigoscamposlayout_campolayoutid");

        builder.HasOne<Artigo>()
            .WithMany()
            .HasForeignKey(x => x.ArtigoId)
            .HasConstraintName("fk_codex_artigoscamposlayout_artigoid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CampoLayout>()
            .WithMany()
            .HasForeignKey(x => x.CampoLayoutId)
            .HasConstraintName("fk_codex_artigoscamposlayout_campolayoutid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
