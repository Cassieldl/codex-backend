/**
 * Lê o mapeamento De/Para de um provedor (exportação da configuração do InvoiCy, em Markdown) e
 * monta o caminho de cada tag pela coluna "Dentro do grupo". Só funções puras.
 *
 * Formato da linha:
 *   | **seq** | tag | Grupo/Elemento/Atributo | "N - pai" | campo InvoiCy | ocorrência | descrição | Interno/Externo |
 *
 * O sequencial vem em negrito em umas exportações e sem negrito em outras: as duas formas valem.
 *
 * A extração original pulou alguns sequenciais (a observação ao pé de cada arquivo diz quais); por
 * isso a tag é identificada pelo sequencial, nunca pela posição na tabela.
 */

const LINHA_DE_TAG = /^\|\s*(\*\*)?\d+\1\s*\|/

const ELEMENTO = { Grupo: 'G', Elemento: 'E', Atributo: 'A' }

function limpar(texto) {
  // Algumas descrições vieram com a aspa de abertura da célula da planilha ("Total valor…).
  return (
    texto
      ?.replace(/^"+|"+$/g, '')
      .replace(/\s+/g, ' ')
      .trim() || null
  )
}

export function lerMapeamento(markdown) {
  const linhas = markdown.split(/\r?\n/).filter((linha) => LINHA_DE_TAG.test(linha))
  if (!linhas.length) throw new Error('O mapeamento não tem nenhuma linha de tag.')

  const tags = linhas.map((linha) => {
    const celulas = linha
      .split('|')
      .slice(1, -1)
      .map((celula) => celula.trim())
    if (celulas.length < 8) throw new Error(`Linha com ${celulas.length} colunas: ${linha}`)
    const [sequencial, nomeTag, elemento, dentroDoGrupo, nomeCampo, ocorrencia, descricao, origem] =
      celulas

    const pai = dentroDoGrupo.match(/^(\d+)\s*-/)
    if (!pai)
      throw new Error(`Sequencial ${sequencial}: "Dentro do grupo" ilegível (${dentroDoGrupo}).`)
    if (!ELEMENTO[elemento]) throw new Error(`Sequencial ${sequencial}: elemento "${elemento}".`)

    return {
      sequencial: Number(sequencial.replace(/\*/g, '')),
      nomeTag,
      elemento: ELEMENTO[elemento],
      sequencialPai: Number(pai[1]),
      nomeCampo: limpar(nomeCampo),
      ocorrencia: limpar(ocorrencia),
      descricao: limpar(descricao),
      interno: origem === 'Interno',
    }
  })

  const porSequencial = new Map()
  for (const tag of tags) {
    if (porSequencial.has(tag.sequencial))
      throw new Error(`Sequencial ${tag.sequencial} repetido no mapeamento.`)
    porSequencial.set(tag.sequencial, tag)
  }

  const caminhoDe = (tag) => {
    if (tag.sequencialPai === 0) return tag.nomeTag
    const pai = porSequencial.get(tag.sequencialPai)
    if (!pai)
      throw new Error(
        `Sequencial ${tag.sequencial} aponta para o pai ${tag.sequencialPai}, que não existe.`,
      )
    return `${caminhoDe(pai)}/${tag.nomeTag}`
  }

  return tags.map((tag) => ({ ...tag, caminho: caminhoDe(tag) }))
}
