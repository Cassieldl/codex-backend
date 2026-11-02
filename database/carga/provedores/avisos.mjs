/**
 * Aviso que a versão precisa ter quando parte dos campos ainda não tem mapeamento confirmado. Só
 * função pura.
 *
 * A tabela de campos é exaustiva: sem o aviso, a ausência de um campo leria como "não se aplica".
 * O leitor é o analista do ERP, então o texto diz só quais campos faltam; o motivo de cada um fica
 * no relatório da carga. O Emissor Nacional usa o mesmo aviso.
 */

export const TITULO_CAMPOS_PENDENTES = 'Campos ainda em confirmação com o InvoiCy'

/** `pendentes`: nome do campo InvoiCy, ou mapa nome para motivo (o motivo não entra no texto). */
export function avisoDeCamposPendentes(pendentes) {
  const nomes = [...(pendentes instanceof Map ? pendentes.keys() : pendentes)]
  if (!nomes.length) return null

  const ordenados = [...new Set(nomes)].sort((a, b) => a.localeCompare(b, 'pt-BR'))
  return {
    severidade: 'Informativo',
    titulo: TITULO_CAMPOS_PENDENTES,
    corpo:
      'Estes campos do layout InvoiCy ainda não têm o mapeamento confirmado e, por isso, não ' +
      `aparecem na tabela desta versão: ${ordenados.join(', ')}.` +
      '\n\nFora desta lista, campo que não aparece na tabela não se aplica a esta versão.',
  }
}

/**
 * Avisos tirados das observações do provedor (ler-observacoes.mjs): um por subseção, com título e
 * severidade decididos no catálogo. Subseção sem decisão no catálogo, ou decisão sem subseção,
 * interrompe: título e severidade são editoriais e nunca saem do nome da subseção.
 *
 * `decisoes`: subseção → `{ severidade, titulo }` ou `{ ignorar: motivo }`. Devolve os avisos e o
 * que ficou de fora, com o motivo, para o relatório.
 */
export function avisosDasObservacoes(subsecoes, decisoes) {
  const lidas = new Set(subsecoes.map((item) => item.subsecao))
  const semSubsecao = Object.keys(decisoes).filter((nome) => !lidas.has(nome))
  if (semSubsecao.length)
    throw new Error(`Decisão para subseção que não existe: ${semSubsecao.join(', ')}.`)

  const avisos = []
  const foraDoAviso = []
  for (const { subsecao, corpo, removidos } of subsecoes) {
    const decisao = decisoes[subsecao]
    if (!decisao)
      throw new Error(`A subseção "${subsecao}" não tem título nem severidade no catálogo.`)
    foraDoAviso.push(...removidos.map((removido) => ({ subsecao, ...removido })))
    if (decisao.ignorar) {
      foraDoAviso.push({ subsecao, trecho: '(subseção inteira)', motivo: decisao.ignorar })
    } else if (!corpo) {
      foraDoAviso.push({
        subsecao,
        trecho: '(subseção inteira)',
        motivo: 'Nada sobrou depois das exclusões.',
      })
    } else {
      avisos.push({ severidade: decisao.severidade, titulo: decisao.titulo, corpo })
    }
  }
  return { avisos, foraDoAviso }
}
