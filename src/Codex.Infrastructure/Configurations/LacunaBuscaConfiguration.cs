using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>
/// Fila editorial (escopo funcional v3.1 §9.2): buscas internas sem resultado, com parceiro e
/// chamado. Os tetos vêm de <see cref="LimitesLacunaBusca"/>, o mesmo que o validator usa.
/// </summary>
public sealed class LacunaBuscaConfiguration : IEntityTypeConfiguration<LacunaBusca>
{
    public void Configure(EntityTypeBuilder<LacunaBusca> builder)
    {
        builder.ToTable("lacunasbusca", CodexDbContext.Schema, t =>
        {
            t.HasCheckConstraint("ck_codex_lacunasbusca_estado", "estado IN ('Aberta', 'Resolvida', 'Descartada')");
            t.HasCheckConstraint(
                "ck_codex_lacunasbusca_resolucao_consistente",
                "(estado = 'Aberta' AND resolvidaem IS NULL AND resolvidoporid IS NULL) OR " +
                "(estado <> 'Aberta' AND resolvidaem IS NOT NULL AND resolvidoporid IS NOT NULL)");
            t.HasCheckConstraint("ck_codex_lacunasbusca_termo_minimo", $"length(btrim(termo)) >= {LimitesLacunaBusca.TermoMinimo}");
        });

        builder.HasKey(x => x.Id).HasName("pk_codex_lacunasbusca");
        builder.Property(x => x.Id).HasColumnName("lacunabuscaid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Termo).HasColumnName("termo").HasMaxLength(LimitesLacunaBusca.Termo).IsRequired();
        builder.Property(x => x.TermoNormalizado).HasColumnName("termonormalizado").HasMaxLength(LimitesLacunaBusca.Termo).IsRequired();
        builder.Property(x => x.Parceiro).HasColumnName("parceiro").HasMaxLength(LimitesLacunaBusca.Parceiro);
        builder.Property(x => x.NumeroChamado).HasColumnName("numerochamado").HasMaxLength(LimitesLacunaBusca.NumeroChamado);
        builder.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Nota).HasColumnName("nota").HasMaxLength(LimitesLacunaBusca.Nota);

        builder.Property(x => x.RegistradoPorId).HasColumnName("registradoporid").HasMaxLength(LimitesLacunaBusca.UsuarioId).IsRequired();
        builder.Property(x => x.RegistradoPorNome).HasColumnName("registradopornome").HasMaxLength(LimitesLacunaBusca.UsuarioNome);
        builder.Property(x => x.ResolvidaEm).HasColumnName("resolvidaem");
        builder.Property(x => x.ResolvidoPorId).HasColumnName("resolvidoporid").HasMaxLength(LimitesLacunaBusca.UsuarioId);
        builder.Property(x => x.ResolvidoPorNome).HasColumnName("resolvidopornome").HasMaxLength(LimitesLacunaBusca.UsuarioNome);

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Listagem padrão: filtro por estado, mais recentes primeiro.
        builder.HasIndex(x => new { x.Estado, x.CriadoEm }).HasDatabaseName("idx_codex_lacunasbusca_estado_criadoem");

        // Contagem de recorrência por termo (e resolução em lote do mesmo termo).
        builder.HasIndex(x => new { x.TermoNormalizado, x.Estado }).HasDatabaseName("idx_codex_lacunasbusca_termonormalizado_estado");
    }
}
