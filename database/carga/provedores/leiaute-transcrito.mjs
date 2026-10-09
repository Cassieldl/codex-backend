/**
 * Definições das tags de um provedor sem XSD, a partir da tabela do manual transcrita para JSON
 * (Tag | Domínio | Tamanho | Obrig. | Descrição). Só funções puras.
 *
 * Produz a mesma forma que o leitor de XSD, para a montagem dos campos não precisar saber de onde
 * o comportamento veio. O que o manual não diz fica vazio — nunca deduzido.
 */

const NUMERICOS = new Set(['numérico', 'numerico', 'real', 'decimal', 'inteiro'])
const TEXTOS = new Set(['alfa', 'alfanumérico', 'alfanumerico', 'caractere', 'texto'])
/** "Agrupador", e também "Agrup." quando a célula do manual quebra em duas linhas. */
const GRUPO = /^(agrup|grupo)/i

function tipoBaseDe(dominio) {
  const chave = dominio?.trim().toLowerCase()
  if (!chave) return null
  if (NUMERICOS.has(chave)) return 'decimal'
  if (TEXTOS.has(chave)) return 'string'
  if (chave === 'data') return 'date'
  return null
}

/**
 * `transcricao.tags`: um item por sequencial do mapeamento. Devolve caminho do mapeamento →
 * definição; tag não encontrada no manual fica de fora, e a montagem a trata como "não descrita".
 */
export function definicoesDeLeiauteTranscrito(transcricao, tags) {
  const porSequencial = new Map(tags.map((tag) => [tag.sequencial, tag]))
  const definicoes = new Map()

  for (const item of transcricao.tags) {
    if (!item.encontradaNoPdf) continue
    const tag = porSequencial.get(item.sequencial)
    if (!tag)
      throw new Error(
        `A transcrição cita o sequencial ${item.sequencial}, que o mapeamento não tem.`,
      )
    if (tag.nomeTag !== item.tag)
      throw new Error(
        `Sequencial ${item.sequencial}: o mapeamento diz ${tag.nomeTag}, a transcrição ${item.tag}.`,
      )

    const ehGrupo = GRUPO.test(item.dominio?.trim() ?? '')
    const tamanho = /^\d+$/.test(item.tamanho ?? '') ? item.tamanho : null
    const obrigatoriedade = item.obrigatoriedade?.match(/^(\d)-(\d|N|n)$/)

    definicoes.set(tag.caminho, {
      caminho: tag.caminho,
      nome: tag.nomeTag,
      ehAtributo: false,
      ehGrupo,
      minimo: obrigatoriedade ? obrigatoriedade[1] : null,
      maximo: obrigatoriedade ? obrigatoriedade[2] : null,
      emEscolha: false,
      compositorOpcional: false,
      tipoBase: ehGrupo ? null : tipoBaseDe(item.dominio),
      nomeTipo: null,
      tamanhoMaximo: ehGrupo ? undefined : (tamanho ?? undefined),
      valoresAceitos: [],
      documentacao: item.descricao ?? null,
    })
  }
  return definicoes
}
