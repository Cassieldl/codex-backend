using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>Migrada na 002 (decisoes.md U-1/U-2, modelo-dados.md §2.1).</summary>
public sealed class ComunicadoConfiguration : IEntityTypeConfiguration<Comunicado>
{
    public void Configure(EntityTypeBuilder<Comunicado> builder)
    {
        builder.ToTable("comunicados", CodexDbContext.Schema, t =>
        {
            t.HasCheckConstraint("ck_codex_comunicados_severidade", "severidade IN ('Critico', 'Alerta', 'Informativo')");
            t.HasCheckConstraint("ck_codex_comunicados_abrangencia", "abrangencia IN ('Global', 'Provedor', 'ProvedorVersao')");
            t.HasCheckConstraint(
                "ck_codex_comunicados_abrangencia_consistente",
                "(abrangencia = 'Global' AND provedorid IS NULL AND versaoprovedorid IS NULL) OR " +
                "(abrangencia = 'Provedor' AND provedorid IS NOT NULL AND versaoprovedorid IS NULL) OR " +
                "(abrangencia = 'ProvedorVersao' AND provedorid IS NOT NULL AND versaoprovedorid IS NOT NULL)");
        });

        builder.HasKey(x => x.Id).HasName("pk_codex_comunicados");
        builder.Property(x => x.Id).HasColumnName("comunicadoid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(LimitesComunicado.Titulo).IsRequired();
        builder.Property(x => x.Corpo).HasColumnName("corpo").IsRequired();
        builder.Property(x => x.Severidade).HasColumnName("severidade").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Abrangencia).HasColumnName("abrangencia").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.ProvedorId).HasColumnName("provedorid").IsRequired(false);
        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired(false);
        builder.Property(x => x.ExpiraEm).HasColumnName("expiraem").IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.ProvedorId).HasDatabaseName("idx_codex_comunicados_provedorid");
        builder.HasIndex(x => x.VersaoProvedorId).HasDatabaseName("idx_codex_comunicados_versaoprovedorid");
        builder.HasIndex(x => x.ExpiraEm).HasDatabaseName("idx_codex_comunicados_expiraem");

        // Sustenta ComunicadoRepositorio.QueryVigentes nas duas direções (leitura "não venceu" e
        // job de expiração "já venceu") — o predicado do índice é IDÊNTICO às duas condições
        // estáveis da consulta (publicado AND desativadoem IS NULL); expiraem é a chave do range
        // scan (modelo-dados.md §2.1/§7).
        builder.HasIndex(x => x.ExpiraEm)
            .HasDatabaseName("idx_codex_comunicados_vigentes")
            .HasFilter("publicado AND desativadoem IS NULL");
    }
}
