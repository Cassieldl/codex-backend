using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>Migrada na 002 (decisoes.md U-1, modelo-dados.md §2.4).</summary>
public sealed class ArtigoErroConfiguration : IEntityTypeConfiguration<ArtigoErro>
{
    public void Configure(EntityTypeBuilder<ArtigoErro> builder)
    {
        builder.ToTable("artigoserros", CodexDbContext.Schema);

        builder.HasKey(x => new { x.ArtigoId, x.ErroId }).HasName("pk_codex_artigoserros");

        builder.Property(x => x.ArtigoId).HasColumnName("artigoid");
        builder.Property(x => x.ErroId).HasColumnName("erroid");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.ErroId).HasDatabaseName("idx_codex_artigoserros_erroid");

        builder.HasOne<Artigo>()
            .WithMany()
            .HasForeignKey(x => x.ArtigoId)
            .HasConstraintName("fk_codex_artigoserros_artigoid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Erro>()
            .WithMany()
            .HasForeignKey(x => x.ErroId)
            .HasConstraintName("fk_codex_artigoserros_erroid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
