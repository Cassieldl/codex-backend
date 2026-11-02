/**
 * Estrutura de uma versão do Layout InvoiCy: a árvore do documento em XML (SOAP) e em JSON (REST),
 * gravada na versão e servida pelo portal. Só funções puras.
 *
 * A árvore XML é o XSD inteiro, raiz Envio. A JSON deriva dela pela convenção da planilha: raiz
 * Documento, RPS como lista e, onde a planilha manda "não informar este grupo quando a integração
 * for API Rest", o grupo de item some e a lista recebe o array direto.
 */

const TOKEN = /<(\/?)xs:(\w+)([^>]*?)(\/?)>/g
const ATRIBUTO = /(\w+)="([^"]*)"/g

export const RAIZ_JSON = 'Documento'
export const LIMITES_ESTRUTURA = { profundidade: 32, nos: 5000 }

/** Grupo de item que a planilha manda omitir no REST. */
const OMITIR_NO_REST = /não informar este grupo quando a integração for api rest/i

function atributos(texto) {
  return Object.fromEntries([...texto.matchAll(ATRIBUTO)].map(([, nome, valor]) => [nome, valor]))
}

function ocorrenciaMaxima(valor) {
  if (valor === undefined) return 1
  return valor === 'unbounded' ? Infinity : Number(valor)
}

/**
 * Árvore de elementos do XSD com o que o valor-modelo precisa: tipo base, facetas e ocorrência.
 * `escolha` marca o elemento que é uma das alternativas de um `xs:choice`: entre irmãos
 * consecutivos marcados, só um entra no documento.
 */
export function lerArvoreXsd(texto) {
  const raizes = []
  const pilha = []

  for (const [, fechamento, tag, brutos, autoFechado] of texto.matchAll(TOKEN)) {
    if (fechamento) {
      pilha.pop()
      continue
    }

    const attrs = atributos(brutos)
    const elementoAtual = pilha.findLast((item) => item.tag === 'element')?.no

    if (tag === 'element') {
      const no = {
        nome: attrs.name,
        caminho: elementoAtual ? `${elementoAtual.caminho}/${attrs.name}` : attrs.name,
        maximo: ocorrenciaMaxima(attrs.maxOccurs),
        escolha: pilha.at(-1)?.tag === 'choice',
        tipo: attrs.type?.replace(/^xs:/, '') ?? null,
        facetas: {},
        filhos: [],
      }
      ;(elementoAtual ? elementoAtual.filhos : raizes).push(no)
      if (!autoFechado) pilha.push({ tag, no })
      continue
    }

    if (tag === 'restriction' && elementoAtual) elementoAtual.tipo = attrs.base.replace(/^xs:/, '')
    if (['maxLength', 'totalDigits', 'fractionDigits'].includes(tag) && elementoAtual)
      elementoAtual.facetas[tag] = Number(attrs.value)
    if (tag === 'enumeration' && elementoAtual)
      (elementoAtual.facetas.enumeracao ??= []).push(attrs.value)

    if (!autoFechado) pilha.push({ tag })
  }

  if (raizes.length !== 1) throw new Error(`O XSD precisa de uma raiz só; tem ${raizes.length}.`)
  return raizes[0]
}

const TIPOS_INTEIROS = new Set([
  'integer',
  'nonNegativeInteger',
  'positiveInteger',
  'int',
  'long',
  'short',
  'byte',
  'unsignedInt',
  'unsignedLong',
  'unsignedShort',
  'unsignedByte',
])

/**
 * Valor-modelo da folha pelo tipo do XSD. Precisa ser legível e também válido contra o XSD, porque
 * o documento montado com eles é validado em teste:
 * - texto com tamanho n: `texto(n)`; quando n não comporta essa forma, n letras X (`XX` para UF);
 * - decimal com f casas: `0.` mais f zeros; sem casas, `0`; inteiro: `0`;
 * - data e data-hora: uma data fixa no formato do XSD (`2026-12-31`, `2026-12-31T23:59:59`);
 * - enumeração: o primeiro valor aceito.
 */
export function valorModelo(no) {
  const { tipo, facetas } = no
  if (facetas.enumeracao?.length) return facetas.enumeracao[0]
  if (tipo === 'date') return '2026-12-31'
  if (tipo === 'dateTime') return '2026-12-31T23:59:59'
  if (tipo === 'decimal')
    return facetas.fractionDigits > 0 ? `0.${'0'.repeat(facetas.fractionDigits)}` : '0'
  if (TIPOS_INTEIROS.has(tipo)) return '0'
  if (tipo === 'string') {
    const tamanho = facetas.maxLength
    if (tamanho === undefined) return 'texto'
    const descrito = `texto(${tamanho})`
    return descrito.length <= tamanho ? descrito : 'X'.repeat(tamanho)
  }
  return null
}

/** Caminho canônico do campo (`//Tomador/TomaCNPJ`), o mesmo do vocabulário. */
function caminhoCampoDe(caminhoXml, caminhosDoVocabulario) {
  const relativo = caminhoXml.replace(/^Envio\/RPS\//, '').replace(/^Envio\//, '')
  const canonico = `//${relativo}`
  return caminhosDoVocabulario.has(canonico) ? canonico : null
}

function gruposOmitidosNoRest(planilha) {
  return new Set(
    planilha.tags
      .filter((tag) => OMITIR_NO_REST.test(tag.observacao ?? ''))
      .map((tag) => tag.caminho),
  )
}

function montarNoXml(no, contexto) {
  const ehGrupo = no.filhos.length > 0
  return {
    nome: no.nome,
    caminho: no.caminho,
    ehGrupo,
    repete: no.maximo > 1,
    escolha: no.escolha,
    valorModelo: ehGrupo ? null : valorModelo(no),
    formato: contexto.omitidos.has(no.caminho) ? 'soXml' : 'ambos',
    caminhoCampo: caminhoCampoDe(no.caminho, contexto.caminhosDoVocabulario),
    filhos: no.filhos.map((filho) => montarNoXml(filho, contexto)),
  }
}

/**
 * Nó JSON a partir do nó XML. O grupo omitido no REST sai e os filhos sobem para a lista, que
 * passa a repetir; a lista herda o campo do grupo de item quando não é campo ela mesma.
 */
function montarNoJson(noXml, caminhoDoPai) {
  const nome = caminhoDoPai === null && noXml.nome === 'Envio' ? RAIZ_JSON : noXml.nome
  const caminho = caminhoDoPai === null ? nome : `${caminhoDoPai}/${nome}`
  const omitido = noXml.filhos.find((filho) => filho.formato === 'soXml')

  if (omitido && noXml.filhos.length > 1)
    throw new Error(`${omitido.caminho} é omitido no REST, mas ${noXml.caminho} tem outros filhos.`)

  const filhosEfetivos = omitido ? omitido.filhos : noXml.filhos
  return {
    nome,
    caminho,
    ehGrupo: noXml.ehGrupo,
    repete: noXml.repete || Boolean(omitido) || noXml.caminho === 'Envio/RPS',
    escolha: noXml.escolha,
    valorModelo: noXml.valorModelo,
    formato: 'ambos',
    caminhoCampo: noXml.caminhoCampo ?? omitido?.caminhoCampo ?? null,
    filhos: filhosEfetivos.map((filho) => montarNoJson(filho, caminho)),
  }
}

function caminhoDoPaiNaPlanilha(caminho) {
  return caminho.slice(0, caminho.lastIndexOf('/'))
}

/**
 * Tag da planilha que o XSD não tem: entra só no JSON (`soJson`), sob o pai equivalente e logo
 * depois do irmão que a precede na planilha. As de `caminhosComDefeito` ficam de fora.
 */
function acrescentarSoJson(arvores, planilha, caminhosDoXsd, omitidos, caminhosComDefeito) {
  const noJsonPorCaminhoPlanilha = new Map()
  const indexar = (noJson, caminhoXml) => noJsonPorCaminhoPlanilha.set(caminhoXml, noJson)

  const percorrer = (noXml, noJson) => {
    indexar(noJson, noXml.caminho)
    const filhosXml =
      noXml.filhos.length === 1 && omitidos.has(noXml.filhos[0].caminho)
        ? noXml.filhos[0].filhos
        : noXml.filhos
    if (filhosXml !== noXml.filhos) indexar(noJson, noXml.filhos[0].caminho)
    filhosXml.forEach((filho, indice) => percorrer(filho, noJson.filhos[indice]))
  }
  percorrer(arvores.xml, arvores.json)

  let anterior = null
  for (const tag of planilha.tags) {
    if (caminhosComDefeito.has(tag.caminho)) continue
    if (caminhosDoXsd.has(tag.caminho)) {
      anterior = tag.caminho
      continue
    }
    const pai = noJsonPorCaminhoPlanilha.get(caminhoDoPaiNaPlanilha(tag.caminho))
    if (!pai) throw new Error(`${tag.caminho} está só na planilha e o pai dela também.`)

    const nome = tag.caminho.slice(tag.caminho.lastIndexOf('/') + 1)
    const no = {
      nome,
      caminho: `${pai.caminho}/${nome}`,
      ehGrupo: false,
      repete: false,
      escolha: false,
      valorModelo: null,
      formato: 'soJson',
      caminhoCampo: null,
      filhos: [],
    }
    const irmao = anterior ? noJsonPorCaminhoPlanilha.get(anterior) : null
    const posicao = pai.filhos.indexOf(irmao)
    pai.filhos.splice(posicao + 1, 0, no)
    pai.ehGrupo = true
    noJsonPorCaminhoPlanilha.set(tag.caminho, no)
    anterior = tag.caminho
  }
}

function contarELimitar(no, profundidade = 1) {
  if (profundidade > LIMITES_ESTRUTURA.profundidade)
    throw new Error(`${no.caminho} passa da profundidade ${LIMITES_ESTRUTURA.profundidade}.`)
  return 1 + no.filhos.reduce((total, filho) => total + contarELimitar(filho, profundidade + 1), 0)
}

/**
 * Estrutura completa da versão. `caminhosDoVocabulario` são os caminhos canônicos dos campos da
 * versão: é por eles que o portal liga o nó ao campo publicado. `caminhosComDefeito` são tags da
 * planilha com caminho errado, que não viram nó.
 */
export function montarEstrutura(
  xsdTexto,
  planilha,
  caminhosDoVocabulario,
  caminhosComDefeito = new Set(),
) {
  const arvore = lerArvoreXsd(xsdTexto)
  const omitidos = gruposOmitidosNoRest(planilha)
  const xml = montarNoXml(arvore, { omitidos, caminhosDoVocabulario })
  const json = montarNoJson(xml, null)

  const caminhosDoXsd = new Set()
  const coletar = (no) => {
    caminhosDoXsd.add(no.caminho)
    no.filhos.forEach(coletar)
  }
  coletar(xml)
  acrescentarSoJson({ xml, json }, planilha, caminhosDoXsd, omitidos, caminhosComDefeito)

  for (const raiz of [xml, json]) {
    const total = contarELimitar(raiz)
    if (total > LIMITES_ESTRUTURA.nos)
      throw new Error(
        `A árvore ${raiz.nome} tem ${total} nós; o limite é ${LIMITES_ESTRUTURA.nos}.`,
      )
  }
  return { xml, json }
}
