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
      'InvoiCy ainda está sendo confirmado — por isso não aparecem na tabela de campos:\n\n' +
      itens.join('\n'),
  }
}
