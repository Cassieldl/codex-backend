/**
 * Vocabulário canônico do Layout InvoiCy, o mesmo para todas as cargas de provedor. A ordem dos
 * campos é a posição no XSD entre os que entram no vocabulário: se cada carga decidisse sozinha
 * quais grupos entram, a ordem mudaria a cada execução de um provedor diferente.
 */

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { CAMINHOS_EM_DUVIDA } from './emissor-nacional/resolver-mapeamento.mjs'
import { lerXsd, montarVocabulario } from './emissor-nacional/transformar-layout.mjs'

const PASTA_DO_LAYOUT = join(dirname(fileURLToPath(import.meta.url)), 'emissor-nacional')

/**
 * Grupos do XSD que algum provedor mapeia como campo — as listas. Grupo puramente estrutural não é
 * vocabulário; um grupo novo entra aqui, e não por efeito de rodar uma carga.
 */
export const GRUPOS_COMO_CAMPO = new Set([
  'Envio/RPS/ListaDed/Ded',
  'Envio/RPS/ListaNFSeReferenciadas',
  'Envio/RPS/ListaItens',
  'Envio/RPS/ListaParcelas',
])

/** Interrompe a carga quando um provedor mapeia como campo um grupo que o vocabulário não tem. */
export function exigirGruposConhecidos(caminhosUsados, elementosXsd) {
  const grupos = new Set(elementosXsd.filter((item) => item.ehGrupo).map((item) => item.caminho))
  const desconhecidos = [...new Set(caminhosUsados)].filter(
    (caminho) => grupos.has(caminho) && caminho !== 'Envio/RPS' && !GRUPOS_COMO_CAMPO.has(caminho),
  )
  if (desconhecidos.length) {
    throw new Error(
      `Grupos mapeados como campo fora de GRUPOS_COMO_CAMPO: ${desconhecidos.join(', ')}.`,
    )
  }
}

/** Elementos do XSD de entrada do InvoiCy e o vocabulário canônico montado a partir deles. */
export async function lerVocabularioInvoicy() {
  const [xsd, planilha] = await Promise.all([
    readFile(join(PASTA_DO_LAYOUT, 'layout-invoicy-v8.xsd'), 'utf8'),
    readFile(join(PASTA_DO_LAYOUT, 'layout-invoicy-v8-planilha.json'), 'utf8'),
  ])
  const elementos = lerXsd(xsd)
  const vocabulario = montarVocabulario(
    elementos,
    JSON.parse(planilha),
    GRUPOS_COMO_CAMPO,
    CAMINHOS_EM_DUVIDA,
  )
  return { elementos, vocabulario }
}
