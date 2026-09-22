using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Exceptions;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Área interna de <see cref="Exemplo"/> (I38-I42, analise-backend.md §5.3). Sem chamada a
/// <c>IIndiceBuscaWriter</c>: exemplo não é indexado — não pertence a nenhuma das 6 famílias de
/// <c>TipoItemIndexado</c>. Sem publicação própria (decisão C1): a visibilidade cascateia da
/// versão/provedor.
/// </summary>
public sealed class ExemploServicoInterno : IExemploServicoInterno
{
    /// <summary>C3 — cap de 10 exemplos por versão, rejeitado na escrita com 422.</summary>
    private const int CapExemplosPorVersao = 10;

    private readonly IExemploRepositorioInterno _repositorio;
    private readonly IVersaoProvedorRepositorioInterno _versaoRepositorio;

    public ExemploServicoInterno(
        IExemploRepositorioInterno repositorio, IVersaoProvedorRepositorioInterno versaoRepositorio)
    {
        _repositorio = repositorio;
        _versaoRepositorio = versaoRepositorio;
    }

    public async Task<IReadOnlyCollection<ExemploAdminDto>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct)
    {
        _ = await _versaoRepositorio.ObterPorIdAsync(versaoProvedorId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", versaoProvedorId);

        return await _repositorio.ListarPorVersaoAsync(versaoProvedorId, ct);
    }

    public async Task<ExemploAdminDto> ObterPorIdAsync(int id, CancellationToken ct)
    {
        var exemplo = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Exemplo", id);

        return Mapear(exemplo);
    }

    public async Task<ExemploAdminDto> CriarAsync(int versaoProvedorId, CriarExemploDto dto, CancellationToken ct)
    {
        var versao = await _versaoRepositorio.ObterPorIdAsync(versaoProvedorId, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", versaoProvedorId);

        var totalAtual = await _repositorio.ContarPorVersaoAsync(versaoProvedorId, ct);
        if (totalAtual >= CapExemplosPorVersao)
            throw new LimiteExcedidoException(
                $"A versão já possui {totalAtual} exemplos, no limite de {CapExemplosPorVersao} (C3).");

        var slug = await ResolverSlugAsync(versaoProvedorId, dto.Nome, ct);
        var exemplo = Exemplo.Criar(versaoProvedorId, dto.Nome, dto.Xml, dto.Descricao, slug);

        await _repositorio.AdicionarAsync(exemplo, ct);
        _repositorio.TocarVersaoProvedor(versao);
        await _repositorio.SalvarAlteracoesAsync(ct);

        return Mapear(exemplo);
    }

    public async Task<ExemploAdminDto> AtualizarAsync(int id, AtualizarExemploDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var exemplo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        exemplo.AtualizarConteudo(dto.Nome, dto.Xml, dto.Descricao);

        await SalvarTocandoVersaoAsync(exemplo, ct);

        return Mapear(exemplo);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var exemplo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        exemplo.Desativar();

        await SalvarTocandoVersaoAsync(exemplo, ct);
    }

    /// <summary>
    /// Decisão C5 — toda escrita de exemplo (criar/editar/remover) força um <c>UPDATE</c> na
    /// <see cref="VersaoProvedor"/> pai no MESMO <c>SaveChanges</c>, o que avança o <c>xmin</c> dela
    /// e invalida a <c>ETag</c> de P4. Sem isso, editar um exemplo deixaria a página pública
    /// servindo <c>304</c> com o conteúdo velho.
    /// </summary>
    private async Task SalvarTocandoVersaoAsync(Exemplo exemplo, CancellationToken ct)
    {
        var versao = await _versaoRepositorio.ObterPorIdAsync(exemplo.VersaoProvedorId, rastreada: true, ct);
        if (versao is not null)
            _repositorio.TocarVersaoProvedor(versao);

        await _repositorio.SalvarAlteracoesAsync(ct);
    }

    /// <summary>
    /// Derivação do slug com sufixo determinístico quando dois nomes distintos normalizam para o
    /// mesmo slug dentro da versão (modelo-dados.md §1.3) — sem isso o <c>INSERT</c> voltaria como
    /// 409 de nome duplicado para um nome que, de fato, não está duplicado. O <c>UNIQUE</c> do
    /// banco segue como última linha de defesa contra corrida entre duas criações simultâneas.
    /// </summary>
    private async Task<string> ResolverSlugAsync(int versaoProvedorId, string nome, CancellationToken ct)
    {
        // Nome que não sobrevive à normalização é barrado em 422 pelo validator (mesmo predicado,
        // Slug.PodeDerivarDe) antes de chegar aqui — como nos serviços de artigo, erro e provedor.
        var slugBase = SlugVo.DeTexto(nome);

        var ocupados = (await _repositorio.ListarSlugsDaVersaoAsync(versaoProvedorId, ct))
            .ToHashSet(StringComparer.Ordinal);

        if (!ocupados.Contains(slugBase))
            return slugBase;

        for (var sufixo = 2; ; sufixo++)
        {
            var candidato = $"{slugBase}-{sufixo}";
            if (!ocupados.Contains(candidato))
                return candidato;
        }
    }

    private async Task<Exemplo> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var exemplo = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Exemplo", id);

        if (exemplo.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O exemplo foi alterado por outra requisição. Recarregue e tente novamente.");

        return exemplo;
    }

    private static ExemploAdminDto Mapear(Exemplo exemplo) => new()
    {
        Id = exemplo.Id,
        VersaoProvedorId = exemplo.VersaoProvedorId,
        Slug = exemplo.Slug,
        Nome = exemplo.Nome,
        Descricao = exemplo.Descricao,
        Xml = exemplo.Xml,
        VersaoConcorrencia = exemplo.Versao,
    };
}
