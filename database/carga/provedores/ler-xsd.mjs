/**
 * Comportamento de cada tag do XML de um provedor, lido do XSD dele: ocorrência, tipo, tamanho,
 * valores aceitos e formato. Só funções puras.
 *
 * Os esquemas dos provedores usam tipos nomeados (`tsCnpj`, `TCInfDPS`), `include`, `choice` e
 * atributos; o caminho de uma tag só existe depois de percorrer a árvore a partir do elemento raiz
 * da mensagem, que é o mesmo do mapeamento InvoiCy (`EnviarLoteRpsEnvio`, `DPS`).
 */

import { lerXml, nomeLocal } from './xml.mjs'

const FACETAS_DE_TAMANHO = {
  length: 'comprimento',
  minLength: 'tamanhoMinimo',
  maxLength: 'tamanhoMaximo',
  totalDigits: 'totalDigitos',
  fractionDigits: 'casasDecimais',
  minInclusive: 'valorMinimo',
  maxInclusive: 'valorMaximo',
}

/** Tipos e elementos de topo de todos os arquivos do esquema, indexados pelo nome sem prefixo. */
export function indexarEsquema(textos) {
  const tiposSimples = new Map()
  const tiposComplexos = new Map()
  const elementos = new Map()

  for (const texto of textos) {
    const raiz = lerXml(texto)
    if (raiz.nome !== 'schema') throw new Error(`Esperava <schema>, veio <${raiz.nome}>.`)
    for (const no of raiz.filhos) {
      const nome = no.atributos.name
      if (no.nome === 'simpleType') tiposSimples.set(nome, no)
      else if (no.nome === 'complexType') tiposComplexos.set(nome, no)
      else if (no.nome === 'element') elementos.set(nome, no)
    }
  }
  return { tiposSimples, tiposComplexos, elementos }
}

function documentacaoDe(no) {
  const anotacao = no.filhos.find((filho) => filho.nome === 'annotation')
  const texto = anotacao?.filhos
    .filter((filho) => filho.nome === 'documentation')
    .map((filho) => filho.texto)
    .join(' ')
  return texto?.replace(/\s+/g, ' ').trim() || null
}

/**
 * Facetas de um tipo simples, seguindo a cadeia de restrições até o tipo nativo. A restrição mais
 * próxima da tag vence: é ela que o validador aplica por último.
 */
export function resolverTipoSimples(esquema, no, visitados = new Set()) {
  const restricao = no?.filhos.find((filho) => filho.nome === 'restriction')
  if (!restricao) {
    // União e lista não aparecem nos esquemas destes provedores: tratados como texto.
    return { tipoBase: 'string', valoresAceitos: [], documentacao: no ? documentacaoDe(no) : null }
  }

  const base = nomeLocal(restricao.atributos.base)
  const nomeado = esquema.tiposSimples.get(base)
  const herdado =
    nomeado && !visitados.has(base)
      ? resolverTipoSimples(esquema, nomeado, new Set([...visitados, base]))
      : { tipoBase: base, valoresAceitos: [] }

  const proprio = { valoresAceitos: [] }
  const padroes = []
  for (const faceta of restricao.filhos) {
    const valor = faceta.atributos.value
    if (faceta.nome === 'enumeration') {
      proprio.valoresAceitos.push({ valor, descricao: documentacaoDe(faceta) })
    } else if (faceta.nome === 'pattern') {
      padroes.push(valor)
    } else if (FACETAS_DE_TAMANHO[faceta.nome]) {
      proprio[FACETAS_DE_TAMANHO[faceta.nome]] = valor
    }
  }

  return {
    ...herdado,
    ...proprio,
    padrao: padroes.length ? padroes.join('|') : herdado.padrao,
    valoresAceitos: proprio.valoresAceitos.length ? proprio.valoresAceitos : herdado.valoresAceitos,
    documentacao: documentacaoDe(no) ?? herdado.documentacao ?? null,
  }
}

function ocorrencia(no, padrao = '1') {
  return { minimo: no.atributos.minOccurs ?? padrao, maximo: no.atributos.maxOccurs ?? padrao }
}

/**
 * Percorre a árvore a partir de `raiz` e devolve caminho → definição. O caminho usa o nome das
 * tags separado por `/`, e o atributo entra como mais um passo (`DPS/infDPS/Id`), a mesma forma
 * do mapeamento InvoiCy.
 *
 * `emEscolha` marca a tag que está dentro de um `choice`, direta ou indiretamente: quem decide se
 * ela vai no XML é a alternativa escolhida, não a ocorrência dela.
 */
export function definicoesPorCaminho(esquema, raiz) {
  const elementoRaiz = esquema.elementos.get(raiz)
  if (!elementoRaiz) throw new Error(`O esquema não tem o elemento raiz ${raiz}.`)

  const definicoes = new Map()

  function visitarConteudo(no, caminhoPai, contexto, tiposNoCaminho) {
    for (const filho of no.filhos) {
      if (filho.nome === 'sequence' || filho.nome === 'all') {
        const opcional = (filho.atributos.minOccurs ?? '1') === '0'
        visitarConteudo(
          filho,
          caminhoPai,
          { ...contexto, compositorOpcional: contexto.compositorOpcional || opcional },
          tiposNoCaminho,
        )
      } else if (filho.nome === 'choice') {
        const opcional = (filho.atributos.minOccurs ?? '1') === '0'
        visitarConteudo(
          filho,
          caminhoPai,
          { emEscolha: true, compositorOpcional: contexto.compositorOpcional || opcional },
          tiposNoCaminho,
        )
      } else if (filho.nome === 'element') {
        visitarElemento(filho, caminhoPai, contexto, tiposNoCaminho)
      } else if (filho.nome === 'attribute') {
        visitarAtributo(filho, caminhoPai)
      } else if (filho.nome === 'complexContent' || filho.nome === 'simpleContent') {
        throw new Error(`${caminhoPai}: ${filho.nome} não é suportado por este leitor.`)
      }
    }
  }

  function visitarAtributo(no, caminhoPai) {
    const nome = no.atributos.name
    if (!nome) return
    const tipo = nomeLocal(no.atributos.type)
    const simples = no.filhos.find((filho) => filho.nome === 'simpleType')
    const caminho = `${caminhoPai}/${nome}`
    definicoes.set(caminho, {
      caminho,
      nome,
      ehAtributo: true,
      ehGrupo: false,
      minimo: no.atributos.use === 'required' ? '1' : '0',
      maximo: '1',
      emEscolha: false,
      compositorOpcional: false,
      ...resolverTipoSimples(esquema, simples ?? esquema.tiposSimples.get(tipo)),
      ...(esquema.tiposSimples.has(tipo) || simples ? {} : { tipoBase: tipo ?? 'string' }),
      nomeTipo: simples ? null : (tipo ?? null),
      documentacao: documentacaoDe(no) ?? null,
    })
  }

  function visitarElemento(noOriginal, caminhoPai, contexto, tiposNoCaminho) {
    const referencia = nomeLocal(noOriginal.atributos.ref)
    const no = referencia ? esquema.elementos.get(referencia) : noOriginal
    // Referência a outro esquema (a assinatura `ds:Signature`) não é dado de nota.
    if (!no) return

    const nome = no.atributos.name
    const caminho = caminhoPai ? `${caminhoPai}/${nome}` : nome
    const tipo = nomeLocal(no.atributos.type)
    const complexoEmLinha = no.filhos.find((filho) => filho.nome === 'complexType')
    const simplesEmLinha = no.filhos.find((filho) => filho.nome === 'simpleType')
    const complexo = complexoEmLinha ?? esquema.tiposComplexos.get(tipo)

    const base = {
      caminho,
      nome,
      ehAtributo: false,
      ehGrupo: Boolean(complexo),
      ...ocorrencia(noOriginal),
      emEscolha: contexto.emEscolha,
      compositorOpcional: contexto.compositorOpcional,
    }

    if (complexo) {
      definicoes.set(caminho, {
        ...base,
        tipoBase: null,
        valoresAceitos: [],
        documentacao: documentacaoDe(no) ?? documentacaoDe(complexo),
      })
      // Tipo recursivo (raro, mas existe em esquemas de evento) não é percorrido duas vezes.
      if (tipo && tiposNoCaminho.has(tipo)) return
      visitarConteudo(
        complexo,
        caminho,
        { emEscolha: false, compositorOpcional: false },
        tipo ? new Set([...tiposNoCaminho, tipo]) : tiposNoCaminho,
      )
      return
    }

    const simples = simplesEmLinha ?? esquema.tiposSimples.get(tipo)
    const resolvido = simples
      ? resolverTipoSimples(esquema, simples)
      : { tipoBase: tipo ?? 'string', valoresAceitos: [], documentacao: null }
    definicoes.set(caminho, {
      ...base,
      ...resolvido,
      nomeTipo: simplesEmLinha ? null : (tipo ?? null),
      documentacao: documentacaoDe(no) ?? resolvido.documentacao,
    })
  }

  visitarElemento(elementoRaiz, '', { emEscolha: false, compositorOpcional: false }, new Set())
  return definicoes
}

/**
 * Põe as definições na forma de caminho do mapeamento. O ISS.NET embrulha a DPS nacional num
 * envelope de lote: a DPS do esquema nacional vira `EnviarLoteDpsSincronoEnvio/…/ListaDps/DPS`.
 */
export function comPrefixo(definicoes, prefixo) {
  if (!prefixo) return definicoes
  return new Map([...definicoes].map(([caminho, definicao]) => [`${prefixo}${caminho}`, definicao]))
}
