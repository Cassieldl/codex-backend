using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class NaturezaOperacaoConfiguration : IEntityTypeConfiguration<NaturezaOperacao>
{
    public void Configure(EntityTypeBuilder<NaturezaOperacao> builder)
    {
        builder.ToTable("naturezasoperacao", CodexDbContext.Schema, t => t.HasCheckConstraint(
            "ck_codex_naturezasoperacao_codigoinvoicy",
            $"codigoinvoicy BETWEEN 0 AND {LimitesNaturezaOperacao.CodigoInvoicyMaximo}"));

        builder.HasKey(x => x.Id).HasName("pk_codex_naturezasoperacao");
        builder.Property(x => x.Id).HasColumnName("naturezasoperacaoid").UseIdentityAlwaysColumn();

        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired();
        builder.Property(x => x.CodigoInvoicy).HasColumnName("codigoinvoicy").HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(LimitesNaturezaOperacao.Descricao).IsRequired();
        builder.Property(x => x.ValorProvedor).HasColumnName("valorprovedor").HasMaxLength(LimitesNaturezaOperacao.ValorProvedor).IsRequired();
        builder.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(LimitesNaturezaOperacao.Observacao);
        builder.Property(x => x.AConfirmar).HasColumnName("aconfirmar").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").HasColumnType("smallint").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => new { x.VersaoProvedorId, x.CodigoInvoicy })
            .IsUnique()
            .HasDatabaseName("uq_codex_naturezasoperacao_versaoprovedorid_codigoinvoicy");

        builder.HasOne<VersaoProvedor>()
            .WithMany()
            .HasForeignKey(x => x.VersaoProvedorId)
            .HasConstraintName("fk_codex_naturezasoperacao_versaoprovedorid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
