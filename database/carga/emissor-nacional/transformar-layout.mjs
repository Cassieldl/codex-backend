/**
 * Vocabulário canônico do Layout InvoiCy a partir do XSD de entrada (layout-invoicy-v8.xsd),
 * descrito pela planilha do InvoiCy (layout-invoicy-v8-planilha.json). Só funções puras.
 *
 * O XSD é a referência de caminho — é ele que o InvoiCy valida. A planilha entra só como texto:
 * ela repete sequências e tem o grupo Imovel desatualizado, então nunca decide caminho.
 *
 * Caminho canônico: `//` + o caminho a partir do RPS (`//Tomador/TomaCNPJ`), ou a partir do Envio
 * para as tags de fora do RPS (`//ModeloDocumento`). É a forma que o parceiro reconhece no XML que
 * monta, e o `//` é a convenção que a tela já trata como sintaxe.
 */

// Os do validator do backend (`LimitesCampoLayout`): o caminho tem o teto do slug que nasce dele.
export const LIMITES_CAMPO_LAYOUT = { nome: 120, caminhoXml: 160, significado: 1000, grupo: 120 }

const TOKEN = /<(\/?)xs:(\w+)([^>]*?)(\/?)>|([^<]+)/g
const ATRIBUTO = /(\w+)="([^"]*)"/g

function decodificar(texto) {
  return texto
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&')
}

function atributos(texto) {
  return Object.fromEntries([...texto.matchAll(ATRIBUTO)].map(([, nome, valor]) => [nome, valor]))
}

/**
 * Elementos do XSD em ordem de documento, com o caminho completo montado pelo aninhamento. O XSD
 * do InvoiCy não usa tipos nomeados nem `ref`, então a árvore inteira está no aninhamento. Tipo,
 * tamanho e ocorrência ficam de fora de propósito: o campo canônico é vocabulário, não
 * comportamento (R3) — comportamento é da versão do provedor.
 */
export function lerXsd(texto) {
  const elementos = []
  const pilha = []
  let documentacao = null

  for (const [, fechamento, tag, brutos, autoFechado, conteudo] of texto.matchAll(TOKEN)) {
    if (conteudo !== undefined) {
      if (documentacao !== null) documentacao += conteudo
      continue
    }

    if (tag === 'element' && !fechamento) {
      const elemento = {
        nome: atributos(brutos).name,
        caminho: [...pilha.map((pai) => pai.nome), atributos(brutos).name].join('/'),
        documentacao: null,
      }
      elementos.push(elemento)
      if (!autoFechado) pilha.push(elemento)
    } else if (tag === 'element' && fechamento) {
      pilha.pop()
    } else if (tag === 'documentation') {
      const atual = pilha.at(-1)
      if (fechamento && atual) {
        atual.documentacao = decodificar(documentacao).trim() || null
      }
      documentacao = fechamento ? null : ''
    }
  }

  const pais = new Set(
    elementos.map((elemento) => elemento.caminho.slice(0, elemento.caminho.lastIndexOf('/'))),
  )
  return elementos.map((elemento) => ({ ...elemento, ehGrupo: pais.has(elemento.caminho) }))
}

export function caminhoCanonico(caminhoCompleto) {
  const relativo = caminhoCompleto.replace(/^Envio\/RPS\//, '').replace(/^Envio\//, '')
  return `//${relativo}`
}

/**
 * Grupo do campo = caminho do grupo pai no XSD, a partir do RPS (`Servico/Valores/IBSCBS`). Cada
 * grupo da documentação vira um cabeçalho próprio, na ordem do documento — com só o primeiro
 * nível, "Servico" juntava 187 campos e misturava os dois `IBSCBS`, que são grupos diferentes.
 *
 * Um grupo usado como campo (a lista `ListaDed/Ded`) fica no próprio grupo, à frente dos filhos;
 * no grupo do pai, ele apareceria longe deles.
 */
export function grupoDe(caminhoCompleto, ehGrupo = false) {
  if (!caminhoCompleto.startsWith('Envio/RPS/')) return 'Envio'
  const relativo = caminhoCompleto.slice('Envio/RPS/'.length)
  if (ehGrupo) return relativo
  const fimDoPai = relativo.lastIndexOf('/')
  return fimDoPai < 0 ? 'RPS' : relativo.slice(0, fimDoPai)
}

/** A mesma normalização que o slug faz: o que colidir aqui colide no índice único do banco. */
export function chaveDeSlug(caminho) {
  return caminho
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
}

function exigirLimite(valor, limite, descricao) {
  if (valor && valor.length > limite) {
    throw new Error(`${descricao} tem ${valor.length} caracteres e o limite é ${limite}.`)
  }
  return valor
}

function normalizar(texto) {
  return texto?.replace(/\s+/g, ' ').trim() || null
}

const INICIO_DE_LISTA = /^\s*(Valores aceitos|\d+\s*[-–=])/im

const REMISSAO_AOS_VALORES = 'Os valores aceitos estão no domínio da versão de cada provedor.'

/**
 * Descrição mais observação da planilha. Quando a observação traz a lista de valores aceitos e
 * estoura o limite, o significado fica com o texto anterior à lista: os valores são comportamento
 * e vivem no domínio da versão de cada provedor, não no vocabulário.
 */
export function montarSignificado(descricao, observacao) {
  const frase = descricao.replace(/\.?$/, '.')
  const semPonto = (texto) => texto.replace(/\.$/, '').toLowerCase()
  const observacaoUtil =
    normalizar(observacao) && !semPonto(frase).includes(semPonto(normalizar(observacao)))
      ? normalizar(observacao)
      : null

  const completo = observacaoUtil ? `${frase} ${observacaoUtil}` : descricao
  if (completo.length <= LIMITES_CAMPO_LAYOUT.significado) return completo

  const inicioDaLista = observacao.search(INICIO_DE_LISTA)
  if (inicioDaLista < 0) return completo

  // A frase que apresenta a lista ("Os valores aceitos são:") sai junto com ela.
  const antesDaLista = normalizar(observacao.slice(0, inicioDaLista).replace(/[^.]*:\s*$/, ''))
  return antesDaLista
    ? `${frase} ${antesDaLista} ${REMISSAO_AOS_VALORES}`
    : `${frase} ${REMISSAO_AOS_VALORES}`
}

/**
 * Um campo canônico por folha do XSD, mais os grupos que algum provedor mapeia como campo (as
 * listas, como `ListaDed/Ded`). Grupo puramente estrutural não é vocabulário. `caminhosExcluidos`
 * tira regiões inteiras cujo caminho ainda não é confiável.
 */
export function montarVocabulario(
  elementos,
  planilha,
  gruposUsados = new Set(),
  caminhosExcluidos = [],
) {
  const descricaoPorCaminho = new Map(planilha.tags.map((tag) => [tag.caminho, tag]))
  const excluido = (caminho) =>
    caminhosExcluidos.some((prefixo) => caminho === prefixo || caminho.startsWith(`${prefixo}/`))
  const campos = elementos
    .filter((elemento) => elemento.caminho !== 'Envio' && elemento.caminho !== 'Envio/RPS')
    .filter((elemento) => !excluido(elemento.caminho))
    .filter((elemento) => !elemento.ehGrupo || gruposUsados.has(elemento.caminho))
    .map((elemento, indice) => {
      const daPlanilha = descricaoPorCaminho.get(elemento.caminho)
      const descricao =
        normalizar(daPlanilha?.descricao) ?? normalizar(elemento.documentacao) ?? elemento.nome
      const caminhoXml = caminhoCanonico(elemento.caminho)
      return {
        caminhoCompleto: elemento.caminho,
        nome: exigirLimite(elemento.nome, LIMITES_CAMPO_LAYOUT.nome, `Nome de ${caminhoXml}`),
        caminhoXml: exigirLimite(
          caminhoXml,
          LIMITES_CAMPO_LAYOUT.caminhoXml,
          `Caminho de ${caminhoXml}`,
        ),
        significado: exigirLimite(
          montarSignificado(descricao, daPlanilha?.observacao ?? null),
          LIMITES_CAMPO_LAYOUT.significado,
          `Significado de ${caminhoXml}`,
        ),
        grupo: exigirLimite(
          grupoDe(elemento.caminho, elemento.ehGrupo),
          LIMITES_CAMPO_LAYOUT.grupo,
          `Grupo de ${caminhoXml}`,
        ),
        ordem: indice + 1,
      }
    })

  const vistos = new Map()
  for (const campo of campos) {
    const chave = chaveDeSlug(campo.caminhoXml)
    if (vistos.has(chave)) {
      throw new Error(`${campo.caminhoXml} e ${vistos.get(chave)} geram o mesmo slug.`)
    }
    vistos.set(chave, campo.caminhoXml)
  }
  return campos
}
