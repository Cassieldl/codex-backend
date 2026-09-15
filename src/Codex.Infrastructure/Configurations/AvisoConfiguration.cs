using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class AvisoConfiguration : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> builder)
    {
        builder.ToTable("avisos", t =>
        {
            t.HasCheckConstraint("ck_codex_avisos_severidade", "severidade IN ('Critico', 'Alerta', 'Informativo')");
            // U8 — replicado aqui só para o EF entender o schema; criação real é SQL bruto na migration.
            t.HasCheckConstraint(
                "ck_codex_avisos_escopo_xor",
                "(provedorid IS NOT NULL AND versaoprovedorid IS NULL) OR " +
                "(provedorid IS NULL AND versaoprovedorid IS NOT NULL)");
        });

        builder.HasKey(x => x.Id).HasName("pk_codex_avisos");
        builder.Property(x => x.Id).HasColumnName("avisoid").UseIdentityAlwaysColumn();

        builder.Property(x => x.ProvedorId).HasColumnName("provedorid").IsRequired(false);
        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired(false);
        builder.Property(x => x.Severidade).HasColumnName("severidade").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Corpo).HasColumnName("corpo").IsRequired();
        builder.Property(x => x.VigenciaInicio).HasColumnName("vigenciainicio").HasDefaultValueSql("CURRENT_DATE").IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Coleção de ids de campos afetados — persistida via codex.avisoscampos (AvisoCampoConfiguration),
        // nunca uma coluna própria. Ignorada aqui: quem materializa a junção é o service/repositório.
        builder.Ignore(x => x.CamposAfetadosIds);

        builder.HasIndex(x => x.ProvedorId).HasDatabaseName("idx_codex_avisos_provedorid");
        builder.HasIndex(x => x.VersaoProvedorId).HasDatabaseName("idx_codex_avisos_versaoprovedorid");
        builder.HasIndex(x => x.Severidade).HasDatabaseName("idx_codex_avisos_severidade");

        builder.HasOne<Provedor>()
            .WithMany()
            .HasForeignKey(x => x.ProvedorId)
            .HasConstraintName("fk_codex_avisos_provedorid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoProvedor>()
            .WithMany()
            .HasForeignKey(x => x.VersaoProvedorId)
            .HasConstraintName("fk_codex_avisos_versaoprovedorid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
