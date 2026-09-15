using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class ErroCampoConfiguration : IEntityTypeConfiguration<ErroCampo>
{
    public void Configure(EntityTypeBuilder<ErroCampo> builder)
    {
        builder.ToTable("erroscampos");

        builder.HasKey(x => new { x.ErroId, x.CampoLayoutId }).HasName("pk_codex_erroscampos");

        builder.Property(x => x.ErroId).HasColumnName("erroid");
        builder.Property(x => x.CampoLayoutId).HasColumnName("campolayoutid");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.CampoLayoutId).HasDatabaseName("idx_codex_erroscampos_campolayoutid");

        builder.HasOne<Erro>()
            .WithMany()
            .HasForeignKey(x => x.ErroId)
            .HasConstraintName("fk_codex_erroscampos_erroid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CampoLayout>()
            .WithMany()
            .HasForeignKey(x => x.CampoLayoutId)
            .HasConstraintName("fk_codex_erroscampos_campolayoutid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
