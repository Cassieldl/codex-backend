using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class ErroVersaoConfiguration : IEntityTypeConfiguration<ErroVersao>
{
    public void Configure(EntityTypeBuilder<ErroVersao> builder)
    {
        builder.ToTable("errosversoes");

        builder.HasKey(x => new { x.ErroId, x.VersaoProvedorId }).HasName("pk_codex_errosversoes");

        builder.Property(x => x.ErroId).HasColumnName("erroid");
        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.VersaoProvedorId).HasDatabaseName("idx_codex_errosversoes_versaoprovedorid");

        builder.HasOne<Erro>()
            .WithMany()
            .HasForeignKey(x => x.ErroId)
            .HasConstraintName("fk_codex_errosversoes_erroid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoProvedor>()
            .WithMany()
            .HasForeignKey(x => x.VersaoProvedorId)
            .HasConstraintName("fk_codex_errosversoes_versaoprovedorid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
