using System.Text.RegularExpressions;
using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Infrastructure.Busca;

/// <summary>
/// Cascata de consulta FORTE → PARCIAL → APROX. → VAZIA (analise-backend.md §6.1–§6.5,
/// modelo-dados.md §5.8), sempre SEQUENCIAL no mesmo <see cref="CodexDbContext"/> (nunca
/// paralela — regressão histórica já documentada). Cada passo repete literalmente o predicado do
/// índice GIN parcial correspondente (<c>visivelpublico</c> para público, <c>desativadoem IS
/// NULL</c> para interno) — nunca abstraído atrás de um parâmetro, para o planner sempre casar
/// com o índice certo (§3.2 da análise de backend).
/// <para>
/// Depois de escolhida a página, os rótulos de provedor/versão e a meta de verificação são
/// hidratados por JOIN sobre os ids da página (<see cref="CarregarRotulosAsync"/>) — nunca por
/// coluna denormalizada em <c>codex.itensbusca</c>, que envelheceria em silêncio quando um rótulo
/// mudasse sem passar pela cascata de publicação.
/// </para>
/// </summary>
public sealed partial class ConsultaBuscaPostgres : IConsultaBusca
{
    private const int CapLinhas = 300;

    /// <summary>
    /// Delimitadores de destaque do <c>ts_headline</c>. A reconstrução em
    /// <see cref="ParseTrechos"/> decide o destaque pela PARIDADE do índice do split, então um
    /// delimitador vindo do próprio conteúdo editorial deslocaria a paridade e passaria a marcar
    /// texto que não casou com a busca. Por isso os dois caracteres são removidos do documento
    /// dentro do <c>ts_headline</c> de cada passo: quando o texto chega aqui, todo ⟦/⟧ presente
    /// foi posto pelo Postgres.
    /// </summary>
    private const string DelimitadorInicio = "⟦";

    /// <inheritdoc cref="DelimitadorInicio"/>
    private const string DelimitadorFim = "⟧";

    /// <summary>
    /// Cap de tokens submetidos à autocorreção. O <c>statement_timeout</c> protege CADA statement,
    /// não a requisição: sem este cap um termo de 299 caracteres rende até 60 tokens × 3 consultas
    /// (normalização + léxico + trigram/levenshtein) numa única chamada anônima. A consulta real
    /// tem mediana de 2 a 4 palavras, então corrigir os 6 primeiros cobre o uso legítimo.
    /// </summary>
    private const int CapTokensAutocorrecao = 6;

    private const string TipoErro = "erro";
    private const string TipoProvedor = "provedor";
    private const string TipoVersaoProvedor = "versaoprovedor";
    private const string TipoAviso = "aviso";
    private const string TipoCampoLayout = "campolayout";

    /// <summary>
    /// Sentinela de filtro de tipo impossível: nenhum valor de <see cref="TipoItemIndexado"/> tem
    /// este nome. Serve ao caso em que o cliente pediu um recorte por tipo e NENHUM dos valores
    /// existe — devolver array vazio ali significaria "sem filtro" para o SQL e entregaria o
    /// conjunto inteiro a quem pediu o recorte.
    /// </summary>
    private const string TipoInexistente = "__tipo-inexistente__";

    /// <summary>Separador entre provedor e rótulo de versão quando um mesmo erro vale para provedores diferentes.</summary>
    private const string SeparadorProvedorVersao = " · ";

    private const string TipoArtigo = "artigo";

    /// <summary>Marca do título de erro cortado na carga (ver <see cref="TituloInteiro"/>).</summary>
    private const string Reticencias = "…";

    private static readonly IReadOnlyDictionary<string, string> RotulosGrupo = new Dictionary<string, string>
    {
        ["erro"] = "Erros conhecidos",
        ["provedor"] = "Provedores",
        ["versaoprovedor"] = "Versões de provedor",
        ["aviso"] = "Avisos",
        ["campolayout"] = "Campos do layout",
        ["artigo"] = "Artigos",
    };

    private static readonly IReadOnlyDictionary<string, string> RotulosItem = new Dictionary<string, string>
    {
        ["erro"] = "REJEIÇÃO",
        ["provedor"] = "PROVEDOR",
        ["versaoprovedor"] = "VERSÃO",
        ["aviso"] = "AVISO",
        ["campolayout"] = "CAMPO DO LAYOUT",
        ["artigo"] = "ARTIGO",
    };

    /// <summary>
    /// P-06 — mapa (tipo × bucket de peso) → frase honesta de casamento, ao lado de
    /// <see cref="RotulosGrupo"/>/<see cref="RotulosItem"/>. Construído a partir do que
    /// <c>Codex.Application.Common.PesosBusca</c> de fato grava em cada peso por tipo — nunca
    /// inventa precisão que a consulta não tem: onde um bucket empacota mais de um campo
    /// (ex.: C de erro = causaProvavel+solucaoSugerida), a frase nomeia o bucket, não o campo.
    /// </summary>
    private static readonly IReadOnlyDictionary<(string Tipo, char Bucket), string> FrasesPorBucket =
        new Dictionary<(string, char), string>
        {
            [(TipoErro, 'A')] = "casou com a mensagem observada",
            [(TipoErro, 'B')] = "casou com o título",
            [(TipoErro, 'C')] = "casou com a causa ou solução documentada",
            [(TipoProvedor, 'B')] = "casou com o nome",
            [(TipoVersaoProvedor, 'B')] = "casou com o rótulo da versão",
            [(TipoVersaoProvedor, 'D')] = "casou com a introdução",
            [(TipoAviso, 'B')] = "casou com o título",
            [(TipoAviso, 'C')] = "casou com o corpo do aviso",
            [(TipoCampoLayout, 'B')] = "casou com o nome ou caminho do campo",
            [(TipoCampoLayout, 'C')] = "casou com o significado do campo",
            [(TipoArtigo, 'A')] = "casou com o título",
            [(TipoArtigo, 'C')] = "casou com o corpo do artigo",
            [(TipoArtigo, 'D')] = "casou com o tipo Artigo",
        };

    private readonly CodexDbContext _contexto;
    private readonly double _limiarAutocorrecao;
    private readonly int _timeoutMs;
    private readonly int _orcamentoRequisicaoMs;

    public ConsultaBuscaPostgres(CodexDbContext contexto, IConfiguration configuracao)
    {
        _contexto = contexto;

        // Sem Microsoft.Extensions.Configuration.Binder no projeto — parse manual em vez de
        // adicionar dependência só para GetValue<T> (rules/backend.md: nunca instalar dependência
        // sem verificar se o já existente resolve).
        _limiarAutocorrecao = double.TryParse(
            configuracao["Busca:LimiarAutocorrecao"], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var limiar) ? limiar : 0.45;
        _timeoutMs = int.TryParse(configuracao["Busca:TimeoutMs"], out var timeoutMs) ? timeoutMs : 2000;
        _orcamentoRequisicaoMs = int.TryParse(configuracao["Busca:OrcamentoRequisicaoMs"], out var orcamentoMs) ? orcamentoMs : 2000;
    }

    public async Task<ResultadoBuscaDto> BuscarAsync(
        string termo,
        string? provedorSlug,
        IReadOnlyCollection<string>? tipos,
        int pagina,
        int tamanhoPagina,
        bool incluirRascunho,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termo);

        var termoOriginal = termo.Trim();
        var termoTrabalho = NormalizadorTermo.LimitarTamanho(termoOriginal);
        var tiposArray = FiltrarTiposValidos(tipos);

        // Comando de busca dedicado (proteção de custo, A04) — statement_timeout só vale para
        // esta transação, nunca a conexão inteira. SET não aceita parâmetro bind para o valor
        // (o protocolo estendido do Postgres rejeita "$1" em SET — só literal ou identificador),
        // então ExecuteSqlInterpolatedAsync (sempre parametriza) falha em runtime com
        // "syntax error at or near $1". _timeoutMs é int de configuração (nunca input do
        // usuário), então ExecuteSqlRawAsync com o literal embutido é seguro (sem SQL injection).
        await using var transacao = await _contexto.Database.BeginTransactionAsync(ct);
#pragma warning disable EF1002 // _timeoutMs é int validado por int.TryParse (nunca string de origem externa) — sem risco de injeção.
        await _contexto.Database.ExecuteSqlRawAsync($"SET LOCAL statement_timeout = {_timeoutMs}", ct);
#pragma warning restore EF1002

        var provedorFiltro = await ResolverProvedorFiltroAsync(provedorSlug, incluirRascunho, ct);

        // Recorte pedido cujo alvo não existe nesta superfície (slug inexistente, removido ou —
        // no público — em rascunho): resultado VAZIO, nunca o conjunto inteiro. Tratar
        // "não resolveu" como "sem filtro" devolveria tudo a quem pediu um recorte, e a resposta
        // é deliberadamente idêntica nos três casos para não virar oráculo de enumeração.
        if (provedorFiltro is null && !string.IsNullOrWhiteSpace(provedorSlug))
        {
            await transacao.CommitAsync(ct);
            return ResultadoVazio(termoOriginal);
        }

        // Só custa uma consulta quando há filtro de provedor ativo, e é restrita a esse provedor.
        IReadOnlySet<int> errosDoProvedor = provedorFiltro is null
            ? new HashSet<int>()
            : await CarregarErroIdsDoProvedorAsync(provedorFiltro.Id, incluirRascunho, ct);

        var qualidade = "exata";
        BuscaCorrecaoDto? correcao = null;

        // Cache de M28(a) compartilhado pelos passos da cascata: o mesmo erro reaparece em FORTE,
        // PARCIAL e nos dois passos da autocorreção, e a resposta não muda dentro da requisição.
        var vinculoPublicoPorErro = new Dictionary<int, bool>();

        // MED-B05 — orçamento de PAREDE por requisição, distinto do statement_timeout (que
        // protege cada statement, não a cascata inteira). Ligado ao token da requisição: se o
        // cliente cancelar de verdade, o cancelamento original propaga normal — só o estouro do
        // ORÇAMENTO (nunca o do cliente) é engolido abaixo, devolvendo o melhor resultado já
        // obtido em vez de 500/499.
        using var orcamentoCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_orcamentoRequisicaoMs > 0)
            orcamentoCts.CancelAfter(_orcamentoRequisicaoMs);
        var ctOrcamento = orcamentoCts.Token;

        // A visibilidade de vínculo de erro é aplicada em CADA passo, antes de qualquer decisão de
        // cascata, contagem ou paginação: se rodasse só na montagem final, um erro invisível ainda
        // seguraria a qualidade em "exata" e entraria no total.
        async Task<List<LinhaBusca>> ForteAsync(string termoPasso)
        {
            var linhasDoPasso = await ExecutarForteAsync(termoPasso, incluirRascunho, tiposArray, ctOrcamento);
            return await FiltrarErrosSemVinculoPublicoAsync(linhasDoPasso, incluirRascunho, vinculoPublicoPorErro, ctOrcamento);
        }

        async Task<List<LinhaBusca>> ParcialAsync(string termoPasso)
        {
            var linhasDoPasso = await ExecutarParcialAsync(termoPasso, incluirRascunho, tiposArray, ctOrcamento);
            return await FiltrarErrosSemVinculoPublicoAsync(linhasDoPasso, incluirRascunho, vinculoPublicoPorErro, ctOrcamento);
        }

        // `linhas` fica fora do try de propósito: se o orçamento estourar no MEIO de um passo, a
        // atribuição daquele passo nunca acontece e `linhas` retém o resultado do ÚLTIMO passo que
        // terminou com sucesso — é o "melhor resultado parcial" que MED-B05 pede, sem precisar de
        // nenhum estado adicional.
        var linhas = new List<LinhaBusca>();

        try
        {
            linhas = await ForteAsync(termoTrabalho);

            if (!TemLinhaDentroDoFiltro(linhas, provedorFiltro?.Id, errosDoProvedor))
            {
                linhas = await ParcialAsync(termoTrabalho);
                qualidade = "parcial";

                if (!TemLinhaDentroDoFiltro(linhas, provedorFiltro?.Id, errosDoProvedor))
                {
                    var (termoCorrigido, tokens) = await TentarAutocorrecaoAsync(termoTrabalho, ctOrcamento);

                    if (termoCorrigido is not null)
                    {
                        linhas = await ForteAsync(termoCorrigido);
                        if (!TemLinhaDentroDoFiltro(linhas, provedorFiltro?.Id, errosDoProvedor))
                            linhas = await ParcialAsync(termoCorrigido);

                        if (TemLinhaDentroDoFiltro(linhas, provedorFiltro?.Id, errosDoProvedor))
                        {
                            qualidade = "aproximada";
                            termoTrabalho = termoCorrigido;
                            correcao = new BuscaCorrecaoDto
                            {
                                TermoOriginal = termoOriginal,
                                TermoCorrigido = termoCorrigido,
                                Tokens = tokens,
                            };
                        }
                        else
                        {
                            qualidade = "vazia";
                            linhas = [];
                        }
                    }
                    else
                    {
                        qualidade = "vazia";
                        linhas = [];
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // MED-B05 — orçamento de PAREDE estourado (nunca o cancelamento do cliente, tratado
            // pela guarda acima): `linhas` já tem o melhor resultado do último passo bem-sucedido
            // da cascata. Sem nenhum passo concluído, regride para "vazia" — nunca 500, nunca 499.
            if (linhas.Count == 0)
                qualidade = "vazia";
        }

        // Hidratação de rótulos (provedor/versão/meta) roda DEPOIS da seleção da página e ainda
        // DENTRO da transação: consulta no máximo `tamanhoPagina` ids, e o statement_timeout desta
        // transação continua valendo para ela. Nada aqui toca o WHERE dos passos da cascata — o
        // predicado do índice GIN parcial permanece byte a byte o mesmo.
        var selecao = SelecionarItensDaPagina(linhas, provedorFiltro, errosDoProvedor, pagina, tamanhoPagina);
        var rotulos = await CarregarRotulosAsync(selecao.ItensDaPagina, incluirRascunho, ct);
        var causaOuSolucao = await CarregarCasamentoCausaOuSolucaoAsync(selecao.ItensDaPagina, termoTrabalho, ct);

        await transacao.CommitAsync(ct);

        return MontarResultado(termoOriginal, termoTrabalho, qualidade, correcao, selecao, provedorFiltro, rotulos, causaOuSolucao);
    }

    /// <summary>Resposta de sucesso sem nenhum resultado — P1 nunca devolve 404 para busca vazia.</summary>
    private static ResultadoBuscaDto ResultadoVazio(string termoOriginal) => new()
    {
        Termo = termoOriginal,
        TermoUsado = termoOriginal,
        Correcao = null,
        Qualidade = "vazia",
        TotalGeral = 0,
        Grupos = [],
        ForaDoFiltro = null,
        FiltroProvedor = null,
    };

    /// <summary>
    /// M28(a) — erro COM vínculo de versão só é público se ao menos um vínculo for público (versão
    /// e provedor publicados e ativos). Erro SEM vínculo nenhum continua público: é o erro
    /// genuinamente geral de U9.
    ///
    /// <para>
    /// Os dois casos colapsavam no MESMO payload público (os três campos de rótulo nulos são o
    /// sinal do selo "Geral"), então um erro específico de prefeitura cujas versões estão todas em
    /// rascunho era indistinguível de um erro que vale para todos os provedores — afirmação falsa
    /// sobre o alcance do erro, não apenas um item a mais na lista.
    /// </para>
    ///
    /// <para>
    /// Predicado idêntico ao de <see cref="CarregarVersoesAplicaveisAsync"/>, que monta os rótulos
    /// exibidos: "tem vínculo" e "tem vínculo público" precisam ser a mesma pergunta que a
    /// hidratação responde, senão o item volta a aparecer com rótulo que a própria resposta nega.
    /// Consulta feita em C# sobre os ids do passo, nunca no WHERE do SQL — o predicado do índice
    /// GIN parcial permanece byte a byte o mesmo.
    /// </para>
    /// </summary>
    private async Task<List<LinhaBusca>> FiltrarErrosSemVinculoPublicoAsync(
        List<LinhaBusca> linhas,
        bool incluirRascunho,
        Dictionary<int, bool> vinculoPublicoPorErro,
        CancellationToken ct)
    {
        // A área interna precisa enxergar o erro para saber que ele existe (M28, segunda parte).
        if (incluirRascunho || !linhas.Any(l => l.TipoItem == TipoErro))
            return linhas;

        var idsDesconhecidos = linhas
            .Where(l => l.TipoItem == TipoErro && !vinculoPublicoPorErro.ContainsKey(l.ItemId))
            .Select(l => l.ItemId)
            .Distinct()
            .ToArray();

        if (idsDesconhecidos.Length > 0)
        {
            var vinculos = await (
                from ev in _contexto.ErrosVersoes.AsNoTracking()
                join vp in _contexto.VersoesProvedor.AsNoTracking() on ev.VersaoProvedorId equals vp.Id
                join p in _contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
                where idsDesconhecidos.Contains(ev.ErroId) && vp.DesativadoEm == null && p.DesativadoEm == null
                select new { ev.ErroId, Publico = vp.Publicado && p.Publicado }
            ).ToListAsync(ct);

            foreach (var erroId in idsDesconhecidos)
            {
                var doErro = vinculos.Where(v => v.ErroId == erroId).ToList();
                vinculoPublicoPorErro[erroId] = doErro.Count == 0 || doErro.Exists(v => v.Publico);
            }
        }

        return linhas.Where(l => l.TipoItem != TipoErro || vinculoPublicoPorErro[l.ItemId]).ToList();
    }

    /// <summary>
    /// FORTE: exige todos os lexemas do termo via AND (<c>websearch_to_tsquery</c>).
    ///
    /// <para>
    /// Normaliza por <c>codex.normalizar_consulta</c>, nunca por <c>codex.normalizar_busca</c>.
    /// As duas diferem na terceira forma — a compactada —, e é o que separa consulta de
    /// indexação: no documento as formas são ALTERNATIVAS (o tsvector fica com todas), enquanto
    /// aqui viram EXIGÊNCIAS, porque o AND pede cada lexema. A compactada de um documento longo é
    /// o texto inteiro colado num lexema só, que nunca é igual à compactada de um termo curto —
    /// então exigi-la tornava este passo inalcançável para qualquer termo com mais de um token,
    /// incluindo o código do erro (pelo hífen) e o trecho de mensagem, que são os dois caminhos
    /// de entrada que o escopo funcional declara críticos.
    /// </para>
    /// </summary>
    private Task<List<LinhaBusca>> ExecutarForteAsync(string termo, bool incluirRascunho, string[] tiposArray, CancellationToken ct)
    {
        var query = incluirRascunho
            ? _contexto.Database.SqlQuery<LinhaBusca>($"""
                SELECT ib.itembuscaid AS "ItemBuscaId", ib.itemid AS "ItemId", ib.tipoitem AS "TipoItem", ib.chave AS "Chave",
                       ib.titulo AS "Titulo", ib.url AS "Url", ib.publicado AS "Publicado",
                       ib.provedorid AS "ProvedorId", ib.versaoprovedorid AS "VersaoProvedorId",
                       ib.provedorids AS "ProvedorIds",
                       ts_rank_cd(ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo})), 32) AS "Pontuacao",
                       ts_rank(ARRAY[0,0,0,1]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouA",
                       ts_rank(ARRAY[0,0,1,0]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouB",
                       ts_rank(ARRAY[0,1,0,0]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouC",
                       ts_rank(ARRAY[1,0,0,0]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouD",
                       ts_headline('codex.codex_pt'::regconfig,
                           replace(replace(concat_ws(' ', ib.pesoa, ib.pesob, ib.pesoc), '⟦', ''), '⟧', ''),
                           websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo})),
                           'StartSel=⟦, StopSel=⟧, MaxFragments=1, MaxWords=35, MinWords=15') AS "FragmentoBruto"
                FROM codex.itensbusca ib
                WHERE ib.desativadoem IS NULL
                  AND (cardinality({tiposArray}::text[]) = 0 OR ib.tipoitem = ANY({tiposArray}::text[]))
                  AND ib.documento @@ websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))
                ORDER BY "Pontuacao" DESC, ib.itembuscaid
                LIMIT {CapLinhas}
                """)
            : _contexto.Database.SqlQuery<LinhaBusca>($"""
                SELECT ib.itembuscaid AS "ItemBuscaId", ib.itemid AS "ItemId", ib.tipoitem AS "TipoItem", ib.chave AS "Chave",
                       ib.titulo AS "Titulo", ib.url AS "Url", ib.publicado AS "Publicado",
                       ib.provedorid AS "ProvedorId", ib.versaoprovedorid AS "VersaoProvedorId",
                       ib.provedorids AS "ProvedorIds",
                       ts_rank_cd(ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo})), 32) AS "Pontuacao",
                       ts_rank(ARRAY[0,0,0,1]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouA",
                       ts_rank(ARRAY[0,0,1,0]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouB",
                       ts_rank(ARRAY[0,1,0,0]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouC",
                       ts_rank(ARRAY[1,0,0,0]::float4[], ib.documento, websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))) > 0 AS "CasouD",
                       ts_headline('codex.codex_pt'::regconfig,
                           replace(replace(concat_ws(' ', ib.pesoa, ib.pesob, ib.pesoc), '⟦', ''), '⟧', ''),
                           websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo})),
                           'StartSel=⟦, StopSel=⟧, MaxFragments=1, MaxWords=35, MinWords=15') AS "FragmentoBruto"
                FROM codex.itensbusca ib
                WHERE ib.visivelpublico
                  AND ib.tipoitem <> {TipoVersaoProvedor} -- versão não tem página própria no portal público
                  AND (cardinality({tiposArray}::text[]) = 0 OR ib.tipoitem = ANY({tiposArray}::text[]))
                  AND ib.documento @@ websearch_to_tsquery('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))
                ORDER BY "Pontuacao" DESC, ib.itembuscaid
                LIMIT {CapLinhas}
                """);

        return query.ToListAsync(ct);
    }

    /// <summary>
    /// PARCIAL: recombina os mesmos lexemas do termo com OR (arquitetura.md §6.4) — cobertura
    /// parcial em vez de exigir todos os lexemas via AND (o que <c>websearch_to_tsquery</c> faz
    /// no passo FORTE).
    ///
    /// <para>
    /// Usa <c>codex.normalizar_consulta</c> pelo mesmo motivo do passo forte: no OR a forma
    /// compactada não chega a excluir resultado, mas entra como um lexema que nunca casa e polui
    /// a pontuação. Os dois passos precisam partir do mesmo conjunto de lexemas, senão o parcial
    /// deixa de ser o relaxamento do forte.
    /// </para>
    ///
    /// <para>
    /// Cada lexema entra citado e vira <c>tsquery</c> por cast: reparsear com <c>to_tsquery</c> lia o
    /// <c>:</c> de um token como <c>host:porta</c> como operador.
    /// </para>
    /// </summary>
    private Task<List<LinhaBusca>> ExecutarParcialAsync(string termo, bool incluirRascunho, string[] tiposArray, CancellationToken ct)
    {
        var query = incluirRascunho
            ? _contexto.Database.SqlQuery<LinhaBusca>($"""
                WITH lexemas AS (
                    SELECT string_agg(DISTINCT '''' || replace(replace(lexema, '\', '\\'), '''', '''''') || '''', ' | ')::tsquery AS consulta
                    FROM unnest(tsvector_to_array(
                        to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))
                    )) AS lexema
                )
                SELECT ib.itembuscaid AS "ItemBuscaId", ib.itemid AS "ItemId", ib.tipoitem AS "TipoItem", ib.chave AS "Chave",
                       ib.titulo AS "Titulo", ib.url AS "Url", ib.publicado AS "Publicado",
                       ib.provedorid AS "ProvedorId", ib.versaoprovedorid AS "VersaoProvedorId",
                       ib.provedorids AS "ProvedorIds",
                       ts_rank_cd(ib.documento, lexemas.consulta, 32) AS "Pontuacao",
                       ts_rank(ARRAY[0,0,0,1]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouA",
                       ts_rank(ARRAY[0,0,1,0]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouB",
                       ts_rank(ARRAY[0,1,0,0]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouC",
                       ts_rank(ARRAY[1,0,0,0]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouD",
                       ts_headline('codex.codex_pt'::regconfig,
                           replace(replace(concat_ws(' ', ib.pesoa, ib.pesob, ib.pesoc), '⟦', ''), '⟧', ''),
                           lexemas.consulta,
                           'StartSel=⟦, StopSel=⟧, MaxFragments=1, MaxWords=35, MinWords=15') AS "FragmentoBruto"
                FROM codex.itensbusca ib, lexemas
                WHERE ib.desativadoem IS NULL
                  AND (cardinality({tiposArray}::text[]) = 0 OR ib.tipoitem = ANY({tiposArray}::text[]))
                  AND lexemas.consulta IS NOT NULL
                  AND ib.documento @@ lexemas.consulta
                ORDER BY "Pontuacao" DESC, ib.itembuscaid
                LIMIT {CapLinhas}
                """)
            : _contexto.Database.SqlQuery<LinhaBusca>($"""
                WITH lexemas AS (
                    SELECT string_agg(DISTINCT '''' || replace(replace(lexema, '\', '\\'), '''', '''''') || '''', ' | ')::tsquery AS consulta
                    FROM unnest(tsvector_to_array(
                        to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_consulta({termo}))
                    )) AS lexema
                )
                SELECT ib.itembuscaid AS "ItemBuscaId", ib.itemid AS "ItemId", ib.tipoitem AS "TipoItem", ib.chave AS "Chave",
                       ib.titulo AS "Titulo", ib.url AS "Url", ib.publicado AS "Publicado",
                       ib.provedorid AS "ProvedorId", ib.versaoprovedorid AS "VersaoProvedorId",
                       ib.provedorids AS "ProvedorIds",
                       ts_rank_cd(ib.documento, lexemas.consulta, 32) AS "Pontuacao",
                       ts_rank(ARRAY[0,0,0,1]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouA",
                       ts_rank(ARRAY[0,0,1,0]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouB",
                       ts_rank(ARRAY[0,1,0,0]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouC",
                       ts_rank(ARRAY[1,0,0,0]::float4[], ib.documento, lexemas.consulta) > 0 AS "CasouD",
                       ts_headline('codex.codex_pt'::regconfig,
                           replace(replace(concat_ws(' ', ib.pesoa, ib.pesob, ib.pesoc), '⟦', ''), '⟧', ''),
                           lexemas.consulta,
                           'StartSel=⟦, StopSel=⟧, MaxFragments=1, MaxWords=35, MinWords=15') AS "FragmentoBruto"
                FROM codex.itensbusca ib, lexemas
                WHERE ib.visivelpublico
                  AND ib.tipoitem <> {TipoVersaoProvedor} -- versão não tem página própria no portal público
                  AND (cardinality({tiposArray}::text[]) = 0 OR ib.tipoitem = ANY({tiposArray}::text[]))
                  AND lexemas.consulta IS NOT NULL
                  AND ib.documento @@ lexemas.consulta
                ORDER BY "Pontuacao" DESC, ib.itembuscaid
                LIMIT {CapLinhas}
                """);

        return query.ToListAsync(ct);
    }

    /// <summary>
    /// Autocorreção explícita (arquitetura.md §6.5): só tokens com ≥ 4 caracteres AUSENTES do
    /// léxico são candidatos (se já está no léxico, é termo legítimo — nunca corrigir o que já
    /// existe). Guarda final por <c>levenshtein &lt;= 2</c> e limiar de similaridade configurável.
    /// </summary>
    private async Task<(string? TermoCorrigido, IReadOnlyCollection<BuscaTokenCorrecaoDto> Tokens)> TentarAutocorrecaoAsync(
        string termo, CancellationToken ct)
    {
        var candidatos = NormalizadorTermo.ExtrairTokensCandidatos(termo);
        if (candidatos.Count == 0)
            return (null, []);

        var termoTrabalho = termo;
        var tokens = new List<BuscaTokenCorrecaoDto>();

        // Take: os primeiros tokens são os que o usuário digitou primeiro. Limita o pior caso a
        // 18 consultas em vez de 180 (ver CapTokensAutocorrecao).
        foreach (var candidato in candidatos.Take(CapTokensAutocorrecao))
        {
            var normalizado = await NormalizarTokenAsync(candidato, ct);

            var jaConhecido = await _contexto.TermosBusca.AsNoTracking()
                .AnyAsync(t => t.Termo == normalizado && t.DesativadoEm == null, ct);
            if (jaConhecido)
                continue;

            var correcao = await BuscarCorrecaoAsync(normalizado, ct);
            if (correcao is null)
                continue;

            termoTrabalho = NormalizadorTermo.SubstituirTokenPorLiteral(termoTrabalho, candidato, correcao.Value.Termo);

            tokens.Add(new BuscaTokenCorrecaoDto { De = candidato, Para = correcao.Value.Termo, Similaridade = correcao.Value.Similaridade });
        }

        return tokens.Count == 0 ? (null, []) : (termoTrabalho, tokens);
    }

    private Task<string> NormalizarTokenAsync(string token, CancellationToken ct)
        => _contexto.Database.SqlQuery<string>($"SELECT lower(codex.f_unaccent({token})) AS \"Value\"").SingleAsync(ct);

    private async Task<(string Termo, double Similaridade)?> BuscarCorrecaoAsync(string tokenNormalizado, CancellationToken ct)
    {
        var candidatos = await _contexto.Database.SqlQuery<CandidatoCorrecao>($"""
            SELECT termo AS "Termo", similarity(termo, {tokenNormalizado}) AS "Similaridade"
            FROM codex.termosbusca
            WHERE desativadoem IS NULL
              AND termo % {tokenNormalizado}
              AND levenshtein(termo, {tokenNormalizado}) <= 2
            ORDER BY similarity(termo, {tokenNormalizado}) DESC, ocorrencias DESC
            LIMIT 1
            """).ToListAsync(ct);

        var candidato = candidatos.FirstOrDefault();
        if (candidato is null || candidato.Similaridade < _limiarAutocorrecao)
            return null;

        return (candidato.Termo, candidato.Similaridade);
    }

    /// <summary>
    /// O filtro segue a MESMA visibilidade da superfície que o chama: a busca pública não pode
    /// confirmar a existência — nem devolver o nome — de um provedor em rascunho, senão a resposta
    /// distingue "não existe" de "existe e não foi publicado" (oráculo de enumeração) e vaza o nome
    /// de conteúdo pré-lançamento a um anônimo. A busca interna (<c>incluirRascunho</c>) precisa
    /// vê-lo, e é por isso que o parâmetro entra em vez de o predicado ser fixo.
    /// </summary>
    private async Task<ProvedorFiltroInfo?> ResolverProvedorFiltroAsync(
        string? provedorSlug, bool incluirRascunho, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provedorSlug))
            return null;

        var consulta = _contexto.Provedores.AsNoTracking()
            .Where(p => p.Slug == provedorSlug && p.DesativadoEm == null);

        if (!incluirRascunho)
            consulta = consulta.Where(p => p.Publicado);

        var provedor = await consulta
            .Select(p => new { p.Id, p.Slug, p.Nome })
            .FirstOrDefaultAsync(ct);

        return provedor is null ? null : new ProvedorFiltroInfo(provedor.Id, provedor.Slug, provedor.Nome);
    }

    /// <summary>
    /// Erros ligados a um provedor pelas versões DELE, sob o mesmo predicado de visibilidade que
    /// <see cref="CarregarVersoesAplicaveisAsync"/> usa para montar os rótulos exibidos — é o que
    /// mantém filtro e exibição coerentes.
    ///
    /// <para>
    /// Por que a consulta é dinâmica e não o snapshot <c>itensbusca.provedorids</c>: a ligação
    /// erro↔provedor é PUBLICAÇÃO-DEPENDENTE, e o snapshot só é reescrito quando o próprio erro é
    /// reindexado. Publicar ou despublicar uma VERSÃO muda a resposta correta e a cascata de
    /// visibilidade (<c>RecalcularVersaoProvedorAsync</c>) mexe apenas em
    /// <c>itensbusca.publicado</c> das linhas de provedor/versão/aviso — nunca no
    /// <c>provedorids</c> das linhas de erro. Qualquer valor gravado ali nasce correto e fica
    /// obsoleto na primeira mudança de publicação.
    /// </para>
    /// </summary>
    private async Task<HashSet<int>> CarregarErroIdsDoProvedorAsync(
        int provedorId, bool incluirRascunho, CancellationToken ct)
    {
        var consulta =
            from ev in _contexto.ErrosVersoes.AsNoTracking()
            join vp in _contexto.VersoesProvedor.AsNoTracking() on ev.VersaoProvedorId equals vp.Id
            join p in _contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
            where vp.ProvedorId == provedorId && vp.DesativadoEm == null && p.DesativadoEm == null
            select new
            {
                ev.ErroId,
                ProvedorPublicado = p.Publicado,
                VersaoPublicada = vp.Publicado,
            };

        if (!incluirRascunho)
            consulta = consulta.Where(x => x.ProvedorPublicado && x.VersaoPublicada);

        var ids = await consulta.Select(x => x.ErroId).Distinct().ToListAsync(ct);

        return [.. ids];
    }

    private static bool EstaDentroDoFiltro(
        LinhaBusca linha, int provedorId, IReadOnlySet<int> errosLigadosAoProvedor)
    {
        // Erro não usa o snapshot: a pertinência ao provedor vem da consulta publicação-aware.
        if (linha.TipoItem == TipoErro)
            return errosLigadosAoProvedor.Contains(linha.ItemId);

        return linha.ProvedorId == provedorId || (linha.ProvedorIds?.Contains(provedorId) ?? false);
    }

    private static bool TemLinhaDentroDoFiltro(
        IReadOnlyList<LinhaBusca> linhas, int? provedorId, IReadOnlySet<int> errosLigadosAoProvedor)
        => provedorId is null
            ? linhas.Count > 0
            : linhas.Any(l => EstaDentroDoFiltro(l, provedorId.Value, errosLigadosAoProvedor));

    /// <summary>
    /// Whitelist de tipos (nunca o valor cru do cliente na consulta). Array vazio significa "sem
    /// filtro" para o SQL — por isso o caso "pedi um recorte e nenhum valor existe" não pode
    /// devolver vazio: ignorar um valor desconhecido ENTRE válidos é sensato, mas descartar o
    /// filtro inteiro devolve o conjunto completo a quem pediu o recorte. Nesse caso o filtro
    /// segue ativo com <see cref="TipoInexistente"/>, que não casa com nenhuma linha.
    /// </summary>
    private static string[] FiltrarTiposValidos(IReadOnlyCollection<string>? tipos)
    {
        if (tipos is null || tipos.Count == 0)
            return [];

        var validos = tipos
            .Where(t => Enum.TryParse<TipoItemIndexado>(t, ignoreCase: true, out _))
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToArray();

        return validos.Length > 0 ? validos : [TipoInexistente];
    }

    /// <summary>
    /// Aplica filtro de provedor e paginação sobre as linhas brutas — passo puro e síncrono,
    /// separado da montagem final porque a hidratação de rótulos precisa saber exatamente
    /// quais itens entram na página antes de consultar o banco de novo.
    /// </summary>
    private static SelecaoPagina SelecionarItensDaPagina(
        IReadOnlyList<LinhaBusca> linhas,
        ProvedorFiltroInfo? provedorFiltro,
        IReadOnlySet<int> errosLigadosAoProvedor,
        int pagina,
        int tamanhoPagina)
    {
        var dentro = linhas;
        BuscaForaDoFiltroDto? foraDoFiltro = null;

        if (provedorFiltro is not null)
        {
            var dentroLista = linhas.Where(l => EstaDentroDoFiltro(l, provedorFiltro.Id, errosLigadosAoProvedor)).ToList();
            var foraLista = linhas.Where(l => !EstaDentroDoFiltro(l, provedorFiltro.Id, errosLigadosAoProvedor)).ToList();
            dentro = dentroLista;

            if (foraLista.Count > 0)
            {
                foraDoFiltro = new BuscaForaDoFiltroDto
                {
                    Total = foraLista.Count,
                    Tipos = foraLista.Select(l => l.TipoItem).Distinct().ToArray(),
                };
            }
        }

        // Teto além do piso: aqui a coleção já está materializada, então `Skip` negativo NÃO lança —
        // o LINQ-to-Objects trata como zero e devolveria a página 1 rotulada com a página pedida.
        // É a variante silenciosa do mesmo estouro que no repositório vira 2201X do Postgres.
        var paginaEfetiva = Paginacao.NormalizarPagina(pagina, tamanhoPagina);
        var itensDaPagina = dentro
            .OrderByDescending(l => l.Pontuacao)
            .ThenBy(l => l.ItemBuscaId)
            .Skip((paginaEfetiva - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToList();

        return new SelecaoPagina(dentro, itensDaPagina, foraDoFiltro);
    }

    private static ResultadoBuscaDto MontarResultado(
        string termoOriginal,
        string termoUsado,
        string qualidade,
        BuscaCorrecaoDto? correcao,
        SelecaoPagina selecao,
        ProvedorFiltroInfo? provedorFiltro,
        IReadOnlyDictionary<int, RotulosDoItem> rotulos,
        IReadOnlyDictionary<int, CasamentoCausaOuSolucao> causaOuSolucao)
    {
        var dentro = selecao.Dentro;

        var grupos = selecao.ItensDaPagina
            .GroupBy(l => l.TipoItem)
            .Select(grupo => new BuscaGrupoDto
            {
                Tipo = grupo.Key,
                Rotulo = RotulosGrupo.GetValueOrDefault(grupo.Key, grupo.Key),
                Total = dentro.Count(l => l.TipoItem == grupo.Key),
                Pontuacao = grupo.Max(l => (double)l.Pontuacao),
                Itens = grupo.OrderByDescending(l => l.Pontuacao)
                    .ThenBy(l => l.ItemBuscaId)
                    .Select(linha => MapearItem(linha, rotulos, termoUsado, qualidade, causaOuSolucao))
                    .ToArray(),
            })
            .OrderByDescending(g => g.Pontuacao)
            .ThenBy(g => g.Tipo, StringComparer.Ordinal)
            .ToArray();

        return new ResultadoBuscaDto
        {
            Termo = termoOriginal,
            TermoUsado = termoUsado,
            Correcao = correcao,
            Qualidade = qualidade,
            TotalGeral = dentro.Count,
            Grupos = grupos,
            ForaDoFiltro = selecao.ForaDoFiltro,
            FiltroProvedor = provedorFiltro is null
                ? null
                : new BuscaFiltroProvedorDto { Slug = provedorFiltro.Slug, Nome = provedorFiltro.Nome },
        };
    }

    private static string DescricaoCasamentoPorQualidade(string qualidade) => qualidade switch
    {
        "exata" => "casou com o termo pesquisado",
        "parcial" => "casou parcialmente com o termo pesquisado",
        "aproximada" => "casou com a correção sugerida para o termo pesquisado",
        _ => string.Empty,
    };

    /// <summary>
    /// P-06 — descrição do casamento por ITEM (não mais por grupo/qualidade). Prioridade: (1)
    /// casamento de CHAVE — código do erro presente no termo, frase precisa; (2) bucket de peso
    /// vencedor (A &gt; B &gt; C &gt; D), via <see cref="FrasesPorBucket"/>; (3) fallback honesto
    /// por qualidade da busca, quando nenhum bucket casou (pode acontecer no passo PARCIAL, cujo
    /// OR de lexemas não garante que TODO lexema do documento tenha pontuação isolada positiva).
    /// </summary>
    private static string DescricaoCasamentoDoItem(LinhaBusca linha, string termoUsado, string qualidade)
    {
        if (linha.TipoItem == TipoErro && CasouPelaChave(linha, termoUsado))
            return $"casou com o código {linha.Chave}";

        var bucket = linha.CasouA ? 'A' : linha.CasouB ? 'B' : linha.CasouC ? 'C' : linha.CasouD ? 'D' : (char?)null;

        if (bucket is not null && FrasesPorBucket.TryGetValue((linha.TipoItem, bucket.Value), out var frase))
            return frase;

        return DescricaoCasamentoPorQualidade(qualidade);
    }

    /// <summary>
    /// Heurística pragmática de texto (não é a normalização de <c>codex.normalizar_busca</c>,
    /// que já decidiu o casamento real no SQL): compara o termo de trabalho contra o código do
    /// erro sem diferenciar caixa. Serve só para escolher a FRASE de exibição — nunca influencia
    /// pontuação, filtro ou paginação.
    /// </summary>
    private static bool CasamentoDeChave(string chave, string termo) =>
        termo.Contains(chave, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Mesma origem de <see cref="DescricaoCasamentoDoItem"/> (chave e bucket vencedor), só que como
    /// valor fechado para a UI decidir pela estrutura. O bucket C junta causa e solução; a separação
    /// vem de <see cref="CarregarCasamentoCausaOuSolucaoAsync"/>.
    /// </summary>
    private static string? CampoCasadoDoItem(
        LinhaBusca linha, string termoUsado, IReadOnlyDictionary<int, CasamentoCausaOuSolucao> causaOuSolucao)
    {
        if (linha.TipoItem != TipoErro)
            return null;

        if (CasouPelaChave(linha, termoUsado))
            return CampoCasadoErro.Codigo;

        if (linha.CasouA || linha.CasouB)
            return CampoCasadoErro.Mensagem;

        if (!linha.CasouC || !causaOuSolucao.TryGetValue(linha.ItemId, out var casamento))
            return CampoCasadoErro.Outro;

        if (casamento.CasouCausa)
            return CampoCasadoErro.Causa;

        return casamento.CasouSolucao ? CampoCasadoErro.Solucao : CampoCasadoErro.Outro;
    }

    private static bool CasouPelaChave(LinhaBusca linha, string termoUsado) =>
        !string.IsNullOrEmpty(linha.Chave) && CasamentoDeChave(linha.Chave, termoUsado);

    /// <summary>
    /// Para os erros da página que casaram só no bucket C, diz se o termo está na causa ou na
    /// solução, cada uma com a mesma normalização do índice. Usa o OU dos lexemas: o passo forte
    /// exige todos no documento inteiro, e aqui a pergunta é em qual campo eles aparecem.
    /// O trecho sai só de causa e solução: o do índice começa pelo código e pela mensagem, que o
    /// card já mostra no título.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, CasamentoCausaOuSolucao>> CarregarCasamentoCausaOuSolucaoAsync(
        IReadOnlyList<LinhaBusca> itensDaPagina, string termoUsado, CancellationToken ct)
    {
        var erroIds = itensDaPagina
            .Where(l => l.TipoItem == TipoErro && l.CasouC && !l.CasouA && !l.CasouB && !CasouPelaChave(l, termoUsado))
            .Select(l => l.ItemId)
            .Distinct()
            .ToArray();

        if (erroIds.Length == 0)
            return new Dictionary<int, CasamentoCausaOuSolucao>();

        var casamentos = await _contexto.Database.SqlQuery<CasamentoCausaOuSolucao>($"""
            WITH lexemas AS (
                SELECT string_agg(DISTINCT '''' || replace(replace(lexema, '\', '\\'), '''', '''''') || '''', ' | ')::tsquery AS consulta
                FROM unnest(tsvector_to_array(
                    to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_consulta({termoUsado}))
                )) AS lexema
            )
            SELECT e.erroid AS "ErroId",
                   to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(e.causaprovavel, ''))) @@ lexemas.consulta AS "CasouCausa",
                   to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(e.solucaosugerida, ''))) @@ lexemas.consulta AS "CasouSolucao",
                   ts_headline('codex.codex_pt'::regconfig,
                       replace(replace(concat_ws(' ', e.causaprovavel, e.solucaosugerida), '⟦', ''), '⟧', ''),
                       lexemas.consulta,
                       'StartSel=⟦, StopSel=⟧, MaxFragments=1, MaxWords=35, MinWords=15') AS "Trecho"
            FROM codex.erros e, lexemas
            WHERE e.erroid = ANY({erroIds})
              AND lexemas.consulta IS NOT NULL
            """).ToListAsync(ct);

        return casamentos.ToDictionary(c => c.ErroId);
    }

    private static BuscaItemDto MapearItem(
        LinhaBusca linha,
        IReadOnlyDictionary<int, RotulosDoItem> rotulos,
        string termoUsado,
        string qualidade,
        IReadOnlyDictionary<int, CasamentoCausaOuSolucao> causaOuSolucao)
    {
        // Indexador, não GetValueOrDefault: CarregarRotulosAsync monta uma entrada para CADA linha
        // da página, e é exatamente a mesma lista que chega aqui. Um default silencioso devolveria
        // a URL persistida (possivelmente morta) escondendo um defeito de hidratação.
        var rotulosDoItem = rotulos[linha.ItemBuscaId];

        return new BuscaItemDto
        {
            Tipo = linha.TipoItem,
            Rotulo = RotulosItem.GetValueOrDefault(linha.TipoItem, linha.TipoItem.ToUpperInvariant()),
            Chave = linha.Chave,
            Titulo = rotulosDoItem.TituloInteiro ?? linha.Titulo,
            Url = rotulosDoItem.Url,
            ProvedorNome = rotulosDoItem.ProvedorNome,
            VersaoRotulo = rotulosDoItem.VersaoRotulo,
            Versoes = rotulosDoItem.Versoes,
            Meta = rotulosDoItem.Meta,
            DescricaoCasamento = DescricaoCasamentoDoItem(linha, termoUsado, qualidade),
            CampoCasado = CampoCasadoDoItem(linha, termoUsado, causaOuSolucao),
            SolucaoResumo = rotulosDoItem.SolucaoResumo,
            Publicado = linha.Publicado,
            Pontuacao = linha.Pontuacao,
            Trechos = ParseTrechos(TrechoDoItem(linha, causaOuSolucao)),
        };
    }

    private static string? TrechoDoItem(LinhaBusca linha, IReadOnlyDictionary<int, CasamentoCausaOuSolucao> causaOuSolucao) =>
        linha.TipoItem == TipoErro && causaOuSolucao.TryGetValue(linha.ItemId, out var casamento) && casamento.Trecho is not null
            ? casamento.Trecho
            : linha.FragmentoBruto;

    private static IReadOnlyCollection<BuscaTrechoDto> ParseTrechos(string? headline)
    {
        if (string.IsNullOrWhiteSpace(headline))
            return [];

        var partes = headline.Split([DelimitadorInicio, DelimitadorFim], StringSplitOptions.None);
        var trechos = new List<BuscaTrechoDto>();

        for (var i = 0; i < partes.Length; i++)
        {
            if (partes[i].Length == 0)
                continue;

            trechos.Add(new BuscaTrechoDto { Texto = partes[i], Destacado = i % 2 == 1 });
        }

        return trechos;
    }

    /// <summary>
    /// Hidrata provedor/versão/meta dos itens que entram na página. Estratégia deliberada: JOIN no
    /// momento da consulta, nunca colunas denormalizadas em <c>codex.itensbusca</c> — rótulo de
    /// versão e data de verificação mudam sem passar pela cascata de publicação (§5.6.1), então
    /// denormalizar criaria rótulo velho no resultado sem nenhum sinal de erro. Aqui o dado é
    /// sempre o corrente, ao custo de no máximo duas consultas por busca, limitadas aos ids da
    /// página (≤ 50 itens), fora do caminho do índice GIN.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, RotulosDoItem>> CarregarRotulosAsync(
        IReadOnlyList<LinhaBusca> itensDaPagina, bool incluirRascunho, CancellationToken ct)
    {
        if (itensDaPagina.Count == 0)
            return new Dictionary<int, RotulosDoItem>();

        var versaoIds = itensDaPagina
            .Where(l => l.VersaoProvedorId is not null)
            .Select(l => l.VersaoProvedorId!.Value)
            .Distinct()
            .ToArray();

        var erroIds = itensDaPagina
            .Where(l => l.TipoItem == TipoErro)
            .Select(l => l.ItemId)
            .Distinct()
            .ToArray();

        // Inclui as linhas do PRÓPRIO provedor (não só versão/aviso): a URL do item de tipo
        // `provedor` também é derivada daqui (decisão B), e o writer grava provedorid = itemid
        // nessas linhas.
        var provedorIds = itensDaPagina
            .Where(l => l.TipoItem is TipoProvedor or TipoVersaoProvedor or TipoAviso && l.ProvedorId is not null)
            .Select(l => l.ProvedorId!.Value)
            .Distinct()
            .ToArray();

        var artigoIds = itensDaPagina
            .Where(l => l.TipoItem == TipoArtigo)
            .Select(l => l.ItemId)
            .Distinct()
            .ToArray();

        var campoLayoutIds = itensDaPagina
            .Where(l => l.TipoItem == TipoCampoLayout)
            .Select(l => l.ItemId)
            .Distinct()
            .ToArray();

        var versoesDiretas = await CarregarVersoesDiretasAsync(versaoIds, ct);
        var versoesPorErro = await CarregarVersoesAplicaveisAsync(erroIds, incluirRascunho, ct);
        var provedores = await CarregarProvedoresAsync(provedorIds, ct);
        var artigos = await CarregarArtigosDiretosAsync(artigoIds, ct);
        var errosDiretos = await CarregarErrosDiretosAsync(erroIds, ct);
        var slugsDeCampoLayout = await CarregarSlugsDeCampoLayoutAsync(campoLayoutIds, ct);

        var rotulos = new Dictionary<int, RotulosDoItem>(itensDaPagina.Count);
        foreach (var linha in itensDaPagina)
        {
            rotulos[linha.ItemBuscaId] = MontarRotulos(
                linha, versoesDiretas, versoesPorErro, provedores, artigos, errosDiretos, slugsDeCampoLayout);
        }

        return rotulos;
    }

    /// <summary>
    /// Nome e slug ATUAIS do provedor dono das linhas <c>provedor</c>/<c>versaoprovedor</c>/
    /// <c>aviso</c>. Antes o nome vinha do <c>subtitulo</c> denormalizado da própria linha,
    /// gravado pelo writer com o nome do pai: renomear o provedor (I4) reindexa só a linha DO
    /// PROVEDOR, então a linha filha continuava exibindo o nome antigo — o BUG-03 um nível abaixo.
    /// O <c>Slug</c> entrou pela MESMA razão, agora para a URL (decisão B): trocar o slug (I37)
    /// não reindexa as filhas, e a coluna <c>itensbusca.url</c> delas apontaria para o slug morto.
    /// O JOIN é por <c>provedorid</c>, que é ID (nunca muda), não rótulo.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, ProvedorDireto>> CarregarProvedoresAsync(int[] provedorIds, CancellationToken ct)
    {
        if (provedorIds.Length == 0)
            return new Dictionary<int, ProvedorDireto>();

        var provedores = await _contexto.Provedores.AsNoTracking()
            .Where(p => provedorIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Slug })
            .ToListAsync(ct);

        return provedores.ToDictionary(p => p.Id, p => new ProvedorDireto(p.Nome, p.Slug));
    }

    /// <summary>
    /// Versão à qual o item pertence diretamente (<c>versaoprovedor</c> e aviso de escopo versão).
    /// Não refiltra por <c>Publicado</c>: a linha só chegou até aqui porque a cascata já a julgou
    /// visível — refiltrar aqui esconderia o rótulo de um item que a busca decidiu mostrar.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, VersaoDireta>> CarregarVersoesDiretasAsync(int[] versaoIds, CancellationToken ct)
    {
        if (versaoIds.Length == 0)
            return new Dictionary<int, VersaoDireta>();

        var versoes = await _contexto.VersoesProvedor.AsNoTracking()
            .Where(v => versaoIds.Contains(v.Id) && v.DesativadoEm == null)
            .Select(v => new { v.Id, v.Rotulo, v.VerificadoEm })
            .ToListAsync(ct);

        return versoes.ToDictionary(v => v.Id, v => new VersaoDireta(v.Rotulo, v.VerificadoEm));
    }

    /// <summary>
    /// Slug e data de verificação de artigo (B-5 + decisão B) — mesma disciplina de
    /// <see cref="CarregarVersoesDiretasAsync"/>: JOIN sobre os ids da página, nunca coluna
    /// denormalizada. <c>Artigo.VerificadoEm</c> é <c>DateTimeOffset?</c> no domínio
    /// (analise-backend.md §0.4); a conversão para <c>DateOnly</c> acontece aqui, na borda entre
    /// Infrastructure e o contrato público.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, ArtigoDireto>> CarregarArtigosDiretosAsync(int[] artigoIds, CancellationToken ct)
    {
        if (artigoIds.Length == 0)
            return new Dictionary<int, ArtigoDireto>();

        var artigos = await _contexto.Artigos.AsNoTracking()
            .Where(a => artigoIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Slug, a.VerificadoEm })
            .ToListAsync(ct);

        return artigos.ToDictionary(
            a => a.Id,
            a => new ArtigoDireto(
                a.Slug,
                a.VerificadoEm is null ? null : DateOnly.FromDateTime(a.VerificadoEm.Value.UtcDateTime)));
    }

    /// <summary>
    /// Slug corrente de cada erro da página (decisão B) e o resumo da solução, numa consulta só
    /// sobre os ids da página, e só quando há erro nela. Existe porque <c>codex.itensbusca</c> não
    /// tem coluna de slug e a <c>url</c> gravada envelhece na troca de slug (I43). Também traz o
    /// título inteiro do erro cujo título foi cortado em "…" na carga.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, ErroDireto>> CarregarErrosDiretosAsync(int[] erroIds, CancellationToken ct)
    {
        if (erroIds.Length == 0)
            return new Dictionary<int, ErroDireto>();

        var erros = await _contexto.Erros.AsNoTracking()
            .Where(e => erroIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Slug, e.Titulo, e.SolucaoSugerida })
            .ToListAsync(ct);

        var idsComTituloCortado = erros.Where(e => TituloCortado(e.Titulo)).Select(e => e.Id).ToArray();
        var variantes = idsComTituloCortado.Length == 0
            ? []
            : await _contexto.VariantesMensagem.AsNoTracking()
                .Where(v => idsComTituloCortado.Contains(v.ErroId) && v.DesativadoEm == null)
                .OrderBy(v => v.Id)
                .Select(v => new VarianteDoErro(v.ErroId, v.Texto))
                .ToListAsync(ct);
        var variantesPorErro = variantes.ToLookup(v => v.ErroId, v => v.Texto);

        return erros.ToDictionary(
            e => e.Id,
            e => new ErroDireto(
                e.Slug,
                ResumoSolucaoErro.Derivar(e.SolucaoSugerida),
                TituloInteiro(e.Titulo, variantesPorErro[e.Id])));
    }

    private static bool TituloCortado(string titulo) => titulo.EndsWith(Reticencias, StringComparison.Ordinal);

    /// <summary>
    /// A carga corta a mensagem longa para caber no título e guarda a inteira como variante. Vale a
    /// primeira variante que começa pelo título sem o "…"; sem nenhuma, fica nulo e o título segue.
    /// </summary>
    private static string? TituloInteiro(string titulo, IEnumerable<string> variantes)
    {
        if (!TituloCortado(titulo))
            return null;

        var inicio = titulo[..^Reticencias.Length].TrimEnd();

        return variantes
            .Select(texto => EspacosSeguidos().Replace(texto, " ").Trim())
            .FirstOrDefault(texto => inicio.Length > 0 && texto.StartsWith(inicio, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosSeguidos();

    /// <summary>
    /// Slug corrente de cada campo de layout da página. O valor coincide com o que o writer grava
    /// hoje (campo de layout não tem troca de slug em P-10), mas a ORIGEM passa a ser o JOIN:
    /// manter um dos seis tipos lendo a coluna seria deixar exatamente um caminho para regredir
    /// sozinho. Só roda na busca interna — <c>campolayout</c> não aparece na pública (M23).
    /// </summary>
    private async Task<IReadOnlyDictionary<int, string>> CarregarSlugsDeCampoLayoutAsync(int[] campoLayoutIds, CancellationToken ct)
    {
        if (campoLayoutIds.Length == 0)
            return new Dictionary<int, string>();

        var campos = await _contexto.CamposLayout.AsNoTracking()
            .Where(c => campoLayoutIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Slug })
            .ToListAsync(ct);

        return campos.ToDictionary(c => c.Id, c => c.Slug);
    }

    /// <summary>
    /// Versões aplicáveis de cada erro (N:N <c>errosversoes</c>, U9 — pode não haver nenhuma).
    /// A visibilidade acompanha a superfície: a busca pública só enxerga versão e provedor
    /// publicados (mesma regra de <c>ListarVersoesAplicaveisPublicadasAsync</c>, P6); a interna
    /// enxerga rascunho. <c>errosversoes</c> é substituída por delete físico no writer interno,
    /// então não há <c>desativadoem</c> a filtrar no vínculo.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<VersaoAplicavel>>> CarregarVersoesAplicaveisAsync(
        int[] erroIds, bool incluirRascunho, CancellationToken ct)
    {
        if (erroIds.Length == 0)
            return new Dictionary<int, IReadOnlyList<VersaoAplicavel>>();

        var consulta =
            from ev in _contexto.ErrosVersoes.AsNoTracking()
            join vp in _contexto.VersoesProvedor.AsNoTracking() on ev.VersaoProvedorId equals vp.Id
            join p in _contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
            where erroIds.Contains(ev.ErroId) && vp.DesativadoEm == null && p.DesativadoEm == null
            select new
            {
                ev.ErroId,
                ProvedorNome = p.Nome,
                VersaoRotulo = vp.Rotulo,
                ProvedorPublicado = p.Publicado,
                VersaoPublicada = vp.Publicado,
            };

        if (!incluirRascunho)
            consulta = consulta.Where(x => x.ProvedorPublicado && x.VersaoPublicada);

        var aplicaveis = await consulta
            .OrderBy(x => x.ProvedorNome)
            .ThenBy(x => x.VersaoRotulo)
            .ToListAsync(ct);

        return aplicaveis
            .GroupBy(x => x.ErroId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<VersaoAplicavel>)grupo
                    .Select(x => new VersaoAplicavel(x.ProvedorNome, x.VersaoRotulo))
                    .ToList());
    }

    /// <summary>
    /// Decide os quatro campos de contexto de um item. Para <c>erro</c>, ausência total de versão
    /// aplicável devolve os três nulos — é o sinal que a UI usa para renderizar o selo "Geral"
    /// (U9); coleção vazia não serve, porque "vazia" não é "nula" para o consumidor.
    /// </summary>
    private static RotulosDoItem MontarRotulos(
        LinhaBusca linha,
        IReadOnlyDictionary<int, VersaoDireta> versoesDiretas,
        IReadOnlyDictionary<int, IReadOnlyList<VersaoAplicavel>> versoesPorErro,
        IReadOnlyDictionary<int, ProvedorDireto> provedores,
        IReadOnlyDictionary<int, ArtigoDireto> artigos,
        IReadOnlyDictionary<int, ErroDireto> errosDiretos,
        IReadOnlyDictionary<int, string> slugsDeCampoLayout)
    {
        var url = MontarUrl(linha, provedores, artigos, errosDiretos, slugsDeCampoLayout);

        if (linha.TipoItem == TipoErro)
        {
            var erroDireto = errosDiretos.GetValueOrDefault(linha.ItemId);
            var solucaoResumo = erroDireto?.SolucaoResumo;
            var tituloInteiro = erroDireto?.TituloInteiro;

            if (!versoesPorErro.TryGetValue(linha.ItemId, out var aplicaveis) || aplicaveis.Count == 0)
                return RotulosDoItem.SemContexto(url) with { SolucaoResumo = solucaoResumo, TituloInteiro = tituloInteiro };

            var nomesDeProvedorAplicaveis = aplicaveis.Select(v => v.ProvedorNome).Distinct().ToArray();
            var provedorUnico = nomesDeProvedorAplicaveis.Length == 1 ? nomesDeProvedorAplicaveis[0] : null;

            // Com mais de um provedor o rótulo cru ("Versão 2") viraria ambíguo na tela, porque
            // não há nome de provedor único ao lado para qualificá-lo — então qualifica cada item.
            var versoes = (provedorUnico is null
                    ? aplicaveis.Select(v => $"{v.ProvedorNome}{SeparadorProvedorVersao}{v.VersaoRotulo}")
                    : aplicaveis.Select(v => v.VersaoRotulo))
                .Distinct()
                .ToArray();

            return new RotulosDoItem(
                ProvedorNome: provedorUnico,
                VersaoRotulo: provedorUnico is not null && versoes.Length == 1 ? versoes[0] : null,
                Versoes: versoes,
                Meta: null,
                Url: url,
                SolucaoResumo: solucaoResumo,
                TituloInteiro: tituloInteiro);
        }

        // Artigo não tem contexto de provedor (PQ-06) — só a meta de verificação, quando existe
        // (B-5, mesma razão de produto do selo em P7/P8: um artigo verificado não deve perder o
        // selo na busca).
        if (linha.TipoItem == TipoArtigo)
        {
            var verificadoEm = artigos.GetValueOrDefault(linha.ItemId)?.VerificadoEm;
            return new RotulosDoItem(
                ProvedorNome: null,
                VersaoRotulo: null,
                Versoes: null,
                Meta: verificadoEm is null ? null : new BuscaMetaDto { Data = verificadoEm.Value },
                Url: url);
        }

        // Nome do provedor hidratado por JOIN (CarregarNomesDeProvedorAsync), nunca pelo
        // subtitulo denormalizado da linha: o subtitulo só é reescrito quando a linha FILHA é
        // reindexada, e renomear o provedor não reindexa as filhas.
        var provedorNome = (linha.TipoItem is TipoVersaoProvedor or TipoAviso) && linha.ProvedorId is not null
            ? provedores.GetValueOrDefault(linha.ProvedorId.Value)?.Nome
            : null;

        if (linha.VersaoProvedorId is null || !versoesDiretas.TryGetValue(linha.VersaoProvedorId.Value, out var versao))
            return new RotulosDoItem(provedorNome, VersaoRotulo: null, Versoes: null, Meta: null, Url: url);

        return new RotulosDoItem(
            ProvedorNome: provedorNome,
            VersaoRotulo: versao.Rotulo,
            Versoes: null,
            // Só a própria versão exibe a data: sob o título de um aviso, "verificado em" leria
            // como se o aviso tivesse sido verificado, e não a versão a que ele pertence.
            Meta: linha.TipoItem == TipoVersaoProvedor && versao.VerificadoEm is not null
                ? new BuscaMetaDto { Data = versao.VerificadoEm.Value }
                : null,
            Url: url);
    }

    /// <summary>
    /// Decisão B (arquitetura.md §5.B) — a URL do resultado é DERIVADA do slug corrente de cada
    /// entidade, nunca lida de <c>codex.itensbusca.url</c>. A coluna continua sendo gravada pelo
    /// writer (serve ao diagnóstico e à reconstrução), mas envelhece: trocar o slug de um provedor
    /// (I37) reindexa só a linha DELE, e as linhas de versão e aviso continuariam apontando para
    /// um endereço morto — o mesmo defeito que o JOIN de <see cref="CarregarProvedoresAsync"/> já
    /// tinha resolvido para o NOME.
    ///
    /// <para>
    /// O fallback para <c>linha.Url</c> cobre a linha cuja entidade de origem não foi encontrada:
    /// a URL persistida é melhor que item sem link ou busca derrubada.
    /// </para>
    /// </summary>
    private static string MontarUrl(
        LinhaBusca linha,
        IReadOnlyDictionary<int, ProvedorDireto> provedores,
        IReadOnlyDictionary<int, ArtigoDireto> artigos,
        IReadOnlyDictionary<int, ErroDireto> errosDiretos,
        IReadOnlyDictionary<int, string> slugsDeCampoLayout)
    {
        switch (linha.TipoItem)
        {
            case TipoErro:
                var slugDoErro = errosDiretos.GetValueOrDefault(linha.ItemId)?.Slug;
                return slugDoErro is null ? linha.Url : $"/erros/{slugDoErro}";

            case TipoArtigo:
                var artigo = artigos.GetValueOrDefault(linha.ItemId);
                // RotaCanonica, nunca "/artigos/{slug}" interpolado: é a mesma fonte única que o
                // writer usa, e é ela que manda o artigo reservado para /como-funciona.
                return artigo is null ? linha.Url : SlugsReservadosArtigo.RotaCanonica(artigo.Slug);

            case TipoCampoLayout:
                var slugDoCampo = slugsDeCampoLayout.GetValueOrDefault(linha.ItemId);
                return slugDoCampo is null ? linha.Url : $"/layout/campos/{slugDoCampo}";

            case TipoProvedor:
            case TipoVersaoProvedor:
            case TipoAviso:
                return MontarUrlSobProvedor(linha, provedores);

            default:
                return linha.Url;
        }
    }

    /// <summary>
    /// Os três tipos que pendem de um provedor apontam para a página do provedor, que mostra o
    /// conteúdo da versão. O <c>#avisos</c> do aviso é âncora nessa página, nos dois escopos.
    /// </summary>
    private static string MontarUrlSobProvedor(
        LinhaBusca linha,
        IReadOnlyDictionary<int, ProvedorDireto> provedores)
    {
        if (linha.ProvedorId is null || provedores.GetValueOrDefault(linha.ProvedorId.Value) is not { } provedor)
            return linha.Url;

        var ancora = linha.TipoItem == TipoAviso ? "#avisos" : string.Empty;

        return $"/provedores/{provedor.Slug}{ancora}";
    }

    private sealed record ProvedorFiltroInfo(int Id, string Slug, string Nome);

    /// <summary>Recorte da página já calculado — o que a hidratação precisa consultar e o que a montagem final precisa contar.</summary>
    private sealed record SelecaoPagina(
        IReadOnlyList<LinhaBusca> Dentro,
        IReadOnlyList<LinhaBusca> ItensDaPagina,
        BuscaForaDoFiltroDto? ForaDoFiltro);

    /// <summary>
    /// Contexto de exibição de um item, mais a <see cref="Url"/> derivada (decisão B). Não existe
    /// mais instância estática "Nenhum": a URL é obrigatória e específica de cada linha, então o
    /// caso "geral" de U9 (os quatro campos de contexto nulos) é <see cref="SemContexto"/>.
    /// </summary>
    private sealed record RotulosDoItem(
        string? ProvedorNome,
        string? VersaoRotulo,
        IReadOnlyCollection<string>? Versoes,
        BuscaMetaDto? Meta,
        string Url,
        string? SolucaoResumo = null,
        string? TituloInteiro = null)
    {
        /// <summary>Caso "geral" de U9 — os quatro campos de contexto nulos, a URL preservada.</summary>
        public static RotulosDoItem SemContexto(string url) => new(null, null, null, null, url);
    }

    private sealed record ProvedorDireto(string Nome, string Slug);

    private sealed record ErroDireto(string Slug, string? SolucaoResumo, string? TituloInteiro);

    private sealed record VarianteDoErro(int ErroId, string Texto);

    private sealed record ArtigoDireto(string Slug, DateOnly? VerificadoEm);

    private sealed record VersaoDireta(string Rotulo, DateOnly? VerificadoEm);

    private sealed record VersaoAplicavel(string ProvedorNome, string VersaoRotulo);

    private sealed class CandidatoCorrecao
    {
        public string Termo { get; set; } = null!;

        public double Similaridade { get; set; }
    }

    private sealed class CasamentoCausaOuSolucao
    {
        public int ErroId { get; set; }

        public bool CasouCausa { get; set; }

        public bool CasouSolucao { get; set; }

        public string? Trecho { get; set; }
    }

    /// <summary>Linha bruta materializada da projeção de busca — shape interno, nunca exposto fora deste arquivo.</summary>
    private sealed class LinhaBusca
    {
        public int ItemBuscaId { get; set; }

        /// <summary>Id da entidade de ORIGEM (erro, provedor, versão…) — chave da hidratação de rótulos, não confundir com <see cref="ItemBuscaId"/>.</summary>
        public int ItemId { get; set; }

        public string TipoItem { get; set; } = null!;

        public string? Chave { get; set; }

        public string Titulo { get; set; } = null!;

        public string Url { get; set; } = null!;

        /// <summary>PQ-09/P1: sempre <c>true</c> na busca pública (só linha visível chega até aqui); reflete o estado real na interna.</summary>
        public bool Publicado { get; set; }

        public int? ProvedorId { get; set; }

        public int? VersaoProvedorId { get; set; }

        public int[]? ProvedorIds { get; set; }

        public double Pontuacao { get; set; }

        /// <summary>P-06 — bucket de peso vencedor (A &gt; B &gt; C &gt; D), calculado no SQL via <c>ts_rank</c> com máscara de pesos.</summary>
        public bool CasouA { get; set; }

        public bool CasouB { get; set; }

        public bool CasouC { get; set; }

        public bool CasouD { get; set; }

        public string? FragmentoBruto { get; set; }
    }
}
