using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class CampoLayoutConfiguration : IEntityTypeConfiguration<CampoLayout>
{
    public void Configure(EntityTypeBuilder<CampoLayout> builder)
    {
        builder.ToTable("camposlayout", t =>
        {
            t.HasCheckConstraint("ck_codex_camposlayout_estado", "estado IN ('Ativo', 'Descontinuado')");
            // Replicado aqui só para o EF entender o schema — criação real é SQL bruto na migration (§3.1 da análise).
            t.HasCheckConstraint(
                "ck_codex_camposlayout_descontinuado_consistente",
                "(estado = 'Ativo' AND versaolayoutdescontinuadoid IS NULL) OR " +
                "(estado = 'Descontinuado' AND versaolayoutdescontinuadoid IS NOT NULL)");
        });

        builder.HasKey(x => x.Id).HasName("pk_codex_camposlayout");
        builder.Property(x => x.Id).HasColumnName("campolayoutid").UseIdentityAlwaysColumn();

        builder.Property(x => x.VersaoLayoutId).HasColumnName("versaolayoutid").IsRequired();
        builder.Property(x => x.VersaoLayoutDescontinuadoId).HasColumnName("versaolayoutdescontinuadoid").IsRequired(false);
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(LimitesCampoLayout.Nome).IsRequired();
        builder.Property(x => x.CaminhoXml).HasColumnName("caminhoxml").HasMaxLength(LimitesCampoLayout.ColunaCaminhoXml).IsRequired();
        builder.Property(x => x.Significado).HasColumnName("significado").IsRequired();
        builder.Property(x => x.Grupo).HasColumnName("grupo").HasMaxLength(LimitesCampoLayout.Grupo);
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(LimitesCampoLayout.Slug).IsRequired();
        builder.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_codex_camposlayout_slug");
        builder.HasIndex(x => x.CaminhoXml).IsUnique().HasDatabaseName("uq_codex_camposlayout_caminhoxml");
        builder.HasIndex(x => x.VersaoLayoutId).HasDatabaseName("idx_codex_camposlayout_versaolayoutid");
        builder.HasIndex(x => x.VersaoLayoutDescontinuadoId).HasDatabaseName("idx_codex_camposlayout_versaolayoutdescontinuadoid");
        builder.HasIndex(x => x.Estado).HasDatabaseName("idx_codex_camposlayout_estado");

        // Segunda relação com VersaoLayout, além da origem — FK de descontinuação. A FK de
        // origem (VersaoLayoutOrigem) é configurada em VersaoLayoutConfiguration.HasMany, para
        // não duplicar a definição da mesma relação em dois lugares.
        builder.HasOne<VersaoLayout>()
            .WithMany()
            .HasForeignKey(x => x.VersaoLayoutDescontinuadoId)
            .HasConstraintName("fk_codex_camposlayout_versaolayoutdescontinuadoid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
