/**
 * Monta os campos da versão a partir do mapeamento de tags do InvoiCy (mapeamento-invoicy.csv)
 * cruzado com o leiaute da DPS. Só funções puras.
 *
 * Cada campo InvoiCy vira uma linha da versão. O comportamento — tipo, tamanho, obrigatoriedade,
 * domínio — vem do leiaute, porque é a Sefin quem valida; quando a tag só existe no Anexo I, o
 * material traz apenas descrição e ocorrência. O caminho do campo no layout InvoiCy chega pronto
 * nas tags, preenchido por resolver-mapeamento.mjs.
 *
 * Os limites espelham os formulários da área interna, mais estreitos que as colunas do banco.
 */

import { escolherExemplo } from '../exemplos-xml.mjs'

export const LIMITES_CAMPO = {
  tipo: 50,
  tamanho: 40,
  mascara: 100,
  dominio: 200,
  condicao: 500,
  notaPreenchimento: 1000,
  destinoXml: 300,
  notaTransformacao: 1000,
}

const ROTULO_TIPO = { C: 'Texto', N: 'Numérico', D: 'Data' }

const MASCARA_DATA = 'AAAA-MM-DD'
const MASCARA_DATA_HORA = 'AAAA-MM-DDThh:mm:ssTZD (UTC)'

/** Tags do CSV: `codigoTag;nomeTag;elemento;tipo;codigoTagPai;codigoCampo;nomeCampo;nulos`. */
export function lerMapeamento(csv) {
  const [, ...linhas] = csv.trim().split(/\r?\n/)
  const tags = linhas.map((linha) => {
    const [codigoTag, nomeTag, elemento, tipo, codigoTagPai, codigoCampo, nomeCampo, nulos] =
      linha.split(';')
    return {
      codigoTag: Number(codigoTag),
      nomeTag,
      elemento,
      tipo,
      codigoTagPai: Number(codigoTagPai),
      codigoCampo: Number(codigoCampo),
      nomeCampo: nomeCampo || null,
      aceitaNulo: nulos !== 'Não',
    }
  })

  const porCodigo = new Map(tags.map((tag) => [tag.codigoTag, tag]))
  const caminhoDe = (tag) => {
    if (tag.codigoTagPai === 0) return tag.nomeTag
    const pai = porCodigo.get(tag.codigoTagPai)
    if (!pai)
      throw new Error(
        `Tag ${tag.codigoTag} aponta para a tag pai ${tag.codigoTagPai}, que não existe.`,
      )
    return `${caminhoDe(pai)}/${tag.nomeTag}`
  }

  return tags.map((tag) => ({ ...tag, caminho: caminhoDe(tag) }))
}

const PREFIXO_NFSE = /^NFSe\/infNFSe\//

/** Regra do RTC que torna obrigatório, na NFS-e regular, um grupo que o leiaute marca como 0-1. */
const OBRIGATORIO_NA_NOTA_REGULAR = /obrigatoriamente \(OCOR\. 1-1\) quando finNFSe = 0/

/**
 * Caminhos da DPS que a Sefin exige quando a nota é regular (finNFSe = 0). O RTC marca grupos como
 * serv e valores como opcionais porque notas de débito e crédito podem omiti-los; numa versão
 * que só emite nota regular, eles são obrigatórios.
 */
export function obrigatoriosNaNotaRegular(regras) {
  return new Set(
    regras
      .filter((regra) => regra.xpath?.startsWith('NFSe/infNFSe/DPS/'))
      .filter((regra) => OBRIGATORIO_NA_NOTA_REGULAR.test(regra.regra ?? ''))
      .map((regra) => regra.xpath.replace(PREFIXO_NFSE, '')),
  )
}

/** O material grafa "valorestrib/totTrib/pTotTrib" onde o leiaute tem "valores/trib/totTrib/pTotTrib". */
export function corrigirCaminhoDoMaterial(caminho) {
  return caminho.replace('/valorestrib/', '/valores/trib/')
}

/**
 * Índice único de definições por caminho da DPS enviada: o leiaute RTC primeiro, completado pelas
 * tags que só o Anexo I tem (descrição e ocorrência apenas). `obrigatorios` promove a 1-1 os
 * caminhos que a versão sempre exige.
 */
export function indexarDefinicoes(camposLeiaute, diferencas, obrigatorios = new Set()) {
  const indice = new Map(
    camposLeiaute.map((campo) => [corrigirCaminhoDoMaterial(campo.xpath_no_envio), campo]),
  )
  for (const campo of diferencas.campos_somente_anexo_I) {
    const caminho = campo.xpath.replace(PREFIXO_NFSE, '')
    if (!indice.has(caminho)) {
      indice.set(caminho, {
        descricao: campo.descricao,
        ocorrencia: campo.ocorrencia,
        somenteAnexoI: true,
      })
    }
  }
  for (const caminho of obrigatorios) {
    const definicao = indice.get(caminho)
    if (definicao) indice.set(caminho, { ...definicao, ocorrencia: '1-1' })
  }
  return indice
}

function normalizar(texto) {
  return texto?.replace(/\s+/g, ' ').trim() || null
}

/**
 * O leiaute embute a lista de valores na descrição ("Tipo de retenção:\n\n1 - Não retido; ...").
 * Quando a mesma lista existe estruturada, ela vai para o domínio e o significado fica só com a
 * frase que vem antes dela.
 */
export function significadoSemEnumeracao(definicao) {
  const descricao = definicao?.descricao
  if (!descricao) return null
  if (!definicao.dominio?.length) return normalizar(descricao)

  const inicioDaLista = descricao.search(/^\s*\d+\s*[-–=]/m)
  const frase = inicioDaLista > 0 ? descricao.slice(0, inicioDaLista) : descricao
  return normalizar(frase).replace(/:$/, '.')
}

function ehObrigatoria(ocorrencia) {
  return ocorrencia?.startsWith('1') ?? false
}

/** "1-15V2" → "1 a 15, com 2 decimais"; "14" → "14"; "1-60" → "1 a 60". */
export function descreverTamanho(tamanho) {
  if (!tamanho) return null
  const decimal = tamanho.match(/^(\d+)-(\d+)V(\d+)$/)
  if (decimal) return `${decimal[1]} a ${decimal[2]} dígitos inteiros, com ${decimal[3]} decimais`
  const faixa = tamanho.match(/^(\d+)-(\d+)$/)
  if (faixa) return `${faixa[1]} a ${faixa[2]}`
  return tamanho
}

/**
 * O domínio publicado é o que o XSD vigente aceita: quando o leiaute RTC lista valores que o
 * XSD 1.01 ainda recusa (finNFSe 1 e 2, opSimpNac 4), eles ficam de fora.
 */
export function descreverDominio(definicao) {
  if (!definicao?.dominio?.length) return null
  const aceitos = definicao.valores_xsd ? new Set(definicao.valores_xsd) : null
  return definicao.dominio
    .filter((item) => !aceitos || aceitos.has(item.valor))
    .map((item) => `${item.valor} = ${normalizar(item.descricao)}`)
    .join('; ')
}

/**
 * Obrigatoriedade efetiva de uma tag, pela estrutura da Sefin: a ocorrência dela só vale quando
 * todos os grupos acima também são obrigatórios. Elemento de escolha (choice) é sempre
 * condicional. O "não nulo" da configuração do InvoiCy não entra aqui: ele marca tags que são
 * escolha na DPS (vTotTrib e pTotTrib, por exemplo), e não pode significar "sempre obrigatório".
 */
export function avaliarPreenchimento(tag, tagsPorCaminho, definicoes) {
  const definicao = definicoes.get(tag.caminho)

  if (definicao?.ele === 'CE') {
    const irmas = [...tagsPorCaminho.values()]
      .filter(
        (outra) =>
          outra.codigoTagPai === tag.codigoTagPai && definicoes.get(outra.caminho)?.ele === 'CE',
      )
      .map((outra) => outra.nomeTag)
    return { estado: 'Condicional', condicao: `Informar apenas um entre: ${irmas.join(', ')}.` }
  }

  if (!ehObrigatoria(definicao?.ocorrencia)) return { estado: 'Opcional', condicao: null }

  const gruposOpcionais = []
  let pai = tagsPorCaminho.get(tag.caminho.slice(0, tag.caminho.lastIndexOf('/')))
  while (pai) {
    const definicaoPai = definicoes.get(pai.caminho)
    if (definicaoPai && !ehObrigatoria(definicaoPai.ocorrencia)) gruposOpcionais.push(pai.nomeTag)
    if (['CG', 'CE'].includes(definicaoPai?.ele)) gruposOpcionais.push(`${pai.nomeTag} (escolha)`)
    pai = tagsPorCaminho.get(pai.caminho.slice(0, pai.caminho.lastIndexOf('/')))
  }

  if (!gruposOpcionais.length) return { estado: 'Obrigatorio', condicao: null }
  return {
    estado: 'Condicional',
    condicao: `Obrigatório quando o grupo ${[...new Set(gruposOpcionais)].reverse().join(' > ')} for informado.`,
  }
}

function tipoDe(tag, definicao) {
  if (tag.tipo === 'DH') return 'Data e hora'
  if (['G', 'CG'].includes(tag.elemento)) return 'Grupo (lista)'
  return ROTULO_TIPO[definicao?.tipo] ?? ROTULO_TIPO[tag.tipo] ?? 'Texto'
}

function mascaraDe(tag, definicao) {
  if (tag.tipo === 'DH') return MASCARA_DATA_HORA
  if (definicao?.tipo === 'D' || tag.tipo === 'D') return MASCARA_DATA
  return null
}

function exigirLimite(valor, limite, descricao) {
  if (valor && valor.length > limite) {
    throw new Error(`${descricao} tem ${valor.length} caracteres e o limite é ${limite}.`)
  }
  return valor ?? null
}

/** Padrão de XSD acima disto (o de data e hora tem ~300) ajuda mais o validador que o leitor. */
const LIMITE_PADRAO_LEGIVEL = 60

/** Descrição dos valores que ainda cabe na nota de preenchimento, ao lado das outras notas. */
const LIMITE_DOMINIO_NA_NOTA = 700

/**
 * Domínio longo demais para a coluna fica só com os códigos, e a descrição de cada valor vai para
 * a nota quando couber. Nunca corta texto no meio: o que não cabe é apontado no leiaute.
 */
export function acomodarDominio(dominio) {
  if (!dominio || dominio.length <= LIMITES_CAMPO.dominio) return { dominio, notaExtra: null }

  const codigos = dominio.split('; ').map((item) => item.split(' = ')[0])
  const notaExtra =
    dominio.length <= LIMITE_DOMINIO_NA_NOTA
      ? `Significado de cada valor: ${dominio}`
      : 'Significado de cada valor: ver a tabela do campo no leiaute da DPS (Sefin).'
  return {
    dominio: exigirLimite(codigos.join(', '), LIMITES_CAMPO.dominio, 'Códigos do domínio'),
    notaExtra,
  }
}

/**
 * A descrição da tag na DPS abre a nota quando cabe: é o que a Sefin entende pelo destino, e às
 * vezes carrega regra (vServ = pCopropriedade x vTotOper no 99.03). Se não couber, as outras notas
 * — que são instrução de preenchimento — têm prioridade.
 */
function montarNotaPreenchimento(descricaoNaDps, notas, descricao) {
  const comDescricao = [descricaoNaDps ? `Na DPS: ${descricaoNaDps}` : null, ...notas].filter(
    Boolean,
  )
  const texto = comDescricao.join('\n\n')
  if (texto.length <= LIMITES_CAMPO.notaPreenchimento) return texto || null
  return exigirLimite(
    notas.join('\n\n') || null,
    LIMITES_CAMPO.notaPreenchimento,
    `Nota de preenchimento de ${descricao}`,
  )
}

/**
 * Um registro por campo do XML de entrada do InvoiCy. Quando o mesmo campo alimenta mais de uma
 * tag (o país da prestação também alimenta o país do resultado, o CEP também alimenta o postal do
 * exterior), a linha é uma só, com todos os destinos — a versão não admite o mesmo campo duas vezes.
 *
 * A chave é o caminho no XML de entrada, não o código interno: a configuração do provedor tem
 * códigos diferentes para o mesmo campo (TomaCEP é 112 e 279), e para o parceiro é um campo só.
 * Sem caminho resolvido, a chave cai para o código.
 *
 * `notasDeComposicao` (código do campo → nota) registra as tags da DPS que o InvoiCy monta a partir
 * de mais de um campo, como o Id.
 *
 * `fonteLida` (exemplos de XML reais, lida por exemplos-xml.mjs) liga o exemplo: o campo passa a ter
 * `campoVersao.exemplo`, com o texto escolhido ou `null` quando nenhum valor pode ser publicado.
 * Sem ela a chave não existe, o que significa "exemplo não gerenciado por esta carga". O motivo da
 * decisão fica em `decisaoDeExemplo`, fora de `campoVersao` para não ir no corpo da API.
 */
export function montarCampos(tags, definicoes, notasDeComposicao = new Map(), fonteLida = null) {
  const tagsPorCaminho = new Map(tags.map((tag) => [tag.caminho, tag]))
  const porCampo = new Map()
  for (const tag of tags.filter((tag) => tag.codigoCampo)) {
    const chave = tag.caminhoInvoicy ?? tag.codigoCampo
    const lista = porCampo.get(chave) ?? []
    lista.push(tag)
    porCampo.set(chave, lista)
  }

  return [...porCampo.values()].map((destinos) => {
    const [principal] = destinos
    const codigosCampo = [...new Set(destinos.map((tag) => tag.codigoCampo))]
    const codigoCampo = codigosCampo.join('/')
    const definicao = definicoes.get(principal.caminho)
    const preenchimentos = destinos.map((tag) =>
      avaliarPreenchimento(tag, tagsPorCaminho, definicoes),
    )
    const estado =
      new Set(preenchimentos.map((p) => p.estado)).size === 1
        ? preenchimentos[0].estado
        : 'Condicional'
    const condicao = [...new Set(preenchimentos.map((p) => p.condicao).filter(Boolean))].join(' ')

    const { dominio, notaExtra } = acomodarDominio(descreverDominio(definicao))
    const notas = [
      normalizar(definicao?.notas),
      definicao?.pattern_xsd && definicao.pattern_xsd.length <= LIMITE_PADRAO_LEGIVEL
        ? `Formato aceito pelo XSD: ${definicao.pattern_xsd}`
        : null,
      notaExtra,
      destinos.some((tag) => !tag.aceitaNulo)
        ? 'No InvoiCy, este campo está configurado como não nulo.'
        : null,
      definicao ? null : 'O material da Sefin não descreve esta tag.',
    ].filter(Boolean)

    const descricao = `Campo ${codigoCampo} (${principal.nomeCampo})`
    const notasDeTransformacao = [
      destinos.length > 1
        ? `O InvoiCy grava este campo em ${destinos.length} tags da DPS: ${destinos.map((tag) => tag.caminho).join(', ')}.`
        : null,
      ...codigosCampo.map((codigo) => notasDeComposicao.get(codigo) ?? null),
    ].filter(Boolean)

    const campoVersao = {
      tipo: tipoDe(principal, definicao),
      tamanho: exigirLimite(
        descreverTamanho(definicao?.tamanho),
        LIMITES_CAMPO.tamanho,
        `Tamanho de ${descricao}`,
      ),
      mascara: mascaraDe(principal, definicao),
      dominio,
      estadoPreenchimento: estado,
      condicao: exigirLimite(condicao || null, LIMITES_CAMPO.condicao, `Condição de ${descricao}`),
      notaPreenchimento: montarNotaPreenchimento(
        significadoSemEnumeracao(definicao),
        notas,
        descricao,
      ),
      destinoXml: exigirLimite(
        destinos.map((tag) => tag.caminho).join(' | '),
        LIMITES_CAMPO.destinoXml,
        `Destino de ${descricao}`,
      ),
      notaTransformacao: exigirLimite(
        notasDeTransformacao.join(' ') || null,
        LIMITES_CAMPO.notaTransformacao,
        `Nota de transformação de ${descricao}`,
      ),
      ordem: principal.codigoTag,
    }

    const item = {
      codigosCampo,
      nomeCampo: principal.nomeCampo,
      caminhoInvoicy: principal.caminhoInvoicy ?? null,
      campoVersao,
    }
    if (!fonteLida) return item

    const decisaoDeExemplo = escolherExemplo({
      destinos: destinos.map((tag) => ({
        caminho: tag.caminho,
        elemento: tag.elemento,
        alternativa: false,
      })),
      fonteLida,
      campoVersao,
    })
    campoVersao.exemplo = decisaoDeExemplo.exemplo
    return { ...item, decisaoDeExemplo }
  })
}
