using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class ProvedorConfiguration : IEntityTypeConfiguration<Provedor>
{
    public void Configure(EntityTypeBuilder<Provedor> builder)
    {
        builder.ToTable("provedores", t => t.HasCheckConstraint(
            "ck_codex_provedores_categoria", "categoria IN ('Nacional', 'Proprio')"));

        builder.HasKey(x => x.Id).HasName("pk_codex_provedores");
        builder.Property(x => x.Id).HasColumnName("provedorid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(160).IsRequired();
        builder.Property(x => x.IdentificadorInvoicy).HasColumnName("identificadorinvoicy").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_codex_provedores_slug");
        builder.HasIndex(x => x.IdentificadorInvoicy).IsUnique().HasDatabaseName("uq_codex_provedores_identificadorinvoicy");
        builder.HasIndex(x => x.Categoria).HasDatabaseName("idx_codex_provedores_categoria");

        builder.HasMany(x => x.Aliases)
            .WithOne()
            .HasForeignKey(x => x.ProvedorId)
            .HasConstraintName("fk_codex_aliasesprovedor_provedorid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Aliases).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
