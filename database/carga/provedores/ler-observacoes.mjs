/**
 * Lê a seção de observações de um provedor (Markdown, uma seção `##` com subseções `###`) e devolve
 * o texto de cada subseção na forma que o corpo do aviso aceita: texto puro, parágrafos separados
 * por linha em branco e listas com `- `. O portal mostra o corpo como texto, sem interpretar
 * Markdown. Só funções puras.
 *
 * Fica fora do corpo: imagem, marca de fonte (`[obs]`, `[portal]`...), trecho com pendência e os
 * trechos de TRECHOS_FORA_DO_AVISO. Link externo continua com a URL completa; link para outro
 * documento da base vira só o rótulo.
 */

/** Chave da parte da seção que vem antes da primeira subseção, quando ela tem conteúdo. */
export const SEM_SUBSECAO = ''

const MARCA_DE_PENDENCIA = /\bPendente\b/

/** Começo de item que não vira aviso, com o motivo. */
export const TRECHOS_FORA_DO_AVISO = [
  {
    inicio: '**Natureza da operação (leiaute nacional):**',
    motivo:
      'A lista dos provedores que usam a tabela do Padrão Nacional ainda não foi confirmada provedor a provedor.',
  },
  {
    inicio: '**`ItemTributavel`:**',
    motivo: 'A lista de atividades citada só existe em imagem.',
  },
]

/** Linhas de identificação da seção, que apontam para outros documentos da base. */
const CABECALHO = /^- \*\*(Layout|Rejeições|Fontes):\*\*/

const ITEM_DE_LISTA = /^(\s*)(- |\d+\. )/
const NOMES_DE_FONTE = '(?:obs|portal antigo|portal|validado)'
const MARCA_DE_FONTE = new RegExp(`\\[${NOMES_DE_FONTE}(?:,\\s*${NOMES_DE_FONTE})*\\](?!\\()`, 'g')
const MARCA_NO_MEIO_DA_FRASE = new RegExp(`\\[(${NOMES_DE_FONTE})\\](?=\\s+\\p{Ll})`, 'gu')
const FONTE_COM_LINK = /\[\[([^\]]+)\]\((https?:\/\/[^)\s]+)\),\s*([^\]]+)\]/g
const LINK = /\[([^\]]+)\]\(([^)\s]+)\)/g
// Link cujo rótulo é o nome de um arquivo da base, com a preposição que o apresenta.
const LINK_PARA_ARQUIVO = /\s+(?:em|no|na)\s+\[[^\]]+\.md\]\([^)\s]+\)/g
const LINK_AUTOMATICO = /<(https?:\/\/[^>\s]+)>/g
// Meia-risca e travessão usados como pausa na frase (com espaço dos dois lados) viram hífen. Montado
// pelo código de ponto para o próprio arquivo não ter o caractere.
const TRAVESSAO_NA_FRASE = new RegExp(` [${String.fromCodePoint(0x2013, 0x2014)}] `, 'g')

/** Marcas de Markdown e de fonte viram texto puro. */
export function textoPuro(markdown) {
  return markdown
    .replace(FONTE_COM_LINK, '($1, $2, $3)')
    .replace(MARCA_NO_MEIO_DA_FRASE, '$1')
    .replace(MARCA_DE_FONTE, '')
    .replace(LINK_PARA_ARQUIVO, '')
    .replace(LINK, (_, rotulo, destino) =>
      /^https?:\/\//.test(destino) ? `${rotulo} (${destino})` : rotulo,
    )
    .replace(LINK_AUTOMATICO, '$1')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/\*([^*\s][^*]*)\*/g, '$1')
    .replace(/`([^`]+)`/g, '$1')
    .replace(TRAVESSAO_NA_FRASE, ' - ')
    .replace(/[ \t]+([.,;:])/g, '$1')
    .replace(/(\S)[ \t]{2,}/g, '$1 ')
    .trimEnd()
}

function celulas(linha) {
  return linha
    .split('|')
    .slice(1, -1)
    .map((celula) => celula.trim())
}

/** Tabela vira lista: a primeira coluna rotula a linha, as outras entram com o nome da coluna. */
function tabelaComoLista(linhas) {
  const [cabecalho, , ...linhasDeDados] = linhas.map(celulas)
  return linhasDeDados.map((linha) => {
    const [rotulo, descricao, ...resto] = linha
    const extras = resto
      .map((valor, indice) => [cabecalho[indice + 2], valor])
      .filter(([, valor]) => valor && valor !== '-')
      .map(([nome, valor]) => `; ${nome.toLowerCase()}: ${valor}`)
    return `- ${rotulo}: ${descricao}${extras.join('')}`
  })
}

/** Unidades de texto (item de lista ou parágrafo), na ordem, já sem imagem e sem tabela. */
function unidadesDe(linhas) {
  const unidades = []
  let quebra = true
  for (let indice = 0; indice < linhas.length; indice++) {
    const linha = linhas[indice]
    if (!linha.trim()) {
      quebra = true
      continue
    }
    if (linha.trim().startsWith('![')) {
      const anterior = unidades.at(-1)
      if (anterior) anterior.antesDeImagem = true
      continue
    }
    if (linha.trim().startsWith('|')) {
      const tabela = []
      while (indice < linhas.length && linhas[indice].trim().startsWith('|'))
        tabela.push(linhas[indice++].trim())
      indice--
      for (const item of tabelaComoLista(tabela)) unidades.push({ item: true, texto: item })
      quebra = true
      continue
    }
    if (ITEM_DE_LISTA.test(linha)) {
      unidades.push({ item: true, texto: linha.trimEnd() })
    } else if (!quebra && unidades.length) {
      unidades.at(-1).texto += ` ${linha.trim()}`
    } else {
      unidades.push({ item: false, texto: linha.trim() })
    }
    quebra = false
  }
  return unidades
}

function semConteudo(texto) {
  return !texto
    .replace(/^\s*(-|\d+\.)(\s|$)/, '')
    .replace(/\*\*[^*]*\*\*/g, '')
    .replace(/[\s.:;,]/g, '')
}

/**
 * Aplica as regras de exclusão a uma unidade. Devolve o texto que fica (ou `null`) e, quando algo
 * saiu, o motivo.
 */
function filtrar(unidade) {
  const conteudo = unidade.texto.replace(ITEM_DE_LISTA, '')
  const fora = TRECHOS_FORA_DO_AVISO.find((trecho) => conteudo.startsWith(trecho.inicio))
  if (fora) return { texto: null, removido: { trecho: unidade.texto, motivo: fora.motivo } }

  const pendencia = unidade.texto.search(MARCA_DE_PENDENCIA)
  if (pendencia >= 0) {
    const antes = unidade.texto.slice(0, pendencia).trimEnd()
    // A marca pode vir em negrito (`**Pendente:**`): o negrito que ficou aberto sai junto.
    const negritoAberto = (antes.match(/\*\*/g) ?? []).length % 2 === 1
    const resto = negritoAberto ? antes.replace(/\*\*$/, '').trimEnd() : antes
    return {
      texto: semConteudo(resto) ? null : resto,
      removido: { trecho: unidade.texto.slice(pendencia), motivo: 'Marcado como pendente.' },
    }
  }

  // Parágrafo que só apresentava uma imagem não diz nada sem ela.
  if (unidade.antesDeImagem && !unidade.item && unidade.texto.endsWith(':'))
    return { texto: null, removido: { trecho: unidade.texto, motivo: 'Apresenta só uma imagem.' } }
  if (unidade.antesDeImagem && unidade.texto.endsWith(':'))
    return { texto: `${unidade.texto.slice(0, -1)}.` }
  return { texto: unidade.texto }
}

function montarCorpo(linhas) {
  const removidos = []
  const partes = []
  for (const unidade of unidadesDe(linhas)) {
    const { texto, removido } = filtrar(unidade)
    if (removido) removidos.push(removido)
    if (texto) partes.push({ item: unidade.item, texto: textoPuro(texto) })
  }
  const corpo = partes
    .map((parte, indice) =>
      indice === 0
        ? parte.texto
        : `${parte.item && partes[indice - 1].item ? '\n' : '\n\n'}${parte.texto}`,
    )
    .join('')
  return { corpo, removidos }
}

/**
 * Subseções da seção de um provedor: `{ subsecao, corpo, removidos }`. A parte antes da primeira
 * subseção entra com a chave SEM_SUBSECAO quando, sem o cabeçalho de identificação, sobra texto.
 * Corpo vazio significa que tudo saiu pelas regras de exclusão.
 */
export function lerObservacoes(markdown) {
  const linhas = markdown.split(/\r?\n/)
  if (!linhas[0]?.startsWith('## ')) throw new Error('As observações não começam pela seção ##.')

  const blocos = [{ subsecao: SEM_SUBSECAO, linhas: [] }]
  for (const linha of linhas.slice(1)) {
    if (linha.startsWith('### ')) blocos.push({ subsecao: linha.slice(4).trim(), linhas: [] })
    else blocos.at(-1).linhas.push(linha)
  }
  blocos[0].linhas = blocos[0].linhas.filter((linha) => !CABECALHO.test(linha))

  return blocos
    .filter((bloco) => bloco.subsecao !== SEM_SUBSECAO || bloco.linhas.some((l) => l.trim()))
    .map((bloco) => ({ subsecao: bloco.subsecao, ...montarCorpo(bloco.linhas) }))
}
