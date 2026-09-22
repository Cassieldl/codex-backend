namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Payload compartilhado pelos 6 endpoints de publicação (I3b, I9, I14d, I15d, I17c, I20c — M19).
/// Nunca aceita outro campo além de <see cref="Publicado"/> — publicação só muda por este DTO
/// dedicado, nunca embutida em <c>AtualizarXDto</c> (analise-backend.md §4.2, nota "sem mass
/// assignment").
/// </summary>
public sealed record AtualizarPublicacaoDto
{
    public required bool Publicado { get; init; }
}

/// <summary>
/// Payload dos 3 endpoints de troca de slug (I37 provedor, I43 erro, I44 artigo — P-10,
/// decisão E1). Verbo próprio pela mesma razão do DTO acima: trocar slug é ação destrutiva de
/// link vivo, e um campo <c>slug</c> opcional em <c>AtualizarXDto</c> faria toda gravação de nome
/// carregar a chance de reescrever URL pública sem ninguém pedir.
///
/// <para>
/// O valor chega como texto livre do formulário — quem normaliza é o backend
/// (<c>Slug.DeTexto</c>), nunca o cliente (rules/security.md: o frontend não é a última linha de
/// defesa), exatamente como <c>Criar()</c> já faz com nome/título.
/// </para>
/// </summary>
public sealed record AlterarSlugDto
{
    public required string Slug { get; init; }
}
