using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class AliasProvedorConfiguration : IEntityTypeConfiguration<AliasProvedor>
{
    public void Configure(EntityTypeBuilder<AliasProvedor> builder)
    {
        builder.ToTable("aliasesprovedor");

        builder.HasKey(x => x.Id).HasName("pk_codex_aliasesprovedor");
        builder.Property(x => x.Id).HasColumnName("aliasprovedorid").UseIdentityAlwaysColumn();

        builder.Property(x => x.ProvedorId).HasColumnName("provedorid").IsRequired();
        builder.Property(x => x.Alias).HasColumnName("alias").HasMaxLength(LimitesProvedor.Alias).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => new { x.ProvedorId, x.Alias }).IsUnique().HasDatabaseName("uq_codex_aliasesprovedor_provedorid_alias");
        builder.HasIndex(x => x.ProvedorId).HasDatabaseName("idx_codex_aliasesprovedor_provedorid");
    }
}
