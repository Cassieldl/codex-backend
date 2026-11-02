/**
 * Aviso que a versão precisa ter quando parte do mapeamento espera o InvoiCy. Só função pura.
 *
 * A tabela de campos é exaustiva: sem o aviso, a ausência de um campo leria como "não se aplica".
 * O título é o mesmo do Emissor Nacional, para quem lê reconhecer a situação.
 */

export const TITULO_CAMPOS_PENDENTES = 'Campos ainda em confirmação com o InvoiCy'

/** `pendentes`: nome do campo InvoiCy → motivo. Agrupa os campos pelo motivo. */
export function avisoDeCamposPendentes(pendentes) {
  if (!pendentes.size) return null

  const porMotivo = new Map()
  for (const [nome, motivo] of pendentes) {
    const nomes = porMotivo.get(motivo) ?? []
    nomes.push(nome)
    porMotivo.set(motivo, nomes)
  }

  const itens = [...porMotivo].map(([motivo, nomes]) => `- ${nomes.join(', ')}: ${motivo}`)
  return {
    severidade: 'Informativo',
    titulo: TITULO_CAMPOS_PENDENTES,
    corpo:
      'A configuração deste provedor no InvoiCy usa estes campos, mas o caminho deles no layout ' +
      'InvoiCy ainda está sendo confirmado, por isso não aparecem na tabela de campos:\n\n' +
      itens.join('\n'),
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
