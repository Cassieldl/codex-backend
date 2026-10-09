/**
 * Campos da versão de um provedor: um por campo do XML de entrada do InvoiCy, com o comportamento
 * que o XML do provedor exige. Só funções puras.
 *
 * O comportamento (tipo, tamanho, valores, obrigatoriedade) vem do leiaute do provedor, porque é
 * ele quem valida. Quando o leiaute não descreve a tag, a ocorrência vem da configuração do
 * InvoiCy (coluna do mapeamento) e a nota diz isso.
 */

import { LIMITES_CAMPO } from '../emissor-nacional/transformar-campos.mjs'

const NUMERICOS = new Set([
  'decimal',
  'integer',
  'int',
  'long',
  'short',
  'byte',
  'nonNegativeInteger',
  'positiveInteger',
  'unsignedByte',
  'unsignedShort',
  'unsignedInt',
  'unsignedLong',
  'double',
  'float',
])

/** Padrão de XSD acima disto (o de data e hora tem ~300) ajuda mais o validador que o leitor. */
const LIMITE_PADRAO_LEGIVEL = 60

/** Descrição dos valores que ainda cabe na nota de preenchimento, ao lado das outras notas. */
const LIMITE_DOMINIO_NA_NOTA = 700

const PADRAO_SO_DE_VALORES = /^[\w.-]+(\|[\w.-]+)+$/
const PADRAO_DE_DIGITOS = /^\[0-9\]\{(\d+)(?:,(\d+))?\}$/

function normalizar(texto) {
  return texto?.replace(/\s+/g, ' ').trim() || null
}

function exigirLimite(valor, limite, descricao) {
  if (valor && valor.length > limite) {
    throw new Error(`${descricao} tem ${valor.length} caracteres e o limite é ${limite}.`)
  }
  return valor ?? null
}

function ehDataHora(definicao) {
  return definicao?.tipoBase === 'dateTime' || /datetime/i.test(definicao?.nomeTipo ?? '')
}

function ehData(definicao) {
  return definicao?.tipoBase === 'date' || /^t[sc]?data$/i.test(definicao?.nomeTipo ?? '')
}

/** Tipo é obrigatório na versão: sem leiaute que descreva a tag, a tela diz isso em vez de supor. */
export const TIPO_NAO_DESCRITO = 'Não descrito'

export function descreverTipo(definicao, ehGrupo) {
  if (ehGrupo || definicao?.ehGrupo) return 'Grupo (lista)'
  if (!definicao?.tipoBase) return TIPO_NAO_DESCRITO
  if (ehDataHora(definicao)) return 'Data e hora'
  if (ehData(definicao)) return 'Data'
  if (NUMERICOS.has(definicao.tipoBase)) return 'Numérico'
  return 'Texto'
}

/** "1 a 60", "até 15 dígitos, com 2 decimais", "14"; do padrão `[0-9]{8}` sai "8 dígitos". */
export function descreverTamanho(definicao) {
  if (!definicao) return null
  const { comprimento, tamanhoMinimo, tamanhoMaximo, totalDigitos, casasDecimais, padrao } =
    definicao

  if (comprimento) return comprimento
  if (tamanhoMaximo) {
    if (!tamanhoMinimo || tamanhoMinimo === tamanhoMaximo)
      return tamanhoMinimo ? tamanhoMaximo : `até ${tamanhoMaximo}`
    return `${tamanhoMinimo} a ${tamanhoMaximo}`
  }
  if (totalDigitos) {
    return casasDecimais && casasDecimais !== '0'
      ? `até ${totalDigitos} dígitos, com ${casasDecimais} decimais`
      : `até ${totalDigitos} dígitos`
  }
  const digitos = padrao?.match(PADRAO_DE_DIGITOS)
  if (digitos) return digitos[2] ? `${digitos[1]} a ${digitos[2]} dígitos` : `${digitos[1]} dígitos`
  return null
}

export function descreverMascara(definicao) {
  if (ehDataHora(definicao))
    return /utc/i.test(definicao.nomeTipo ?? '')
      ? 'AAAA-MM-DDThh:mm:ssTZD (UTC)'
      : 'AAAA-MM-DDThh:mm:ss'
  if (ehData(definicao)) return 'AAAA-MM-DD'
  return null
}

/**
 * Valores aceitos, da enumeração do XSD ou de um padrão que só lista valores (`1|2`, `M|S`, comum
 * no ABRASF). Domínio longo demais para a coluna fica só com os códigos, e a descrição vai para a
 * nota quando couber — nunca corta texto no meio.
 */
export function descreverDominio(definicao) {
  const valores = definicao?.valoresAceitos?.length
    ? definicao.valoresAceitos
    : PADRAO_SO_DE_VALORES.test(definicao?.padrao ?? '')
      ? definicao.padrao.split('|').map((valor) => ({ valor, descricao: null }))
      : []
  if (!valores.length) return { dominio: null, notaExtra: null }

  const comDescricao = valores.some((item) => item.descricao)
  const completo = valores
    .map((item) =>
      comDescricao && item.descricao ? `${item.valor} = ${normalizar(item.descricao)}` : item.valor,
    )
    .join(comDescricao ? '; ' : ', ')
  if (completo.length <= LIMITES_CAMPO.dominio) return { dominio: completo, notaExtra: null }

  const codigos = valores.map((item) => item.valor).join(', ')
  if (codigos.length > LIMITES_CAMPO.dominio) {
    // Tabela inteira no XSD (códigos de país, de município): não cabe nem como lista de códigos.
    return {
      dominio: null,
      notaExtra: `O XSD do provedor aceita ${valores.length} valores fixos; ver a tabela do campo no leiaute do provedor.`,
    }
  }
  return {
    dominio: codigos,
    notaExtra:
      completo.length <= LIMITE_DOMINIO_NA_NOTA
        ? `Significado de cada valor: ${completo}`
        : 'Significado de cada valor: ver a tabela do campo no leiaute do provedor.',
  }
}

function ehObrigatoria(tag, definicao) {
  const minimo = definicao?.minimo ?? tag.ocorrencia?.split('-')[0]
  return Number(minimo) >= 1
}

function caminhoDoPai(caminho) {
  return caminho.slice(0, caminho.lastIndexOf('/'))
}

/** Nome das tags de escolha (choice) de cada grupo, pelo caminho do grupo. */
function indexarEscolhas(definicoes) {
  const escolhasPorPai = new Map()
  for (const [caminho, definicao] of definicoes) {
    if (!definicao.emEscolha) continue
    const lista = escolhasPorPai.get(caminhoDoPai(caminho)) ?? []
    lista.push(definicao.nome)
    escolhasPorPai.set(caminhoDoPai(caminho), lista)
  }
  return escolhasPorPai
}

/**
 * Alternativas do choice em que a tag está, vindas do XSD — o mapeamento pode ter configurado só
 * uma delas. Choice de uma alternativa só (uma lista repetível) não é escolha: devolve `null` e
 * vale a ocorrência da tag.
 */
function alternativasDaEscolha(caminho, definicao, escolhasPorPai) {
  if (!definicao?.emEscolha) return null
  const irmas = escolhasPorPai.get(caminhoDoPai(caminho)) ?? []
  return irmas.length > 1 ? irmas : null
}

/**
 * Obrigatoriedade efetiva de uma tag no XML do provedor: a ocorrência dela só vale quando todos
 * os grupos acima também são obrigatórios. Tag de escolha (choice) é sempre condicional.
 */
export function avaliarPreenchimento(tag, contexto) {
  const { definicoes, tagsPorSequencial, escolhasPorPai } = contexto
  const definicao = definicoes.get(tag.caminho)

  const irmas = alternativasDaEscolha(tag.caminho, definicao, escolhasPorPai)
  if (irmas)
    return { estado: 'Condicional', condicao: `Informar apenas um entre: ${irmas.join(', ')}.` }

  if (!ehObrigatoria(tag, definicao)) return { estado: 'Opcional', condicao: null }

  const gruposOpcionais = []
  let pai = tagsPorSequencial.get(tag.sequencialPai)
  while (pai) {
    const definicaoPai = definicoes.get(pai.caminho)
    // Acima do trecho que o leiaute descreve fica o envelope do lote, que o InvoiCy monta sempre:
    // a ocorrência dele na configuração não torna a tag condicional.
    if (definicao && !definicaoPai) break
    if (!ehObrigatoria(pai, definicaoPai)) gruposOpcionais.push(pai.nomeTag)
    else if (alternativasDaEscolha(pai.caminho, definicaoPai, escolhasPorPai))
      gruposOpcionais.push(`${pai.nomeTag} (escolha)`)
    pai = tagsPorSequencial.get(pai.sequencialPai)
  }

  if (!gruposOpcionais.length) return { estado: 'Obrigatorio', condicao: null }
  return {
    estado: 'Condicional',
    condicao: `Obrigatório quando o grupo ${[...new Set(gruposOpcionais)].reverse().join(' > ')} for informado.`,
  }
}

function montarNotaPreenchimento(documentacao, notas, descricao) {
  const comDocumentacao = [documentacao ? `No XML do provedor: ${documentacao}` : null, ...notas]
    .filter(Boolean)
    .join('\n\n')
  if (comDocumentacao.length <= LIMITES_CAMPO.notaPreenchimento) return comDocumentacao || null
  // As outras notas são instrução de preenchimento: têm prioridade sobre a descrição da tag.
  return exigirLimite(
    notas.join('\n\n') || null,
    LIMITES_CAMPO.notaPreenchimento,
    `Nota de preenchimento de ${descricao}`,
  )
}

/** Destinos que cabem na coluna; quando não cabem todos, a lista completa vai para a nota. */
function acomodarDestinos(caminhos) {
  const completo = caminhos.join(' | ')
  if (completo.length <= LIMITES_CAMPO.destinoXml) return { destinoXml: completo, cortado: false }
  const cabem = []
  for (const caminho of caminhos) {
    if ([...cabem, caminho, '…'].join(' | ').length > LIMITES_CAMPO.destinoXml) break
    cabem.push(caminho)
  }
  return { destinoXml: [...cabem, '…'].join(' | '), cortado: true }
}

/**
 * Um registro por campo do XML de entrada do InvoiCy. Quando o mesmo campo alimenta mais de uma
 * tag do provedor (o CNPJ do prestador vai no cabeçalho do lote e na declaração), a linha é uma só,
 * com todos os destinos — a versão não admite o mesmo campo duas vezes.
 *
 * `definicoes`: caminho → definição do leiaute do provedor, com os caminhos na forma do mapeamento
 * e TODAS as tags do leiaute, não só as mapeadas (as alternativas de um choice vêm daqui).
 * `prefixoDestino`: começo comum dos caminhos (o envelope do lote) tirado do destino exibido.
 */
export function montarCampos(tags, definicoes, { prefixoDestino = '' } = {}) {
  const tagsPorSequencial = new Map(tags.map((tag) => [tag.sequencial, tag]))
  const contexto = { definicoes, tagsPorSequencial, escolhasPorPai: indexarEscolhas(definicoes) }

  const notasDeComposicao = new Map()
  for (const tag of tags.filter((item) => item.compoeDe)) {
    for (const nome of tag.compoeDe) {
      notasDeComposicao.set(
        nome,
        `Também compõe a tag ${tag.nomeTag} do XML do provedor, montada pelo InvoiCy a partir de ${tag.compoeDe.join(' e ')}.`,
      )
    }
  }

  const porCampo = new Map()
  for (const tag of tags) {
    for (const campo of tag.campos ?? []) {
      const destinos = porCampo.get(campo.caminhoInvoicy) ?? []
      destinos.push({ tag, campo })
      porCampo.set(campo.caminhoInvoicy, destinos)
    }
  }

  const exibir = (caminho) =>
    prefixoDestino && caminho.startsWith(prefixoDestino)
      ? caminho.slice(prefixoDestino.length)
      : caminho

  return [...porCampo.values()].map((destinos) => {
    const [{ tag: principal, campo }] = destinos
    const descricao = `Campo ${campo.nomeCampo}`
    const definicao = definicoes.get(principal.caminho)

    const preenchimentos = destinos.map(({ tag }) => avaliarPreenchimento(tag, contexto))
    let estado =
      new Set(preenchimentos.map((item) => item.estado)).size === 1
        ? preenchimentos[0].estado
        : 'Condicional'
    let condicao = [...new Set(preenchimentos.map((item) => item.condicao).filter(Boolean))].join(
      ' ',
    )

    const alternativas = destinos
      .filter(({ campo: item }) => item.alternativa)
      .map(({ tag }) => tag.campos.map((item) => item.nomeCampo))
    if (alternativas.length) {
      estado = 'Condicional'
      condicao = [`Informar ${alternativas[0].join(' ou ')}, conforme o caso.`, condicao]
        .filter(Boolean)
        .join(' ')
    }

    const { dominio, notaExtra } = descreverDominio(definicao)
    const padrao = definicao?.padrao
    const notas = [
      padrao && !PADRAO_SO_DE_VALORES.test(padrao) && padrao.length <= LIMITE_PADRAO_LEGIVEL
        ? `Formato aceito pelo XSD do provedor: ${padrao}`
        : null,
      notaExtra,
      definicao
        ? null
        : 'O leiaute do provedor não descreve esta tag; a ocorrência vem da configuração do InvoiCy.',
    ].filter(Boolean)

    const caminhos = destinos.map(({ tag }) => exibir(tag.caminho))
    const { destinoXml, cortado } = acomodarDestinos(caminhos)
    const notasDeTransformacao = [
      destinos.length > 1
        ? `O InvoiCy grava este campo em ${destinos.length} tags do XML do provedor${cortado ? `: ${caminhos.join(', ')}` : ''}.`
        : null,
      alternativas.length
        ? `A tag ${destinos.find(({ campo: item }) => item.alternativa).tag.nomeTag} recebe ${alternativas[0].join(' ou ')}.`
        : null,
      notasDeComposicao.get(campo.nomeCampo) ?? null,
    ].filter(Boolean)

    return {
      nomeCampo: campo.nomeCampo,
      caminhoInvoicy: campo.caminhoInvoicy,
      campoVersao: {
        tipo: descreverTipo(definicao, principal.elemento === 'G'),
        tamanho: exigirLimite(
          descreverTamanho(definicao),
          LIMITES_CAMPO.tamanho,
          `Tamanho de ${descricao}`,
        ),
        mascara: descreverMascara(definicao),
        dominio,
        estadoPreenchimento: estado,
        condicao: exigirLimite(
          condicao || null,
          LIMITES_CAMPO.condicao,
          `Condição de ${descricao}`,
        ),
        notaPreenchimento: montarNotaPreenchimento(
          normalizar(definicao?.documentacao),
          notas,
          descricao,
        ),
        destinoXml,
        notaTransformacao: exigirLimite(
          notasDeTransformacao.join(' ') || null,
          LIMITES_CAMPO.notaTransformacao,
          `Nota de transformação de ${descricao}`,
        ),
        ordem: Math.min(...destinos.map(({ tag }) => tag.sequencial)),
      },
    }
  })
}
