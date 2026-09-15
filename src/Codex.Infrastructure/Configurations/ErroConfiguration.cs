using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

public sealed class ErroConfiguration : IEntityTypeConfiguration<Erro>
{
    public void Configure(EntityTypeBuilder<Erro> builder)
    {
        builder.ToTable("erros", t => t.HasCheckConstraint(
            "ck_codex_erros_origem", "origem IN ('Prefeitura', 'InvoiCy', 'Schema')"));

        builder.HasKey(x => x.Id).HasName("pk_codex_erros");
        builder.Property(x => x.Id).HasColumnName("erroid").UseIdentityAlwaysColumn();

        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CausaProvavel).HasColumnName("causaprovavel");
        builder.Property(x => x.SolucaoSugerida).HasColumnName("solucaosugerida");
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Sem UNIQUE(codigo) — deliberado (§4.9 do modelo de dados). Índice não único sustenta o atalho /erros?codigo=.
        builder.HasIndex(x => x.Codigo).HasDatabaseName("idx_codex_erros_codigo");
        builder.HasIndex(x => x.Origem).HasDatabaseName("idx_codex_erros_origem");
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_codex_erros_slug");

        builder.HasMany(x => x.Variantes)
            .WithOne()
            .HasForeignKey(x => x.ErroId)
            .HasConstraintName("fk_codex_variantesmensagem_erroid")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Variantes).UsePropertyAccessMode(PropertyAccessMode.Field);

        // VersoesAplicaveisIds/CamposRelacionadosIds são resolvidos via ErrosVersoes/ErrosCampos
        // (tabelas de junção próprias) — nunca coluna array inline nesta tabela.
        builder.Ignore(x => x.VersoesAplicaveisIds);
        builder.Ignore(x => x.CamposRelacionadosIds);
    }
}
