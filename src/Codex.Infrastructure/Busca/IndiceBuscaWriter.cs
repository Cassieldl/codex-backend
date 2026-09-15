using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Busca;

/// <summary>
/// Resolve o risco #1 do DBA (modelo-dados.md §5.6.1, analise-backend.md §6.6): a cascata de
/// visibilidade de <c>codex.itensbusca</c> não é garantida por nenhum <c>CHECK</c>/trigger de
/// banco — é 100% responsabilidade deste writer, chamado dentro da MESMA transação da mudança de
/// <c>Publicado</c> na tabela de origem (a transação em si é aberta pelo service chamador, nunca
/// aqui — este writer só executa <c>UPDATE ... WHERE</c> em lote sobre a conexão/transação
/// ambiente do <see cref="CodexDbContext"/> injetado).
/// </summary>
public sealed class IndiceBuscaWriter : IIndiceBuscaWriter
{
    private readonly CodexDbContext _contexto;

    public IndiceBuscaWriter(CodexDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task RecalcularVisibilidadeAsync(TipoItemBusca tipoQueMudou, int id, CancellationToken ct) => tipoQueMudou switch
    {
        TipoItemBusca.Provedor => RecalcularProvedorAsync(id, ct),
        TipoItemBusca.VersaoProvedor => RecalcularVersaoProvedorAsync(id, ct),
        TipoItemBusca.VersaoLayout => RecalcularVersaoLayoutAsync(id, ct),
        TipoItemBusca.Aviso => RecalcularAvisoAsync(id, ct),
        TipoItemBusca.Erro => RecalcularErroAsync(id, ct),
        TipoItemBusca.CampoLayout => RecalcularCampoLayoutAsync(id, ct),
        TipoItemBusca.Artigo => RecalcularArtigoAsync(id, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(tipoQueMudou), tipoQueMudou, "Tipo de item de busca desconhecido."),
    };

    /// <summary>
    /// Caso mais complexo do grafo de cascata (analise-backend.md §6.6): a própria linha, todas
    /// as versões daquele provedor, todos os avisos de escopo provedor e, transitivamente, todos
    /// os avisos de escopo versão de TODAS as versões daquele provedor.
    /// </summary>
    private async Task RecalcularProvedorAsync(int provedorId, CancellationToken ct)
    {
        var publicado = await _contexto.Provedores.AsNoTracking()
            .Where(p => p.Id == provedorId)
            .Select(p => p.Publicado)
            .FirstAsync(ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET publicado = {publicado}
            WHERE tipoitem = 'provedor' AND itemid = {provedorId}
            """, ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca ib SET publicado = ({publicado} AND vp.publicado)
            FROM codex.versoesprovedor vp
            WHERE ib.tipoitem = 'versaoprovedor' AND ib.itemid = vp.versaoprovedorid
              AND vp.provedorid = {provedorId}
            """, ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca ib SET publicado = ({publicado} AND a.publicado)
            FROM codex.avisos a
            WHERE ib.tipoitem = 'aviso' AND ib.itemid = a.avisoid AND a.provedorid = {provedorId}
            """, ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca ib SET publicado = ({publicado} AND vp.publicado AND a.publicado)
            FROM codex.avisos a
            JOIN codex.versoesprovedor vp ON vp.versaoprovedorid = a.versaoprovedorid
            WHERE ib.tipoitem = 'aviso' AND ib.itemid = a.avisoid AND vp.provedorid = {provedorId}
            """, ct);
    }

    /// <summary>A própria linha (combinada com o publicado do provedor pai) e os avisos de escopo versão daquela versão.</summary>
    private async Task RecalcularVersaoProvedorAsync(int versaoProvedorId, CancellationToken ct)
    {
        var versao = await _contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.Id == versaoProvedorId)
            .Select(v => new { v.Publicado, v.ProvedorId })
            .FirstAsync(ct);

        var provedorPublicado = await _contexto.Provedores.AsNoTracking()
            .Where(p => p.Id == versao.ProvedorId)
            .Select(p => p.Publicado)
            .FirstAsync(ct);

        var visivel = versao.Publicado && provedorPublicado;

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET publicado = {visivel}
            WHERE tipoitem = 'versaoprovedor' AND itemid = {versaoProvedorId}
            """, ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca ib SET publicado = ({visivel} AND a.publicado)
            FROM codex.avisos a
            WHERE ib.tipoitem = 'aviso' AND ib.itemid = a.avisoid AND a.versaoprovedorid = {versaoProvedorId}
            """, ct);
    }

    /// <summary>Sem linha própria em itensbusca (§5.6.1) — só propaga para os camposlayout cuja versão de ORIGEM é esta.</summary>
    private async Task RecalcularVersaoLayoutAsync(int versaoLayoutId, CancellationToken ct)
    {
        var publicado = await _contexto.VersoesLayout.AsNoTracking()
            .Where(v => v.Id == versaoLayoutId)
            .Select(v => v.Publicado)
            .FirstAsync(ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca ib SET publicado = ({publicado} AND cl.publicado)
            FROM codex.camposlayout cl
            WHERE ib.tipoitem = 'campolayout' AND ib.itemid = cl.campolayoutid
              AND cl.versaolayoutid = {versaoLayoutId}
            """, ct);
    }

    /// <summary>Leaf do grafo — só a própria linha, combinada com o(s) ancestral(is) conforme o escopo (provedor OU versão, U8).</summary>
    private async Task RecalcularAvisoAsync(int avisoId, CancellationToken ct)
    {
        var aviso = await _contexto.Avisos.AsNoTracking()
            .Where(a => a.Id == avisoId)
            .Select(a => new { a.Publicado, a.ProvedorId, a.VersaoProvedorId })
            .FirstAsync(ct);

        bool visivel;
        if (aviso.ProvedorId is not null)
        {
            var provedorPublicado = await _contexto.Provedores.AsNoTracking()
                .Where(p => p.Id == aviso.ProvedorId.Value)
                .Select(p => p.Publicado)
                .FirstAsync(ct);
            visivel = aviso.Publicado && provedorPublicado;
        }
        else
        {
            var versao = await _contexto.VersoesProvedor.AsNoTracking()
                .Where(v => v.Id == aviso.VersaoProvedorId!.Value)
                .Select(v => new { v.Publicado, v.ProvedorId })
                .FirstAsync(ct);
            var provedorPublicado = await _contexto.Provedores.AsNoTracking()
                .Where(p => p.Id == versao.ProvedorId)
                .Select(p => p.Publicado)
                .FirstAsync(ct);
            visivel = aviso.Publicado && versao.Publicado && provedorPublicado;
        }

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET publicado = {visivel}
            WHERE tipoitem = 'aviso' AND itemid = {avisoId}
            """, ct);
    }

    /// <summary>Leaf do grafo — Erro não tem ancestral de publicação (não está sob a cascata de provedor, U9).</summary>
    private async Task RecalcularErroAsync(int erroId, CancellationToken ct)
    {
        var publicado = await _contexto.Erros.AsNoTracking()
            .Where(e => e.Id == erroId)
            .Select(e => e.Publicado)
            .FirstAsync(ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET publicado = {publicado}
            WHERE tipoitem = 'erro' AND itemid = {erroId}
            """, ct);
    }

    /// <summary>Leaf do grafo — Artigo não tem ancestral de publicação, igual a Erro (arquitetura.md §5.A).</summary>
    private async Task RecalcularArtigoAsync(int artigoId, CancellationToken ct)
    {
        var publicado = await _contexto.Artigos.AsNoTracking()
            .Where(a => a.Id == artigoId)
            .Select(a => a.Publicado)
            .FirstAsync(ct);

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET publicado = {publicado}
            WHERE tipoitem = 'artigo' AND itemid = {artigoId}
            """, ct);
    }

    /// <summary>Leaf do grafo — combina o próprio publicado com o da VersaoLayout de origem.</summary>
    private async Task RecalcularCampoLayoutAsync(int campoLayoutId, CancellationToken ct)
    {
        var campo = await _contexto.CamposLayout.AsNoTracking()
            .Where(c => c.Id == campoLayoutId)
            .Select(c => new { c.Publicado, c.VersaoLayoutId })
            .FirstAsync(ct);

        var versaoLayoutPublicada = await _contexto.VersoesLayout.AsNoTracking()
            .Where(v => v.Id == campo.VersaoLayoutId)
            .Select(v => v.Publicado)
            .FirstAsync(ct);

        var visivel = campo.Publicado && versaoLayoutPublicada;

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET publicado = {visivel}
            WHERE tipoitem = 'campolayout' AND itemid = {campoLayoutId}
            """, ct);
    }

    /// <summary>
    /// Upsert de uma linha de projeção — titulo/url/chave/subtitulo/provedorId(s) são derivados
    /// da entidade de origem (<see cref="CarregarMetadadosAsync"/>), já que a interface só recebe
    /// os pesos (texto denormalizado para indexação); quem chama já tem a entidade carregada, mas
    /// o contrato deliberadamente não a expõe aqui (Infrastructure/Busca não deve depender do
    /// shape exato de cada service de conteúdo, só do par tipoItem/itemId).
    /// </summary>
    public async Task AtualizarPesosAsync(TipoItemIndexado tipoItem, int itemId, string? pesoA, string? pesoB, string? pesoC, string? pesoD, CancellationToken ct)
    {
        var metadados = await CarregarMetadadosAsync(tipoItem, itemId, ct)
            ?? throw new EntidadeNaoEncontradaException($"Entidade de origem '{tipoItem}'/{itemId} não encontrada para indexação de busca.");

        var existente = await _contexto.ItensBusca
            .FirstOrDefaultAsync(x => x.TipoItem == tipoItem && x.ItemId == itemId, ct);

        if (existente is null)
        {
            existente = ItemBusca.Criar(
                tipoItem, itemId, metadados.Titulo, metadados.Url,
                metadados.Chave, metadados.Subtitulo, metadados.ProvedorId, metadados.VersaoProvedorId,
                metadados.ProvedorIds);
            _contexto.ItensBusca.Add(existente);
        }
        else
        {
            // CarregarMetadadosAsync acabou de reler a entidade de origem: reaproveitar TODOS os
            // campos derivados, não só provedorIds. Atualizar apenas os pesos deixava titulo/url/
            // subtitulo congelados no valor da criação — renomear conteúdo publicado passava a ser
            // encontrável pelo nome novo e exibido com o nome antigo.
            existente.AtualizarProjecao(
                metadados.Titulo, metadados.Url, metadados.Chave, metadados.Subtitulo,
                metadados.ProvedorId, metadados.VersaoProvedorId);
            existente.AtualizarProvedorIds(metadados.ProvedorIds);
        }

        existente.AtualizarPesos(pesoA, pesoB, pesoC, pesoD);

        await _contexto.SaveChangesAsync(ct);

        var textoParaLexico = string.Join(' ', new[] { pesoA, pesoB, pesoC, pesoD }.Where(p => !string.IsNullOrWhiteSpace(p)));
        await AtualizarLexicoAsync(textoParaLexico, ct);
    }

    /// <summary>
    /// Soft delete da projeção. Cascateia para as linhas DEPENDENTES, pela mesma topologia da
    /// cascata de visibilidade (§6.6): remover o pai é o caso MAIS forte que despublicá-lo, e
    /// desativar só a própria linha deixava a versão de um provedor removido na busca pública com
    /// URL morta. <c>visivelpublico</c> é coluna gerada que olha apenas a própria linha
    /// (<c>publicado AND desativadoem IS NULL AND …</c>), então a linha filha tem de ser desativada
    /// explicitamente — nada no banco faz isso por nós.
    /// </summary>
    public async Task DesativarAsync(TipoItemIndexado tipoItem, int itemId, CancellationToken ct)
    {
        var existente = await _contexto.ItensBusca
            .FirstOrDefaultAsync(x => x.TipoItem == tipoItem && x.ItemId == itemId, ct);

        if (existente is not null)
        {
            existente.Desativar();
            await _contexto.SaveChangesAsync(ct);
        }

        // Roda mesmo quando a própria linha não existe: a ausência da linha do pai não diz nada
        // sobre as filhas, e o passo é idempotente (só toca desativadoem ainda nulo).
        await DesativarDependentesAsync(tipoItem, itemId, ct);
    }

    /// <summary>
    /// Dependentes de cada tipo, espelhando <see cref="RecalcularProvedorAsync"/> e
    /// <see cref="RecalcularVersaoProvedorAsync"/>. Erro, aviso, <c>campolayout</c> e
    /// <c>artigo</c> são folhas do grafo de projeção — nenhuma linha de <c>codex.itensbusca</c>
    /// depende deles.
    /// </summary>
    private async Task DesativarDependentesAsync(TipoItemIndexado tipoItem, int itemId, CancellationToken ct)
    {
        switch (tipoItem)
        {
            case TipoItemIndexado.provedor:
                await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE codex.itensbusca ib SET desativadoem = now()
                    FROM codex.versoesprovedor vp
                    WHERE ib.tipoitem = 'versaoprovedor' AND ib.itemid = vp.versaoprovedorid
                      AND vp.provedorid = {itemId} AND ib.desativadoem IS NULL
                    """, ct);

                await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE codex.itensbusca ib SET desativadoem = now()
                    FROM codex.avisos a
                    WHERE ib.tipoitem = 'aviso' AND ib.itemid = a.avisoid
                      AND a.provedorid = {itemId} AND ib.desativadoem IS NULL
                    """, ct);

                await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE codex.itensbusca ib SET desativadoem = now()
                    FROM codex.avisos a
                    JOIN codex.versoesprovedor vp ON vp.versaoprovedorid = a.versaoprovedorid
                    WHERE ib.tipoitem = 'aviso' AND ib.itemid = a.avisoid
                      AND vp.provedorid = {itemId} AND ib.desativadoem IS NULL
                    """, ct);
                break;

            case TipoItemIndexado.versaoprovedor:
                await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE codex.itensbusca ib SET desativadoem = now()
                    FROM codex.avisos a
                    WHERE ib.tipoitem = 'aviso' AND ib.itemid = a.avisoid
                      AND a.versaoprovedorid = {itemId} AND ib.desativadoem IS NULL
                    """, ct);
                break;

            case TipoItemIndexado.aviso:
            case TipoItemIndexado.erro:
            case TipoItemIndexado.campolayout:
            case TipoItemIndexado.artigo:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(tipoItem), tipoItem, "Tipo de item indexado desconhecido.");
        }
    }

    /// <summary>
    /// Mantém codex.termosbusca (modelo-dados.md §5.7) a partir do texto recém-indexado — tokens
    /// ≥ 3 caracteres extraídos via a MESMA codex.normalizar_busca usada na indexação, nunca uma
    /// tokenização equivalente em C#.
    /// </summary>
    private async Task AtualizarLexicoAsync(string texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        await _contexto.Database.ExecuteSqlInterpolatedAsync($"""
            WITH tokens AS (
                SELECT DISTINCT t AS termo
                FROM regexp_split_to_table(codex.normalizar_busca({texto}), '\s+') AS t
                WHERE length(t) >= 3
            )
            INSERT INTO codex.termosbusca (termo, ocorrencias)
            SELECT termo, 1 FROM tokens
            ON CONFLICT (termo) DO UPDATE SET ocorrencias = codex.termosbusca.ocorrencias + 1
            """, ct);
    }

    private sealed record MetadadosItemBusca(
        string Titulo,
        string Url,
        string? Chave,
        string? Subtitulo,
        int? ProvedorId,
        int? VersaoProvedorId,
        IReadOnlyCollection<int> ProvedorIds);

    /// <summary>
    /// Deriva titulo/url/chave/subtitulo/provedorId(s) da entidade de origem — um bloco por tipo,
    /// espelhando o mapa de URLs de arquitetura.md §4.1. <c>campolayout</c> não tem URL pública
    /// própria na 001 (M23); a URL gerada aqui só serve à busca INTERNA (I21).
    /// </summary>
    private async Task<MetadadosItemBusca?> CarregarMetadadosAsync(TipoItemIndexado tipoItem, int itemId, CancellationToken ct)
    {
        switch (tipoItem)
        {
            case TipoItemIndexado.provedor:
            {
                var provedor = await _contexto.Provedores.AsNoTracking()
                    .Where(p => p.Id == itemId)
                    .Select(p => new { p.Nome, p.Slug })
                    .FirstOrDefaultAsync(ct);

                return provedor is null
                    ? null
                    : new MetadadosItemBusca(provedor.Nome, $"/provedores/{provedor.Slug}", null, null, itemId, null, [itemId]);
            }

            case TipoItemIndexado.versaoprovedor:
            {
                var versao = await _contexto.VersoesProvedor.AsNoTracking()
                    .Where(v => v.Id == itemId)
                    .Select(v => new { v.Rotulo, v.Slug, v.ProvedorId })
                    .FirstOrDefaultAsync(ct);
                if (versao is null)
                    return null;

                var provedor = await _contexto.Provedores.AsNoTracking()
                    .Where(p => p.Id == versao.ProvedorId)
                    .Select(p => new { p.Nome, p.Slug })
                    .FirstAsync(ct);

                return new MetadadosItemBusca(
                    versao.Rotulo, $"/provedores/{provedor.Slug}/{versao.Slug}", null, provedor.Nome,
                    versao.ProvedorId, itemId, [versao.ProvedorId]);
            }

            case TipoItemIndexado.aviso:
            {
                var aviso = await _contexto.Avisos.AsNoTracking()
                    .Where(a => a.Id == itemId)
                    .Select(a => new { a.Titulo, a.ProvedorId, a.VersaoProvedorId })
                    .FirstOrDefaultAsync(ct);
                if (aviso is null)
                    return null;

                if (aviso.ProvedorId is not null)
                {
                    var provedor = await _contexto.Provedores.AsNoTracking()
                        .Where(p => p.Id == aviso.ProvedorId.Value)
                        .Select(p => new { p.Nome, p.Slug })
                        .FirstAsync(ct);

                    return new MetadadosItemBusca(
                        aviso.Titulo, $"/provedores/{provedor.Slug}#avisos", null, provedor.Nome,
                        aviso.ProvedorId, null, [aviso.ProvedorId.Value]);
                }

                var versaoDoAviso = await _contexto.VersoesProvedor.AsNoTracking()
                    .Where(v => v.Id == aviso.VersaoProvedorId!.Value)
                    .Select(v => new { v.Slug, v.ProvedorId })
                    .FirstAsync(ct);
                var provedorDoAviso = await _contexto.Provedores.AsNoTracking()
                    .Where(p => p.Id == versaoDoAviso.ProvedorId)
                    .Select(p => new { p.Nome, p.Slug })
                    .FirstAsync(ct);

                return new MetadadosItemBusca(
                    aviso.Titulo, $"/provedores/{provedorDoAviso.Slug}/{versaoDoAviso.Slug}#avisos", null, provedorDoAviso.Nome,
                    versaoDoAviso.ProvedorId, aviso.VersaoProvedorId, [versaoDoAviso.ProvedorId]);
            }

            case TipoItemIndexado.erro:
            {
                var erro = await _contexto.Erros.AsNoTracking()
                    .Where(e => e.Id == itemId)
                    .Select(e => new { e.Titulo, e.Codigo, e.Slug })
                    .FirstOrDefaultAsync(ct);
                if (erro is null)
                    return null;

                // Deliberadamente SEM filtro de publicação: este é o conjunto BRUTO de vínculos.
                // O filtro público/interno `?provedor=` NÃO se apoia nesta coluna para erros — ele
                // resolve a pertinência por consulta publicação-aware em
                // ConsultaBuscaPostgres.CarregarErroIdsDoProvedorAsync, que compartilha o predicado
                // com a hidratação de rótulos.
                //
                // Filtrar por `Publicado` aqui seria pior do que não filtrar: o valor nasceria
                // correto e ficaria obsoleto na primeira publicação/despublicação de VERSÃO, porque
                // a cascata de visibilidade não reescreve o provedorids das linhas de erro — e uma
                // coluna que parece correta e mente é mais perigosa que uma explicitamente bruta.
                var provedorIds = await _contexto.ErrosVersoes.AsNoTracking()
                    .Where(ev => ev.ErroId == itemId)
                    .Join(_contexto.VersoesProvedor.AsNoTracking(), ev => ev.VersaoProvedorId, v => v.Id, (ev, v) => v.ProvedorId)
                    .Distinct()
                    .ToListAsync(ct);

                return new MetadadosItemBusca(erro.Titulo, $"/erros/{erro.Slug}", erro.Codigo, null, null, null, provedorIds);
            }

            case TipoItemIndexado.campolayout:
            {
                var campo = await _contexto.CamposLayout.AsNoTracking()
                    .Where(c => c.Id == itemId)
                    .Select(c => new { c.Nome, c.Significado, c.Slug })
                    .FirstOrDefaultAsync(ct);

                return campo is null
                    ? null
                    : new MetadadosItemBusca(campo.Nome, $"/layout/campos/{campo.Slug}", null, campo.Significado, null, null, []);
            }

            case TipoItemIndexado.artigo:
            {
                var artigo = await _contexto.Artigos.AsNoTracking()
                    .Where(a => a.Id == itemId)
                    .Select(a => new { a.Titulo, a.Slug })
                    .FirstOrDefaultAsync(ct);

                // Chave/subtitulo/provedorId/versaoProvedorId nulos e provedorIds vazio: artigo
                // não tem contexto de provedor (PQ-06). Url via RotaCanonica — fonte ÚNICA da URL
                // pública, para o item indexado do reservado apontar para /como-funciona, nunca
                // /artigos/como-funciona, sem duplicar a regra em outro lugar.
                return artigo is null
                    ? null
                    : new MetadadosItemBusca(artigo.Titulo, SlugsReservadosArtigo.RotaCanonica(artigo.Slug), null, null, null, null, []);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(tipoItem), tipoItem, "Tipo de item indexado desconhecido.");
        }
    }
}
