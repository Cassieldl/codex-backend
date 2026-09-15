using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Codex.Infrastructure.Configurations;

/// <summary>
/// Projeção materializada de busca (modelo-dados.md §5.6). As três colunas geradas
/// (<c>textobusca</c>, <c>documento</c>, <c>visivelpublico</c>) são configuradas aqui via
/// <c>HasComputedColumnSql(..., stored: true)</c> com o SQL copiado verbatim do modelo de
/// dados — para o EF entender o schema e permitir consulta via LINQ — mas nenhuma delas é
/// escrita pelo C# em runtime (<c>ValueGeneratedOnAddOrUpdate</c>). <c>documento</c> (tsvector)
/// não é exposta como propriedade no Domain (tipo específico do Npgsql) — é shadow property,
/// mapeada só aqui em Infrastructure.
/// </summary>
public sealed class ItemBuscaConfiguration : IEntityTypeConfiguration<ItemBusca>
{
    // concat_ws é STABLE (não IMMUTABLE) — Postgres recusa função não-imutável em coluna
    // GERADA/STORED ("generation expression is not immutable"). coalesce+concatenação é
    // IMMUTABLE e produz o mesmo resultado normalizado: normalizar_busca já colapsa espaços
    // extras (regexp_replace '\s+' -> ' ' + trim) e o terceiro segmento remove todo não
    // alfanumérico, então os separadores redundantes de valores NULL somem de qualquer forma.
    private const string SqlTextoBusca =
        "codex.normalizar_busca(coalesce(pesoa, '') || ' ' || coalesce(pesob, '') || ' ' || coalesce(pesoc, '') || ' ' || coalesce(pesod, ''))";

    private const string SqlDocumento =
        "setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesoa, ''))), 'A') || " +
        "setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesob, ''))), 'B') || " +
        "setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesoc, ''))), 'C') || " +
        "setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesod, ''))), 'D')";

    private const string SqlVisivelPublico =
        "publicado AND desativadoem IS NULL AND tipoitem <> 'campolayout'";

    public void Configure(EntityTypeBuilder<ItemBusca> builder)
    {
        builder.ToTable("itensbusca", t => t.HasCheckConstraint(
            "ck_codex_itensbusca_tipoitem",
            "tipoitem IN ('erro', 'provedor', 'versaoprovedor', 'campolayout', 'aviso', 'artigo')"));

        builder.HasKey(x => x.Id).HasName("pk_codex_itensbusca");
        builder.Property(x => x.Id).HasColumnName("itembuscaid").UseIdentityAlwaysColumn();

        builder.Property(x => x.TipoItem).HasColumnName("tipoitem").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("itemid").IsRequired();
        builder.Property(x => x.Chave).HasColumnName("chave").HasMaxLength(80);
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Subtitulo).HasColumnName("subtitulo").HasMaxLength(300);
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(400).IsRequired();
        builder.Property(x => x.Publicado).HasColumnName("publicado").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.ProvedorId).HasColumnName("provedorid").IsRequired(false);
        builder.Property(x => x.VersaoProvedorId).HasColumnName("versaoprovedorid").IsRequired(false);

        builder.Property(x => x.ProvedorIds)
            .HasField("_provedorIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("provedorids")
            .HasColumnType("integer[]")
            .HasDefaultValueSql("'{}'::integer[]")
            .IsRequired();

        builder.Property(x => x.PesoA).HasColumnName("pesoa");
        builder.Property(x => x.PesoB).HasColumnName("pesob");
        builder.Property(x => x.PesoC).HasColumnName("pesoc");
        builder.Property(x => x.PesoD).HasColumnName("pesod");

        builder.Property(x => x.TextoBusca)
            .HasColumnName("textobusca")
            .HasComputedColumnSql(SqlTextoBusca, stored: true)
            .ValueGeneratedOnAddOrUpdate();

        builder.Property<NpgsqlTsVector>("Documento")
            .HasColumnName("documento")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(SqlDocumento, stored: true)
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(x => x.VisivelPublico)
            .HasColumnName("visivelpublico")
            .HasComputedColumnSql(SqlVisivelPublico, stored: true)
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(x => x.CriadoEm).HasColumnName("criadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizadoem").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DesativadoEm).HasColumnName("desativadoem");

        builder.HasIndex(x => new { x.TipoItem, x.ItemId }).IsUnique().HasDatabaseName("uq_codex_itensbusca_tipoitem_itemid");

        // GIN parcial — busca PÚBLICA (P1). Não repetir "desativadoem IS NULL"/"tipoitem <> campolayout"
        // aqui: já estão incorporados em visivelpublico (§3.2 da análise de backend).
        // Nome passado já no HasIndex (não só via HasDatabaseName): os dois índices abaixo têm a
        // MESMA lista de propriedades ("Documento") — sem o nome aqui, o EF trata a segunda
        // chamada como reconfiguração da primeira e um dos dois desaparece silenciosamente do
        // model (já aconteceu ao gerar esta migration — ver ConcorrenciaOtimistaTests/EstruturaBancoTests).
        builder.HasIndex(["Documento"], "idx_codex_itensbusca_documento_publico")
            .HasMethod("gin")
            .HasFilter("visivelpublico");

        // GIN completo (sem filtro de visivelpublico) — busca INTERNA (M15), enxerga rascunho.
        builder.HasIndex(["Documento"], "idx_codex_itensbusca_documento_interno")
            .HasMethod("gin")
            .HasFilter("desativadoem IS NULL");

        builder.HasIndex(x => x.TextoBusca)
            .HasDatabaseName("idx_codex_itensbusca_textobusca_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(x => new { x.TipoItem, x.ProvedorId })
            .HasDatabaseName("idx_codex_itensbusca_tipoitem_provedorid");

        builder.HasIndex(x => x.ProvedorIds)
            .HasDatabaseName("idx_codex_itensbusca_provedorids")
            .HasMethod("gin");

        builder.HasOne<Provedor>()
            .WithMany()
            .HasForeignKey(x => x.ProvedorId)
            .HasConstraintName("fk_codex_itensbusca_provedorid")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoProvedor>()
            .WithMany()
            .HasForeignKey(x => x.VersaoProvedorId)
            .HasConstraintName("fk_codex_itensbusca_versaoprovedorid")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
