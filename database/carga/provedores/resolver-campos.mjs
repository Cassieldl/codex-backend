/**
 * Liga cada tag do mapeamento de um provedor ao campo do XSD de entrada do InvoiCy, pelo NOME do
 * campo InvoiCy — o mapeamento exportado em Markdown não traz o código. Só funções puras.
 *
 * Nome único no XSD resolve sozinho. O resto é decisão registrada aqui, com o motivo; um campo que
 * não caia em nenhum caso interrompe a montagem em vez de ser adivinhado. Grafia diferente só é
 * resolvida quando já foi confirmada pelo usuário; a nova fica pendente.
 */

import { CAMINHOS_EM_DUVIDA } from '../emissor-nacional/resolver-mapeamento.mjs'

/** Família do leiaute do provedor: decide o sentido de nomes que o InvoiCy repete (cMun). */
export const FAMILIAS = ['dps', 'abrasf', 'proprio']

/** Grafia diferente entre a configuração e o XSD, já confirmada (a mesma do Emissor Nacional). */
export const RENOMEADOS = new Map([
  ['TomaXMun', 'Envio/RPS/Tomador/TomaxMun'],
  ['tpRetPISCofins', 'Envio/RPS/Servico/IBSCBS/tpRetPisCofins'],
  ['ListaNFSeRef', 'Envio/RPS/ListaNFSeReferenciadas'],
])

/**
 * O XSD tem dois `cMun` com nome exato. No leiaute DPS a tag é o `cLocEmi`, município emissor —
 * quem emite é o prestador, como no Emissor Nacional. No ABRASF e no IPM, o mapeamento descreve o campo como
 * "Código do Município do Serviço".
 */
export const POR_FAMILIA = new Map([
  [
    'cMun',
    {
      dps: 'Envio/RPS/Prestador/enderPrest/cMun',
      abrasf: 'Envio/RPS/Servico/cMun',
      proprio: 'Envio/RPS/Servico/cMun',
    },
  ],
])

/** O mesmo campo do InvoiCy aparece com dois nomes na configuração: a planilha diz "CNPJ ou CPF". */
export const UNIFICADOS = new Map([['CNPJ_prest (CPF)', 'CNPJ_prest']])

/** Não é campo: o Id da DPS é composto pelo InvoiCy a partir de número e série. */
export const COMPOSICOES = new Map([['RPSNumero e RPSSerie', ['RPSNumero', 'RPSSerie']]])

/** Uma tag do provedor que recebe um campo OU outro, conforme o caso (CPF ou CNPJ do tomador). */
export const ALTERNATIVAS = new Map([['TomaCNPJ e TomaCPF', ['TomaCNPJ', 'TomaCPF']]])

const MOTIVO_REEREPRES = 'Bloco gReeRepRes (1.01) sem equivalente confiável no layout v8.'
const MOTIVO_EXTERIOR = 'Campo "…Ex" que não existe no layout de entrada.'
const MOTIVO_TPOPER =
  'No XSD e na planilha v8, tpOper (valor de 1 a 5) é um grupo com os campos de substituição, e RPSSubs é vazio.'

/** Esperando resposta do InvoiCy: os do Emissor Nacional (por nome) e os que só apareceram nestes provedores. */
export const PENDENTES = new Map([
  ...[
    'DocTipoChaveDFe',
    'DocDescTipoChaveDFe',
    'DocChaveDFe',
    'DoccMun',
    'DocNumero',
    'DocDescricao',
    'DocCNPJ',
    'DocCPF',
    'DocNIF',
    'DoccNaoNIF',
    'DocRazaoSocial',
    'DocDataEmissao',
    'DocDCompetencia',
    'DocTipoReeRepRes',
    'DocDescTpReeRepRes',
    'DocValorReeRepRes',
    'ListaDocumentosRef',
  ].map((nome) => [nome, MOTIVO_REEREPRES]),
  ...[
    'AtivEndCEPEx',
    'AtivEndxMunEx',
    'AtivEndUfEx',
    'DedFornCEPEx',
    'DedFornxMunEx',
    'DedFornUFEx',
    'ObraXMunEx',
    'IntermUFEx',
  ].map((nome) => [nome, MOTIVO_EXTERIOR]),
  ['indTotTrib', 'indTotTrib não existe no layout de entrada.'],
  ...['tpOper', 'SubsChave', 'SubsCodMotivo', 'SubsDescMotivo'].map((nome) => [
    nome,
    MOTIVO_TPOPER,
  ]),
  ['xTpEnteGov', 'xTpEnteGov não existe no layout de entrada.'],
  [
    'ValRedAliqUF',
    'Não existe no layout de entrada; o mais próximo é ValpRedAliqUF (IBS/ValpRedAliqUF), a confirmar.',
  ],
])

const SUFIXO_COMPRA_GOV = /_CompraGov$/
const GRUPO_COMPRA_GOV = 'Envio/RPS/Servico/Valores/IBSCBS/TribCompraGov'

function indexarPorNome(elementosXsd) {
  const porNome = new Map()
  for (const elemento of elementosXsd) {
    const lista = porNome.get(elemento.nome) ?? []
    lista.push(elemento.caminho)
    porNome.set(elemento.nome, lista)
  }
  return porNome
}

function emDuvida(caminho) {
  return CAMINHOS_EM_DUVIDA.some(
    (prefixo) => caminho === prefixo || caminho.startsWith(`${prefixo}/`),
  )
}

/**
 * Caminho no XSD do InvoiCy para um nome de campo, ou `null` quando o nome não existe lá.
 *
 * Compras governamentais: a configuração distingue o grupo TribCompraGov pelo sufixo `_CompraGov`
 * (ValpIBSUF_CompraGov), enquanto o XSD repete o nome da tag dentro do grupo. O nome sem sufixo é,
 * então, o do tributo regular — o único dos dois caminhos fora de TribCompraGov.
 */
export function caminhoNoInvoicy(nomeCampo, familia, porNome) {
  if (RENOMEADOS.has(nomeCampo)) return RENOMEADOS.get(nomeCampo)
  if (POR_FAMILIA.has(nomeCampo)) return POR_FAMILIA.get(nomeCampo)[familia]

  if (SUFIXO_COMPRA_GOV.test(nomeCampo)) {
    const nome = nomeCampo.replace(SUFIXO_COMPRA_GOV, '')
    const caminho = `${GRUPO_COMPRA_GOV}/${nome}`
    return (porNome.get(nome) ?? []).includes(caminho) ? caminho : null
  }

  const candidatos = porNome.get(nomeCampo) ?? []
  if (candidatos.length === 1) return candidatos[0]
  if (candidatos.length === 0) return null

  const foraDaCompraGov = candidatos.filter(
    (caminho) => !caminho.startsWith(`${GRUPO_COMPRA_GOV}/`),
  )
  if (foraDaCompraGov.length === 1) return foraDaCompraGov[0]

  throw new Error(
    `O campo ${nomeCampo} tem ${candidatos.length} caminhos no XSD do InvoiCy e não está nas decisões registradas.`,
  )
}

/**
 * Classifica cada tag com campo InvoiCy. Devolve as tags com `campos` (um item por campo InvoiCy
 * que a tag recebe, com o caminho no XSD) e as listas do que ficou de fora:
 * - `pendentes`: esperam o InvoiCy (nome → motivo);
 * - `internos`: campo preenchido pelo próprio InvoiCy (senha, lote), sem caminho no XML de entrada.
 */
export function resolverCampos(tags, elementosXsd, familia) {
  if (!FAMILIAS.includes(familia)) throw new Error(`Família de leiaute desconhecida: ${familia}.`)
  const porNome = indexarPorNome(elementosXsd)
  const pendentes = new Map()
  const internos = new Set()

  const resolver = (nome, tag) => {
    if (PENDENTES.has(nome)) {
      pendentes.set(nome, PENDENTES.get(nome))
      return null
    }
    const caminho = caminhoNoInvoicy(nome, familia, porNome)
    if (!caminho) {
      if (tag.interno) {
        internos.add(nome)
        return null
      }
      throw new Error(
        `Sequencial ${tag.sequencial} (${tag.caminho}): o campo ${nome} não existe no XSD do InvoiCy e não está nas decisões registradas.`,
      )
    }
    if (emDuvida(caminho)) {
      // As regiões em dúvida são as da substituição (RPSSubs e tpOper), pelo mesmo motivo.
      pendentes.set(nome, MOTIVO_TPOPER)
      return null
    }
    // A raiz do RPS não é campo: é a própria nota.
    if (caminho === 'Envio/RPS') return null
    return { nomeCampo: nome, caminhoInvoicy: caminho }
  }

  const resolvidas = tags.map((tag) => {
    if (!tag.nomeCampo) return { ...tag, campos: [] }

    if (COMPOSICOES.has(tag.nomeCampo))
      return { ...tag, campos: [], compoeDe: COMPOSICOES.get(tag.nomeCampo) }

    if (ALTERNATIVAS.has(tag.nomeCampo)) {
      const campos = ALTERNATIVAS.get(tag.nomeCampo)
        .map((nome) => resolver(nome, tag))
        .filter(Boolean)
        .map((campo) => ({ ...campo, alternativa: true }))
      return { ...tag, campos }
    }

    const nome = UNIFICADOS.get(tag.nomeCampo) ?? tag.nomeCampo
    const campo = resolver(nome, tag)
    return { ...tag, campos: campo ? [campo] : [] }
  })

  return { tags: resolvidas, pendentes, internos }
}
