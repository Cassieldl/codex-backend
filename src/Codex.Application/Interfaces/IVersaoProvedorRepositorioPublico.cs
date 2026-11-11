using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura PÚBLICA de <see cref="VersaoProvedor"/> (P4) — extensão do par
/// Público/Interno estabelecido por BACK-1 em <c>IProvedorRepositorioPublico</c>, aplicado aqui
/// à sub-entidade que sustenta a página exaustiva de campos. Não existe navegação
/// <c>VersaoProvedor.Provedor</c> no mapeamento real (analise-backend.md §3.3) — todo acesso é
/// via join explícito, nunca <c>Include</c>.
/// </summary>
public interface IVersaoProvedorRepositorioPublico
{
    /// <summary>
    /// Join explícito provedor + versão + rótulo da versão de layout, pelos dois slugs. Não
    /// filtra <c>Publicado</c> em nenhum dos dois lados — M7 (404 vs 410) é responsabilidade do
    /// service. Filtra só <c>DesativadoEm</c> (soft-deleted = 404 mudo, correto).
    /// </summary>
    Task<ProvedorVersaoBrutaDto?> ObterPorSlugsAsync(string provedorSlug, string versaoSlug, CancellationToken ct);

    /// <summary>Contagem de <c>CampoVersao</c> ativos da versão — defesa de leitura do cap de 2000 (M22).</summary>
    Task<int> ContarCamposAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Tabela exaustiva de campos da versão, ordenada por <c>CampoLayout.Ordem</c> (§4.3).</summary>
    Task<IReadOnlyCollection<CampoVersaoProjecaoDto>> ListarCamposAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Avisos de escopo VERSÃO desta versão, publicados e não desativados (U8 — nunca duplica os avisos de provedor de P3).</summary>
    Task<IReadOnlyCollection<Aviso>> ListarAvisosPublicadosDaVersaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>
    /// Campos afetados de cada aviso de <paramref name="avisoIds"/>, indexados pelo id do aviso
    /// (avisos sem campo ficam fora do dicionário). Mesmo critério de visibilidade da tabela da
    /// página: <c>CampoVersao</c> e <c>CampoLayout</c> ativos, e o campo tem de ser DESTA versão.
    /// Ordenados pela <c>Ordem</c> canônica, como na tabela.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyCollection<CampoAfetadoPublicoDto>>> ListarCamposAfetadosPorAvisoAsync(
        int versaoProvedorId, IReadOnlyCollection<int> avisoIds, CancellationToken ct);

    /// <summary>Erros publicados aplicáveis a esta versão (via <c>errosversoes</c>), para <c>errosResumo</c>.</summary>
    Task<IReadOnlyCollection<ErroResumoPublicoDto>> ListarErrosAplicaveisPublicadosAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>
    /// Cenários de XML da versão (003/§5.5). Sem filtro de publicação própria — exemplo não tem
    /// (C1): a visibilidade cascateia da versão/provedor, já decidida pelo service.
    /// </summary>
    Task<IReadOnlyCollection<ExemploPublicoDto>> ListarExemplosAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>De-para da natureza da operação da versão, na ordem de exibição.</summary>
    Task<IReadOnlyCollection<NaturezaOperacaoPublicoDto>> ListarNaturezasOperacaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Campos ativos da versão com o caminho canônico e o estado, para recortar o envio modelo.</summary>
    Task<IReadOnlyCollection<CampoEnvioModeloBrutoDto>> ListarCamposDoEnvioModeloAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>
    /// Estrutura gravada da versão de layout, só se ela estiver publicada e ativa. Nulo quando não
    /// tem estrutura ou não está visível.
    /// </summary>
    Task<string?> ObterEstruturaDoLayoutPublicadoAsync(int versaoLayoutId, CancellationToken ct);

    /// <summary>Demais versões publicadas do mesmo provedor, excluindo a atual — para <c>outrasVersoes</c>.</summary>
    Task<IReadOnlyCollection<VersaoProvedor>> ListarOutrasVersoesPublicadasAsync(int provedorId, int versaoProvedorIdAtual, CancellationToken ct);
}
