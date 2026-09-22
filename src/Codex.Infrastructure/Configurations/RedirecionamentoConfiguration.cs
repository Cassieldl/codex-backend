using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class RedirecionamentoConfiguration : IEntityTypeConfiguration<Redirecionamento>
{
    public void Configure(EntityTypeBuilder<Redirecionamento> builder)
    {
        builder.ToTable("redirecionamentos", t => t.HasCheckConstraint(
            "ck_codex_redirecionamentos_tipoitem",
            "tipoitem IN ('provedor', 'versaoprovedor', 'erro', 'campolayout', 'artigo')"));

        builder.HasKey(x => x.Id).HasName("pk_codex_redirecionamentos");
        builder.Property(x => x.Id).HasColumnName("redirecionamentoid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(400).IsRequired();
        builder.Property(x => x.Destino).HasColumnName("destino").HasMaxLength(400).IsRequired();
        builder.Property(x => x.TipoItem).HasColumnName("tipoitem").HasMaxLength(20).HasConversion<string>().IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.Origem).IsUnique().HasDatabaseName("uq_codex_redirecionamentos_origem");
        builder.HasIndex(x => x.TipoItem).HasDatabaseName("idx_codex_redirecionamentos_tipoitem");
    }
}
