using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class VersaoLayoutConfiguration : IEntityTypeConfiguration<VersaoLayout>
{
    public void Configure(EntityTypeBuilder<VersaoLayout> builder)
    {
        builder.ToTable("versoeslayout", t => t.HasCheckConstraint(
            "ck_codex_versoeslayout_estado", "estado IN ('Prevista', 'Vigente', 'Anterior')"));

        builder.HasKey(x => x.Id).HasName("pk_codex_versoeslayout");
        builder.Property(x => x.Id).HasColumnName("versaolayoutid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(LimitesVersaoLayout.Rotulo).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(LimitesVersaoLayout.Slug).IsRequired();
        builder.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao");

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        // Concorrência otimista via xmin (coluna de sistema do Postgres) — nunca coluna própria.
        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_codex_versoeslayout_slug");
        builder.HasIndex(x => x.Ordem).IsUnique().HasDatabaseName("uq_codex_versoeslayout_ordem");

        // Único Vigente por vez — índice único PARCIAL (CHECK/constraint de tabela não suporta WHERE).
        builder.HasIndex(x => x.Estado)
            .IsUnique()
            .HasDatabaseName("uq_codex_versoeslayout_estado_vigente")
            .HasFilter("estado = 'Vigente' AND desativadoem IS NULL");

        builder.HasMany(x => x.CamposOrigem)
            .WithOne(x => x.VersaoLayoutOrigem)
            .HasForeignKey(x => x.VersaoLayoutId)
            .HasConstraintName("fk_codex_camposlayout_versaolayoutid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
