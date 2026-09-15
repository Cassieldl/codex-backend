using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class AvisoCampoConfiguration : IEntityTypeConfiguration<AvisoCampo>
{
    public void Configure(EntityTypeBuilder<AvisoCampo> builder)
    {
        builder.ToTable("avisoscampos");

        builder.HasKey(x => new { x.AvisoId, x.CampoVersaoId }).HasName("pk_codex_avisoscampos");

        builder.Property(x => x.AvisoId).HasColumnName("avisoid");
        builder.Property(x => x.CampoVersaoId).HasColumnName("campoversaoid");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.CampoVersaoId).HasDatabaseName("idx_codex_avisoscampos_campoversaoid");

        builder.HasOne<Aviso>()
            .WithMany()
            .HasForeignKey(x => x.AvisoId)
            .HasConstraintName("fk_codex_avisoscampos_avisoid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CampoVersao>()
            .WithMany()
            .HasForeignKey(x => x.CampoVersaoId)
            .HasConstraintName("fk_codex_avisoscampos_campoversaoid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
