/**
 * Converte o material coletado do Emissor Nacional (Anexo VI v1.04.00, NT 009) nos payloads da
 * API interna do Codex. Só funções puras: ler arquivo e chamar a API é papel de `importar.mjs`.
 *
 * Os limites abaixo espelham os formulários da área interna — um conteúdo que passasse na API e
 * estourasse o formulário não poderia mais ser editado por lá.
 */

export const LIMITE_TITULO = 200
export const LIMITE_CAUSA = 2000
export const LIMITE_VARIANTE = 1000
export const LIMITE_CORPO_AVISO = 4000

/** Título curto o bastante para a lista de erros; a mensagem inteira continua como variante. */
export const LIMITE_TITULO_DESEJADO = 160

/** Códigos ainda não numerados pela Sefin não entram: o analista chega ao erro pelo código. */
const PADRAO_CODIGO_DEFINITIVO = /^E\d{4}$/

/** Rejeições de forma do XML (codificação, esquema) — o resto é regra de negócio da Sefin. */
const CODIGOS_DE_SCHEMA = new Set(['E1229', 'E1235'])

const PREFIXO_NFSE_ATE_DPS = 'NFSe/infNFSe/'

function normalizarEspacos(texto) {
  return texto.replace(/\s+/g, ' ').trim()
}

/**
 * A mensagem da Sefin vira o título quando cabe. Se não couber, usa a primeira frase; se nem
 * ela couber, corta na última palavra inteira antes do limite.
 */
export function derivarTitulo(mensagem) {
  const linear = normalizarEspacos(mensagem)
  if (linear.length <= LIMITE_TITULO_DESEJADO) return linear

  const primeiraFrase = linear.match(/^.+?[.;:](?=\s|$)/)?.[0]
  if (primeiraFrase && primeiraFrase.length <= LIMITE_TITULO_DESEJADO) {
    return primeiraFrase.replace(/[;:]$/, '.')
  }

  const corte = linear.slice(0, LIMITE_TITULO_DESEJADO - 1)
  return `${corte.slice(0, corte.lastIndexOf(' '))}…`
}

export function origemDoCodigo(codigo) {
  return CODIGOS_DE_SCHEMA.has(codigo) ? 'Schema' : 'Prefeitura'
}

/** O material registra o caminho dentro da NFS-e gerada; o emitente conhece o caminho da DPS enviada. */
export function caminhoParaLeitor(xpath) {
  if (!xpath) return null
  return xpath.startsWith(`${PREFIXO_NFSE_ATE_DPS}DPS/`)
    ? xpath.slice(PREFIXO_NFSE_ATE_DPS.length)
    : xpath
}

function descreverOcorrencia(rejeicao) {
  const caminho = caminhoParaLeitor(rejeicao.xpath)
  const regra = rejeicao.regra?.trim()
  const linhas = []
  if (regra) linhas.push(`Regra da Sefin: ${regra}`)
  if (caminho) linhas.push(`Onde: ${caminho}`)
  if (rejeicao.contexto === 'Evento do emitente')
    linhas.push('Ocorre no envio de evento, não na emissão da DPS.')
  return linhas.join('\n')
}

function exigirLimite(valor, limite, descricao) {
  if (valor.length > limite) {
    throw new Error(`${descricao} tem ${valor.length} caracteres e o limite é ${limite}.`)
  }
  return valor
}

/** Códigos que só o RTC (Anexo VI) define — os provisórios já ficam de fora por outro caminho. */
export function codigosSomenteRtc(diferencas) {
  return diferencas.codigos_erro_novos_anexo_VI.filter((codigo) =>
    PADRAO_CODIGO_DEFINITIVO.test(codigo),
  )
}

/**
 * As rejeições que só existem no Anexo I chegam no material sem a regra, só com código, mensagem
 * e caminho — o mesmo formato das demais, com a regra vazia.
 */
export function rejeicoesSomenteAnexoI(diferencas) {
  return diferencas.codigos_erro_somente_anexo_I.map((rejeicao) => ({
    codigo: rejeicao.codigo,
    contexto: 'DPS/NFS-e',
    xpath: rejeicao.xpath,
    mensagem: rejeicao.mensagem,
    regra: null,
    codigo_provisorio: false,
  }))
}

/**
 * Um erro por código definitivo. O mesmo código aparece mais de uma vez no material quando a
 * Sefin o reaproveita em contextos diferentes (DPS e evento, por exemplo): vira um erro só, com
 * uma variante por mensagem distinta e uma descrição por ocorrência.
 *
 * `codigosForaDaVersao` remove as rejeições que o leiaute da versão não tem — as que só existem
 * no RTC não se aplicam a uma versão que envia o leiaute vigente.
 */
export function montarErros(rejeicoes, { codigosForaDaVersao = [] } = {}) {
  const excluidos = new Set(codigosForaDaVersao)
  const porCodigo = new Map()
  for (const rejeicao of rejeicoes) {
    if (rejeicao.codigo_provisorio || !PADRAO_CODIGO_DEFINITIVO.test(rejeicao.codigo)) continue
    if (excluidos.has(rejeicao.codigo)) continue
    const grupo = porCodigo.get(rejeicao.codigo) ?? []
    grupo.push(rejeicao)
    porCodigo.set(rejeicao.codigo, grupo)
  }

  return [...porCodigo.entries()].map(([codigo, ocorrencias]) => {
    const variantes = [...new Set(ocorrencias.map((ocorrencia) => ocorrencia.mensagem.trim()))]
    const causas = [...new Set(ocorrencias.map(descreverOcorrencia).filter(Boolean))]

    return {
      codigo,
      origem: origemDoCodigo(codigo),
      titulo: exigirLimite(derivarTitulo(variantes[0]), LIMITE_TITULO, `Título de ${codigo}`),
      variantes: variantes.map((texto) =>
        exigirLimite(texto, LIMITE_VARIANTE, `Variante de ${codigo}`),
      ),
      causaProvavel: causas.length
        ? exigirLimite(causas.join('\n\n'), LIMITE_CAUSA, `Causa provável de ${codigo}`)
        : null,
    }
  })
}

export const INTRODUCAO_VERSAO =
  'DPS no leiaute vigente (Anexo I v1.01, validado pelo XSD de 27/07/2026), como o InvoiCy a monta ' +
  'e transmite à Sefin Nacional. A Sefin gera a NFS-e, atribui o número e devolve a nota ' +
  'autorizada (cStat 100) ou a rejeição.'

/**
 * Avisos de escopo versão, todos tirados do README e dos arquivos do material. Valem para uma
 * versão que envia o leiaute vigente; os do RTC entram na versão que o implementar.
 */
export function montarAvisos() {
  const avisos = [
    {
      severidade: 'Alerta',
      titulo: 'Esta versão envia o leiaute vigente, não o RTC',
      corpo:
        'A DPS sai no leiaute do Anexo I v1.01, o aceito pelo XSD de 27/07/2026. Os campos novos do ' +
        'leiaute RTC (Anexo VI v1.04, NT 009) não são enviados: ajuste da base de cálculo (vAjusteBC), ' +
        'bens móveis, locação e unidades de imóvel, pagamentos vinculados, estorno de crédito e ' +
        'pagamento antecipado de IBS/CBS, entre outros.\n\n' +
        'Grupos que o RTC removeu ou reestruturou continuam valendo aqui, como a dedução/redução do ' +
        'ISSQN (vDedRed) e o benefício municipal (BM).\n\n' +
        'Dois domínios são mais estreitos que no RTC: finNFSe aceita só 0 (NFS-e regular) — notas de ' +
        'débito e crédito são do RTC — e opSimpNac aceita de 1 a 3.',
      vigenciaInicio: '2026-07-27',
    },
    {
      severidade: 'Alerta',
      titulo: 'Séries de 90000 a 99999 são rejeitadas',
      corpo:
        'O XSD de 27/07/2026 recusa a série da DPS nessa faixa. O que o emitente controla é a série e ' +
        'o número da DPS (nDPS); o Id da DPS é DPS + código do município (7) + tipo de inscrição (1) + ' +
        'inscrição (14) + série (5) + nDPS (15).',
      vigenciaInicio: '2026-07-27',
    },
    {
      severidade: 'Informativo',
      titulo: 'O número da NFS-e é da Sefin, e pulos na numeração são esperados',
      corpo:
        'O emitente numera a DPS, não a nota. A Sefin atribui o número da NFS-e ao gerá-la, e lacunas ' +
        'nessa sequência são esperadas (NT 007): não indicam nota perdida nem erro de integração.',
    },
    {
      severidade: 'Informativo',
      titulo: 'CNPJ alfanumérico já é aceito pelo XSD',
      corpo:
        'O XSD de julho de 2026 aceita CNPJ no formato [0-9A-Z]{14}. Os anexos do leiaute ainda ' +
        'descrevem o campo como numérico; vale o XSD.',
      vigenciaInicio: '2026-07-27',
    },
    {
      severidade: 'Alerta',
      titulo: 'Cancelamento depende da parametrização do município emissor',
      corpo:
        'O cancelamento (evento 101101) é pedido ao ambiente que gerou a NFS-e. Prazo, valor máximo e ' +
        'exigência de tomador são definidos pelo município (rejeições E0822, E0823 e E0824), e o ' +
        'cancelamento é bloqueado quando já existe evento de tributos recolhidos (E0827).\n\n' +
        'Quando o cancelamento direto não é permitido, o caminho é a solicitação de análise fiscal ' +
        '(101103), que o município defere (105104) ou indefere (105105). Para trocar uma nota por ' +
        'outra, emita nova DPS com o grupo subst (chSubstda e cMotivo 01 a 05 ou 99): o cancelamento ' +
        'por substituição (105102) é gerado automaticamente.',
    },
  ]

  for (const aviso of avisos)
    exigirLimite(aviso.corpo, LIMITE_CORPO_AVISO, `Corpo do aviso "${aviso.titulo}"`)
  return avisos
}
