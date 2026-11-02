using Codex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codex.Infrastructure.Configurations;

/// <summary>
/// Divide <c>codex.versoeslayout</c> entre <see cref="VersaoLayout"/> e a coluna <c>estrutura</c>:
/// mesma linha e mesma chave, mas a árvore só vem quando esta entidade é consultada.
/// </summary>
public sealed class EstruturaVersaoLayoutConfiguration : IEntityTypeConfiguration<EstruturaVersaoLayout>
{
    public void Configure(EntityTypeBuilder<EstruturaVersaoLayout> builder)
    {
        builder.ToTable("versoeslayout");

        builder.HasKey(x => x.VersaoLayoutId).HasName("pk_codex_versoeslayout");
        builder.Property(x => x.VersaoLayoutId).HasColumnName("versaolayoutid");

        builder.Property(x => x.Conteudo).HasColumnName("estrutura").HasColumnType("jsonb");

        // Tabela dividida exige o mesmo token de concorrência nas duas entidades.
        builder.Property(x => x.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<VersaoLayout>()
            .WithOne()
            .HasForeignKey<EstruturaVersaoLayout>(x => x.VersaoLayoutId);
    }
}
