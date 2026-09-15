using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class CampoVersaoConfiguration : IEntityTypeConfiguration<CampoVersao>
{
    public void Configure(EntityTypeBuilder<CampoVersao> builder)
    {
        builder.ToTable("camposversao", t => t.HasCheckConstraint(
            "ck_codex_camposversao_estadopreenchimento",
            "estadopreenchimento IN ('Obrigatorio', 'Opcional', 'Condicional')"));

        builder.HasKey(x => x.Id).HasName("pk_codex_camposversao");
        builder.Property(x => x.Id).HasColumnName("campoversaoid").UseIdentityAlwaysColumn();

        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired();
        // U4 — FK obrigatória, sem exceção. Não existe overload de CampoVersao.Criar sem este parâmetro.
        builder.Property(x => x.CampoLayoutId).HasColumnName("campolayoutid").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Tamanho).HasColumnName("tamanho").HasMaxLength(40);
        builder.Property(x => x.Mascara).HasColumnName("mascara").HasMaxLength(120);
        builder.Property(x => x.Dominio).HasColumnName("dominio");
        builder.Property(x => x.EstadoPreenchimento).HasColumnName("estadopreenchimento").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Condicao).HasColumnName("condicao");
        builder.Property(x => x.Exemplo).HasColumnName("exemplo");
        builder.Property(x => x.NotaPreenchimento).HasColumnName("notapreenchimento");
        builder.Property(x => x.DestinoXml).HasColumnName("destinoxml").HasMaxLength(400);
        builder.Property(x => x.NotaTransformacao).HasColumnName("notatransformacao");
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.VersaoProvedorId, x.CampoLayoutId }).IsUnique().HasDatabaseName("uq_codex_camposversao_versaoprovedorid_campolayoutid");
        builder.HasIndex(x => x.VersaoProvedorId).HasDatabaseName("idx_codex_camposversao_versaoprovedorid");
        builder.HasIndex(x => x.CampoLayoutId).HasDatabaseName("idx_codex_camposversao_campolayoutid");

        builder.HasOne<VersaoProvedor>()
            .WithMany()
            .HasForeignKey(x => x.VersaoProvedorId)
            .HasConstraintName("fk_codex_camposversao_versaoprovedorid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CampoLayout>()
            .WithMany()
            .HasForeignKey(x => x.CampoLayoutId)
            .HasConstraintName("fk_codex_camposversao_campolayoutid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
