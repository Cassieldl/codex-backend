using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class VarianteMensagemConfiguration : IEntityTypeConfiguration<VarianteMensagem>
{
    public void Configure(EntityTypeBuilder<VarianteMensagem> builder)
    {
        builder.ToTable("variantesmensagem");

        builder.HasKey(x => x.Id).HasName("pk_codex_variantesmensagem");
        builder.Property(x => x.Id).HasColumnName("variantemensagemid").UseIdentityAlwaysColumn();

        builder.Property(x => x.ErroId).HasColumnName("erroid").IsRequired();
        builder.Property(x => x.Texto).HasColumnName("texto").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => x.ErroId).HasDatabaseName("idx_codex_variantesmensagem_erroid");
    }
}
