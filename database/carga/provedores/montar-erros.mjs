/**
 * Erros de um provedor a partir da lista transcrita do material dele (código e mensagem). Só
 * funções puras.
 *
 * O vínculo com o campo só nasce de citação explícita da tag — entre parênteses `(vBC)`, depois
 * de "tag" (`tag base_calculo`), em snake_case (`aliquota_cofins`) ou pelo caminho entre aspas
 * (`“/nfse/nf/IBSCBS”`) — e quando a citação aponta para uma tag só do mapeamento. Palavra comum
 * ("O valor do tributo") não é citação, e nome que se repete em grupos diferentes seria palpite.
 */

import { LIMITE_VARIANTE, derivarTitulo } from '../emissor-nacional/transformar.mjs'

const CAMINHO_CITADO = /[“"]\/([\w/]+)[”"]/g

const NOMES_CITADOS = [
  /\(([A-Za-z_]\w*)\)/g, // "Valor da base de cálculo (vBC)"
  /\btags?\s+[“"]?([A-Za-z_]\w*)/g, // "O valor da tag base_calculo"
  /\b([a-z]+_[a-z_]+)\b/g, // "aliquota_pis e/ou aliquota_cofins"
]

function indexarPorNome(tags) {
  const porNome = new Map()
  for (const tag of tags) {
    const lista = porNome.get(tag.nomeTag) ?? []
    lista.push(tag)
    porNome.set(tag.nomeTag, lista)
  }
  return porNome
}

/** Campos InvoiCy (caminho completo no XSD de entrada) das tags que a mensagem cita sem ambiguidade. */
export function camposCitados(mensagem, tags) {
  const porNome = indexarPorNome(tags)
  const porCaminho = new Map(tags.map((tag) => [tag.caminho, tag]))
  const citadas = []

  for (const [, caminho] of mensagem.matchAll(CAMINHO_CITADO)) {
    const tag = porCaminho.get(caminho)
    if (tag) citadas.push(tag)
  }

  const nomes = new Set(
    NOMES_CITADOS.flatMap((padrao) => [...mensagem.matchAll(padrao)].map((achado) => achado[1])),
  )
  for (const nome of nomes) {
    const comEsseNome = porNome.get(nome) ?? []
    if (comEsseNome.length === 1) citadas.push(comEsseNome[0])
  }

  return [
    ...new Set(citadas.flatMap((tag) => (tag.campos ?? []).map((campo) => campo.caminhoInvoicy))),
  ]
}

/**
 * `erros`: `[{ codigo, mensagem, causaProvavel?, solucaoSugerida? }]` da transcrição. A mensagem
 * inteira é a variante — é por ela que o analista chega — e o título é a mensagem quando cabe.
 * Causa e solução só entram quando o material as traz; nunca são inventadas.
 */
export function montarErros(erros, tags, { origem = 'Prefeitura' } = {}) {
  const vistos = new Set()
  return erros.map(({ codigo, mensagem, causaProvavel = null, solucaoSugerida = null }) => {
    if (vistos.has(codigo)) throw new Error(`Código ${codigo} repetido na lista de erros.`)
    vistos.add(codigo)
    if (mensagem.length > LIMITE_VARIANTE) {
      throw new Error(
        `A mensagem de ${codigo} tem ${mensagem.length} caracteres e o limite é ${LIMITE_VARIANTE}.`,
      )
    }

    return {
      codigo,
      origem,
      titulo: derivarTitulo(mensagem),
      causaProvavel,
      solucaoSugerida,
      variantes: [mensagem],
      camposRelacionados: camposCitados(mensagem, tags),
    }
  })
}
