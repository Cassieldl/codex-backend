/**
 * De-para da natureza da operação por versão de provedor, lido do artigo `natureza-da-operacao.md`
 * (a mesma fonte publicada como artigo). Cada versão aponta para uma seção do artigo; a tabela da
 * seção vira a lista gravada em `PUT /interno/versoes/{id}/naturezas-operacao`. Só funções puras,
 * exceto a leitura do artigo.
 */

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

export const ARTIGO_NATUREZA_OPERACAO = join(
  dirname(fileURLToPath(import.meta.url)),
  'artigos',
  'documentacao',
  'natureza-da-operacao.md',
)

/** Título da seção do artigo com a tabela de cada grupo de provedores. */
export const TABELAS_NATUREZA = {
  nacional: 'Padrão Nacional e provedores de leiaute nacional',
  elotech: 'Tributos Web - Elotech 2.03',
  giss: 'GISS',
  ipm: 'IPM',
}

const CODIGO_IMUNIDADE = 4
const CAMPO_TIPO_IMUNIDADE = 'tpImunidade'

const COLUNA_CODIGO = 'Código no InvoiCy'
const COLUNA_VALOR = 'Valor na prefeitura'
const COLUNA_SIGLA = 'Sigla'
const COLUNA_DEDUCAO = 'Admite dedução'
const COLUNA_ISS_RETIDO = 'Admite ISS retido'

const TITULO = /^#{2,6}\s+(.+?)\s*$/
const MARCA_A_CONFIRMAR = /\(?\s*a confirmar[^)]*\)?/i

export async function lerArtigoNaturezaOperacao(caminho = ARTIGO_NATUREZA_OPERACAO) {
  return readFile(caminho, 'utf8')
}

function celulas(linha) {
  return linha
    .split('|')
    .slice(1, -1)
    .map((celula) => celula.trim())
}

/** Linhas da primeira tabela da seção, como objetos indexados pelo cabeçalho. Aceita tabela dentro de bloco `>`. */
export function lerTabelaDaSecao(markdown, titulo) {
  const linhas = markdown.split(/\r?\n/)
  const inicio = linhas.findIndex((linha) => linha.match(TITULO)?.[1] === titulo)
  if (inicio < 0) throw new Error(`Seção "${titulo}" não existe no artigo de natureza da operação.`)

  const tabela = []
  for (const linha of linhas.slice(inicio + 1)) {
    if (TITULO.test(linha)) break
    const semCitacao = linha.replace(/^>\s?/, '').trim()
    if (semCitacao.startsWith('|')) tabela.push(semCitacao)
    else if (tabela.length) break
  }
  if (tabela.length < 3) throw new Error(`Seção "${titulo}" não tem tabela.`)

  const [cabecalho, , ...dados] = tabela.map(celulas)
  for (const coluna of [COLUNA_CODIGO, COLUNA_VALOR]) {
    if (!cabecalho.includes(coluna)) throw new Error(`Seção "${titulo}" sem a coluna "${coluna}".`)
  }
  const colunaDescricao = cabecalho.find((coluna) => coluna.startsWith('Descrição'))
  if (!colunaDescricao) throw new Error(`Seção "${titulo}" sem coluna de descrição.`)

  return dados.map((valores) => {
    const linha = Object.fromEntries(
      cabecalho.map((coluna, indice) => [coluna, valores[indice] ?? '']),
    )
    return { ...linha, descricao: linha[colunaDescricao] }
  })
}

/**
 * Tag do provedor que recebe o `tpImunidade` do InvoiCy, no formato "tag (grupo pai)", ou null
 * quando o mapeamento não leva o tipo de imunidade. `tags` vem do leitor de mapeamento
 * (`nomeCampo` + `caminho`).
 */
export function destinoDoTipoDeImunidade(tags) {
  const tag = tags.find((item) => item.nomeCampo === CAMPO_TIPO_IMUNIDADE && !item.interno)
  if (!tag) return null

  const partes = tag.caminho.split('/')
  const nome = partes.at(-1)
  const grupo = partes.at(-2)
  return grupo ? `${nome} (grupo ${grupo})` : nome
}

function notaDaImunidade(destino) {
  if (!destino)
    return `Este provedor não recebe o tipo de imunidade: o ${CAMPO_TIPO_IMUNIDADE} do layout InvoiCy não é enviado à prefeitura.`

  return (
    `Ao usar esta natureza, informe o tipo de imunidade no campo ${CAMPO_TIPO_IMUNIDADE} do layout InvoiCy. ` +
    `Neste provedor, o tipo vai na tag ${destino}.`
  )
}

/** "Não (63)" vira "não (rejeição 63)"; "—" ou vazio não entra. */
function regraComRejeicao(rotulo, valor) {
  if (!valor || valor === '—' || valor === '-') return null

  const texto = valor
    .replace(/\(([^)]*?)(\d[^)]*)\)/, (_, antes, codigos) => `(${antes}rejeição ${codigos})`)
    .replace(/–/g, '-')
  return `${rotulo}: ${texto.charAt(0).toLowerCase()}${texto.slice(1)}.`
}

function montarItem(linha, destinoImunidade) {
  const codigo = Number(linha[COLUNA_CODIGO])
  if (!Number.isInteger(codigo))
    throw new Error(`Código do InvoiCy ilegível: "${linha[COLUNA_CODIGO]}".`)

  const sigla = linha[COLUNA_SIGLA]?.replace(MARCA_A_CONFIRMAR, '').trim()
  const aConfirmar = Object.values(linha).some((valor) => MARCA_A_CONFIRMAR.test(valor))

  const observacoes = [
    regraComRejeicao('Dedução', linha[COLUNA_DEDUCAO]),
    regraComRejeicao('ISS retido', linha[COLUNA_ISS_RETIDO]),
    codigo === CODIGO_IMUNIDADE ? notaDaImunidade(destinoImunidade) : null,
  ].filter(Boolean)

  return {
    codigoInvoicy: codigo,
    descricao: sigla ? `${linha.descricao} (${sigla})` : linha.descricao,
    valorProvedor: linha[COLUNA_VALOR],
    observacao: observacoes.length ? observacoes.join(' ') : null,
    aConfirmar,
  }
}

/** Itens do PUT de naturezas da versão, na ordem da tabela do artigo. */
export function montarNaturezasOperacao(markdown, titulo, destinoImunidade) {
  const itens = lerTabelaDaSecao(markdown, titulo).map((linha) =>
    montarItem(linha, destinoImunidade),
  )

  const codigos = itens.map((item) => item.codigoInvoicy)
  const repetido = codigos.find((codigo, indice) => codigos.indexOf(codigo) !== indice)
  if (repetido !== undefined) throw new Error(`Código ${repetido} repetido na seção "${titulo}".`)

  return itens
}
