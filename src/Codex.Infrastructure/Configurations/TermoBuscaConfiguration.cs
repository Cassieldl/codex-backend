using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class TermoBuscaConfiguration : IEntityTypeConfiguration<TermoBusca>
{
    public void Configure(EntityTypeBuilder<TermoBusca> builder)
    {
        builder.ToTable("termosbusca", t => t.HasCheckConstraint(
            "ck_codex_termosbusca_termo_minimo", "length(termo) >= 3"));

        builder.HasKey(x => x.Id).HasName("pk_codex_termosbusca");
        builder.Property(x => x.Id).HasColumnName("termobuscaid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Termo).HasColumnName("termo").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Ocorrencias).HasColumnName("ocorrencias").HasDefaultValue(1).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        // Nome passado já no HasIndex (não só via HasDatabaseName): os dois índices abaixo têm a
        // MESMA propriedade (Termo) — sem o nome aqui, o EF trata a segunda chamada como
        // reconfiguração da primeira e mescla as anotações num único CreateIndex (mesmo bug de
        // AP-BACKEND-012 documentado em ItemBuscaConfiguration, só que via HasDatabaseName em vez
        // de omitir o nome). Confirmado contra Postgres real: o merge gerava um único índice com
        // unique:true + method gin, e GIN não suporta índice único
        // ("access method gin does not support unique indexes").
        builder.HasIndex(x => x.Termo, "uq_codex_termosbusca_termo").IsUnique();

        builder.HasIndex(x => x.Termo, "idx_codex_termosbusca_termo_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
