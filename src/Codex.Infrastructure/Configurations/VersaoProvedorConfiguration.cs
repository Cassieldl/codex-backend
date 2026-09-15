using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class VersaoProvedorConfiguration : IEntityTypeConfiguration<VersaoProvedor>
{
    public void Configure(EntityTypeBuilder<VersaoProvedor> builder)
    {
        builder.ToTable("versoesprovedor");

        builder.HasKey(x => x.Id).HasName("pk_codex_versoesprovedor");
        builder.Property(x => x.Id).HasColumnName("versaoprovedorid").UseIdentityAlwaysColumn();

        builder.Property(x => x.ProvedorId).HasColumnName("provedorid").IsRequired();
        builder.Property(x => x.VersaoLayoutId).HasColumnName("versaolayoutid").IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.VerificadoEm).HasColumnName("verificadoem");
        builder.Property(x => x.Introducao).HasColumnName("introducao");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.ProvedorId, x.Slug }).IsUnique().HasDatabaseName("uq_codex_versoesprovedor_provedorid_slug");
        builder.HasIndex(x => x.ProvedorId).HasDatabaseName("idx_codex_versoesprovedor_provedorid");
        builder.HasIndex(x => x.VersaoLayoutId).HasDatabaseName("idx_codex_versoesprovedor_versaolayoutid");

        builder.HasOne<Provedor>()
            .WithMany()
            .HasForeignKey(x => x.ProvedorId)
            .HasConstraintName("fk_codex_versoesprovedor_provedorid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoLayout>()
            .WithMany()
            .HasForeignKey(x => x.VersaoLayoutId)
            .HasConstraintName("fk_codex_versoesprovedor_versaolayoutid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
