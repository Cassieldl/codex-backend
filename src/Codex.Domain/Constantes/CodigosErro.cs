namespace Codex.Domain.Constantes;

/// <summary>
/// Códigos de erro que o cliente distingue, em <c>UPPER_SNAKE_CASE</c> (rules/api.md). Fonte
/// única: o código aparece no envelope de erro e nos testes de contrato, e literal repetido nos
/// dois lados é divergência esperando acontecer.
/// </summary>
/// <remarks>
/// Só entra aqui o código que o cliente precisa <b>distinguir</b> de uma violação de regra
/// qualquer. Violação sem tratamento próprio no cliente continua no genérico
/// <see cref="Exceptions.RegraDeNegocioException.CodigoGenerico"/>.
/// </remarks>
public static class CodigosErro
{
    /// <summary>Artigo obrigatório não pode ser removido nem despublicado (analise-backend.md §5.2, I27/I28).</summary>
    public const string ArtigoReservado = "ARTIGO_RESERVADO";

    /// <summary>Publicar comunicado já expirado (analise-backend.md §6.2, I35 — decisão B-4).</summary>
    public const string ComunicadoExpirado = "COMUNICADO_EXPIRADO";

    /// <summary>
    /// I37/I43/I44 com o slug normalizado igual ao já persistido (analise-backend.md §0.3, 422).
    /// Não vira 409 pelo UNIQUE: "igual ao atual" não colide com linha nenhuma — o <c>UPDATE</c>
    /// passaria em silêncio e o redirect gravado apontaria de um caminho para ele mesmo.
    /// </summary>
    public const string SlugIgualAoAtual = "SLUG_IGUAL_AO_ATUAL";

    /// <summary>
    /// I44 tentando trocar o slug do artigo reservado (analise-backend.md §0.2, 422). Trocar
    /// deixaria a rota fixa <c>/como-funciona</c> órfã em silêncio, sem nenhuma exceção.
    /// </summary>
    public const string SlugReservadoNaoPodeMudar = "SLUG_RESERVADO";

    /// <summary>P11 sobre versão de layout existente e não publicada (analise-backend.md §4.3, 410).</summary>
    public const string VersaoLayoutDespublicada = "VERSAO_LAYOUT_DESPUBLICADA";

    /// <summary>P12 sobre campo de layout existente e não visível pelo predicado A1 (analise-backend.md §4.1, 410).</summary>
    public const string CampoLayoutDespublicado = "CAMPO_LAYOUT_DESPUBLICADO";

    /// <summary>
    /// I15d promovendo uma segunda <c>VersaoLayout</c> a <c>Vigente</c> (PQ-17, analise-backend.md
    /// §4.6, 409). Traduzido da violação de <c>uq_codex_versoeslayout_estado_vigente</c> por
    /// <c>ConstraintName</c> — nunca por texto de mensagem.
    /// </summary>
    public const string VersaoLayoutVigenteJaExiste = "VERSAO_LAYOUT_VIGENTE_JA_EXISTE";

    /// <summary>
    /// I8b trocando <c>VersaoLayoutId</c> para um layout inexistente ou desativado
    /// (422). Distinto de <see cref="VersaoLayoutDespublicada"/>
    /// (410, layout existe mas não publicado) — aqui o layout não é um destino válido de jeito
    /// nenhum.
    /// </summary>
    public const string VersaoLayoutInvalida = "VERSAO_LAYOUT_INVALIDA";

    /// <summary>
    /// I8b trocando <c>VersaoLayoutId</c> de uma versão de provedor que já tem
    /// <c>CampoVersao</c> cadastrado (422). Cada campo da
    /// versão só existe porque o campo canônico tem origem EXATAMENTE no layout atual
    /// (<c>CampoVersaoRepositorioInterno.CampoLayoutEstaAtivoNaVersaoDoProvedorAsync</c>) — trocar
    /// o layout com campos já cadastrados quebraria esse vínculo em silêncio.
    /// </summary>
    public const string VersaoComCampos = "VERSAO_COM_CAMPOS";

    /// <summary>
    /// 422: criação de campo de versão revalidada
    /// SOB LOCK da versão descobre que o layout de origem mudou entre a validação (FluentValidation,
    /// antes do service) e a escrita — a troca de layout (PUT da versão) comitou no meio da janela.
    /// Código próprio, não <see cref="VersaoLayoutInvalida"/>: aqui o layout de DESTINO da troca é
    /// válido, o problema é a corrida com esta criação de campo específica.
    /// </summary>
    public const string CampoLayoutOrigemAlterado = "LAYOUT_ALTERADO";

    /// <summary>
    /// Aviso de versão apontando campo afetado que não existe, foi removido ou pertence a outra
    /// versão (422). Aceitar gravaria um vínculo para um campo que a página da versão
    /// não mostra, e o link do aviso levaria a lugar nenhum.
    /// </summary>
    public const string CampoAfetadoInvalido = "CAMPO_AFETADO_INVALIDO";
}
