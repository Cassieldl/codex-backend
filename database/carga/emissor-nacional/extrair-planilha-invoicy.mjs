/**
 * Extrai da planilha de layout do InvoiCy (.xlsx) o JSON versionado ao lado do XSD
 * (`layout-invoicy-v<N>-planilha.json`). Sem dependência: o xlsx é um zip de XMLs, lido aqui com
 * o zlib do Node. Só funções puras, mais o `main` no fim.
 *
 * Uso:
 *   node database/carga/emissor-nacional/extrair-planilha-invoicy.mjs <arquivo.xlsx> <rotulo>
 */

import { readFile, writeFile } from 'node:fs/promises'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { inflateRawSync } from 'node:zlib'

export const ABA_DE_ENVIO = 'Envio_Recepção de RPS'
export const ABA_DE_VERSOES = 'Versões com alterações RT'

const ASSINATURA_DIRETORIO_CENTRAL = 0x02014b50
const ASSINATURA_FIM_DIRETORIO = 0x06054b50

const RISCADO = /<strike(?!\s+val="(?:0|false)")\b/

/** Preenchimentos com que o InvoiCy pinta o campo da reforma tributária: verde e amarelo. */
const CORES_DA_REFORMA_TRIBUTARIA = new Set(['FF00FF00', 'FFFFFF00'])

/** Arquivos do zip por nome. Lê pelo diretório central, que é quem tem os tamanhos confiáveis. */
export function lerZip(conteudo) {
  let fim = conteudo.length - 22
  while (fim >= 0 && conteudo.readUInt32LE(fim) !== ASSINATURA_FIM_DIRETORIO) fim--
  if (fim < 0) throw new Error('Arquivo não é um zip: diretório central não encontrado.')

  const total = conteudo.readUInt16LE(fim + 10)
  let posicao = conteudo.readUInt32LE(fim + 16)
  const arquivos = new Map()

  for (let indice = 0; indice < total; indice++) {
    if (conteudo.readUInt32LE(posicao) !== ASSINATURA_DIRETORIO_CENTRAL)
      throw new Error('Diretório central do zip corrompido.')

    const metodo = conteudo.readUInt16LE(posicao + 10)
    const tamanhoComprimido = conteudo.readUInt32LE(posicao + 20)
    const tamanhoNome = conteudo.readUInt16LE(posicao + 28)
    const tamanhoExtra = conteudo.readUInt16LE(posicao + 30)
    const tamanhoComentario = conteudo.readUInt16LE(posicao + 32)
    const inicioLocal = conteudo.readUInt32LE(posicao + 42)
    const nome = conteudo.toString('utf8', posicao + 46, posicao + 46 + tamanhoNome)

    const inicioDados =
      inicioLocal +
      30 +
      conteudo.readUInt16LE(inicioLocal + 26) +
      conteudo.readUInt16LE(inicioLocal + 28)
    const dados = conteudo.subarray(inicioDados, inicioDados + tamanhoComprimido)
    arquivos.set(nome, metodo === 8 ? inflateRawSync(dados) : Buffer.from(dados))

    posicao += 46 + tamanhoNome + tamanhoExtra + tamanhoComentario
  }
  return arquivos
}

function decodificar(texto) {
  return texto
    .replace(/_x000D_/g, '')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&#(\d+);/g, (_, codigo) => String.fromCodePoint(Number(codigo)))
    .replace(/&amp;/g, '&')
}

function textoDoTrecho(trecho) {
  return trecho.match(/<t(?:\s[^>]*)?>([\s\S]*?)<\/t>/)?.[1] ?? ''
}

/**
 * Um texto por `<si>`, juntando os trechos de texto formatado (`<r><t>`). Trecho riscado é texto
 * que o InvoiCy tirou do layout ("nDI" riscado antes de "nroDUIMP") e fica de fora.
 */
export function lerTextosCompartilhados(xml) {
  return [...xml.matchAll(/<si>([\s\S]*?)<\/si>/g)].map(([, si]) => {
    if (!si.includes('<r>')) return decodificar(textoDoTrecho(si))

    const trechos = [...si.matchAll(/<r>([\s\S]*?)<\/r>/g)]
      .map(([, trecho]) => trecho)
      .filter((trecho) => !RISCADO.test(trecho.match(/<rPr>[\s\S]*?<\/rPr>/)?.[0] ?? ''))
    return decodificar(trechos.map(textoDoTrecho).join(''))
  })
}

/** Índices de estilo de célula (o `s` da célula) cuja fonte é riscada. */
export function lerEstilosRiscados(estilosXml) {
  const blocoFontes = estilosXml.match(/<fonts\b[\s\S]*?<\/fonts>/)?.[0] ?? ''
  const fonteRiscada = [...blocoFontes.matchAll(/<font\b[^>]*\/>|<font\b[\s\S]*?<\/font>/g)].map(
    ([fonte]) => RISCADO.test(fonte),
  )
  const blocoEstilos = estilosXml.match(/<cellXfs\b[\s\S]*?<\/cellXfs>/)?.[0] ?? ''
  const fontePorEstilo = [...blocoEstilos.matchAll(/<xf\b[^>]*?(?:\/>|>[\s\S]*?<\/xf>)/g)].map(
    ([estilo]) => Number(estilo.match(/fontId="(\d+)"/)?.[1] ?? 0),
  )
  return new Set(fontePorEstilo.flatMap((fonte, indice) => (fonteRiscada[fonte] ? [indice] : [])))
}

/**
 * Índices de estilo de célula pintados com a cor da reforma tributária. A planilha não tem legenda:
 * o verde marca os campos da reforma e o amarelo os da NT009 v1.01, que também são da reforma.
 */
export function lerEstilosDaReformaTributaria(estilosXml) {
  const blocoPreenchimentos = estilosXml.match(/<fills\b[\s\S]*?<\/fills>/)?.[0] ?? ''
  const ehDaReforma = [
    ...blocoPreenchimentos.matchAll(/<fill\b[^>]*\/>|<fill\b[\s\S]*?<\/fill>/g),
  ].map(([preenchimento]) => {
    const cor = preenchimento.match(/<fgColor\b[^>]*\brgb="([0-9A-Fa-f]+)"/)?.[1]
    return cor !== undefined && CORES_DA_REFORMA_TRIBUTARIA.has(cor.toUpperCase())
  })
  const blocoEstilos = estilosXml.match(/<cellXfs\b[\s\S]*?<\/cellXfs>/)?.[0] ?? ''
  const preenchimentoPorEstilo = [
    ...blocoEstilos.matchAll(/<xf\b[^>]*?(?:\/>|>[\s\S]*?<\/xf>)/g),
  ].map(([estilo]) => Number(estilo.match(/fillId="(\d+)"/)?.[1] ?? 0))
  return new Set(
    preenchimentoPorEstilo.flatMap((preenchimento, indice) =>
      ehDaReforma[preenchimento] ? [indice] : [],
    ),
  )
}

/** Caminho da planilha de cada aba, pelo nome da aba. */
export function lerAbas(workbookXml, relacoesXml) {
  const alvoPorId = new Map(
    [...relacoesXml.matchAll(/<Relationship\b[^>]*>/g)].map(([tag]) => [
      tag.match(/Id="([^"]+)"/)[1],
      tag.match(/Target="([^"]+)"/)[1],
    ]),
  )
  return new Map(
    [...workbookXml.matchAll(/<sheet\b[^>]*>/g)].map(([tag]) => [
      decodificar(tag.match(/name="([^"]+)"/)[1]),
      `xl/${alvoPorId.get(tag.match(/r:id="([^"]+)"/)[1]).replace(/^\/?xl\//, '')}`,
    ]),
  )
}

/**
 * Linhas da aba como `{ numero, celulas: { B: 'C01', ... }, estilos: { B: 3, ... } }`, só com
 * as células preenchidas. Célula com estilo riscado conta como vazia: é assim que a planilha marca
 * tag removida.
 */
export function lerLinhas(planilhaXml, textos, estilosRiscados = new Set()) {
  return [...planilhaXml.matchAll(/<row\b[^>]*\br="(\d+)"[^>]*>([\s\S]*?)<\/row>/g)].map(
    ([, numero, corpo]) => {
      const celulas = {}
      const estilos = {}
      for (const [, atributos, conteudo] of corpo.matchAll(
        /<c\b([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g,
      )) {
        const estilo = Number(atributos.match(/\bs="(\d+)"/)?.[1])
        if (estilosRiscados.has(estilo)) continue

        const coluna = atributos.match(/\br="([A-Z]+)\d+"/)[1]
        const tipo = atributos.match(/\bt="([^"]+)"/)?.[1]
        const valor =
          tipo === 'inlineStr'
            ? conteudo?.match(/<t[^>]*>([\s\S]*?)<\/t>/)?.[1]
            : conteudo?.match(/<v>([\s\S]*?)<\/v>/)?.[1]
        if (valor === undefined) continue
        celulas[coluna] = tipo === 's' ? textos[Number(valor)] : decodificar(valor)
        estilos[coluna] = estilo
      }
      return { numero: Number(numero), celulas, estilos }
    },
  )
}

function textoOuNulo(valor) {
  const texto = valor?.trim()
  return texto ? texto : null
}

/** "-" na coluna de aplicação de negócio é a planilha dizendo que não há regra. */
function aplicacaoOuNulo(valor) {
  const texto = textoOuNulo(valor)
  return texto === null || texto === '-' ? null : texto
}

/**
 * Tags da aba de envio, no formato do JSON versionado. O caminho é montado pela coluna Pai (a
 * sequência do elemento superior), como na extração da versão 8. Linha sem nome de tag (riscada
 * ou de título) fica de fora. A marca da reforma tributária vem da cor da célula do nome da tag.
 */
export function montarTagsDaPlanilha(linhas, estilosDaReforma = new Set()) {
  const tags = []
  const caminhoPorSeq = new Map()

  for (const { celulas, estilos = {} } of linhas) {
    const seq = textoOuNulo(celulas.B)
    const nome = textoOuNulo(celulas.C)
    if (!seq || !/^C\d+$/.test(seq) || !nome) continue

    const pai = textoOuNulo(celulas.H)
    const temPai = pai !== null && pai !== '-'
    const caminhoDoPai = temPai ? caminhoPorSeq.get(pai) : null
    if (temPai && !caminhoDoPai)
      throw new Error(`${seq} (${nome}) aponta para o pai ${pai}, que não está na planilha.`)

    const caminho = caminhoDoPai ? `${caminhoDoPai}/${nome}` : nome
    caminhoPorSeq.set(seq, caminho)
    tags.push({
      seq,
      caminho,
      descricao: textoOuNulo(celulas.D),
      tipo: textoOuNulo(celulas.F),
      ocorrencia: textoOuNulo(celulas.G),
      observacao: textoOuNulo(celulas.I),
      versaoInvoicy: textoOuNulo(celulas.J),
      aplicacaoNegocio: aplicacaoOuNulo(celulas.K),
      reformaTributaria: estilosDaReforma.has(estilos.C),
    })
  }
  return tags
}

/** Histórico de versões do InvoiCy: `{ versao, descricao }` por linha, na ordem da aba. */
export function montarVersoesDaPlanilha(linhas) {
  return linhas
    .map(({ celulas }) => Object.values(celulas).map(textoOuNulo).filter(Boolean))
    .filter((valores) => valores.length >= 2 && /^\d+(\.\d+){2,3}$/.test(valores[0]))
    .map(([versao, ...resto]) => ({ versao, descricao: resto.join(' ') }))
}

/** Planilha inteira, a partir do conteúdo do xlsx. */
export function extrairPlanilha(conteudoXlsx) {
  const arquivos = lerZip(conteudoXlsx)
  const texto = (nome) => {
    const arquivo = arquivos.get(nome)
    if (!arquivo) throw new Error(`${nome} não está no xlsx.`)
    return arquivo.toString('utf8')
  }

  const textos = lerTextosCompartilhados(texto('xl/sharedStrings.xml'))
  const riscados = lerEstilosRiscados(texto('xl/styles.xml'))
  const daReforma = lerEstilosDaReformaTributaria(texto('xl/styles.xml'))
  const abas = lerAbas(texto('xl/workbook.xml'), texto('xl/_rels/workbook.xml.rels'))
  const linhasDaAba = (nome) => {
    if (!abas.has(nome)) throw new Error(`Aba "${nome}" não está na planilha.`)
    return lerLinhas(texto(abas.get(nome)), textos, riscados)
  }

  return {
    tags: montarTagsDaPlanilha(linhasDaAba(ABA_DE_ENVIO), daReforma),
    versoes: montarVersoesDaPlanilha(linhasDaAba(ABA_DE_VERSOES)),
  }
}

async function main() {
  const [arquivo, rotulo] = process.argv.slice(2)
  if (!arquivo || !rotulo) {
    console.error('Uso: extrair-planilha-invoicy.mjs <arquivo.xlsx> <rotulo>')
    process.exit(1)
  }

  const { tags, versoes } = extrairPlanilha(await readFile(arquivo))
  const destino = join(
    dirname(fileURLToPath(import.meta.url)),
    `layout-invoicy-v${rotulo}-planilha.json`,
  )
  const saida = {
    fonte: `${basename(arquivo)}, aba ${ABA_DE_ENVIO}`,
    extraidoEm: new Date().toISOString().slice(0, 10),
    observacao:
      'Caminho montado pela coluna Pai; o XSD é a referência de caminho. Linha e trecho riscados ' +
      'ficam de fora. aplicacaoNegocio vem da coluna K, nulo quando vazia ou "-". ' +
      'reformaTributaria vem da cor verde ou amarela da célula do nome. versoes vem da ' +
      `aba ${ABA_DE_VERSOES}.`,
    versoes,
    tags,
  }
  await writeFile(destino, `${JSON.stringify(saida, null, 2)}\n`)
  console.log(`${tags.length} tags e ${versoes.length} versões gravadas em ${destino}`)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) await main()
