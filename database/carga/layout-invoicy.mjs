/**
 * Versões do Layout InvoiCy na API: estados, vocabulário multiversão e estrutura de cada versão.
 * Toda carga de provedor passa por aqui antes dos próprios campos, para que nenhuma delas
 * force estado de layout nem reposicione o vocabulário por conta própria.
 */

import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { montarEstrutura } from './emissor-nacional/estrutura-layout.mjs'
import { sincronizarVocabulario } from './emissor-nacional/sincronizar-campos.mjs'
import { chaveDeSlug } from './emissor-nacional/transformar-layout.mjs'
import { lerMaterialDoLayout, lerVocabularioInvoicy } from './vocabulario-invoicy.mjs'

const PASTA_DO_LAYOUT = join(dirname(fileURLToPath(import.meta.url)), 'emissor-nacional')

/** Layout ao qual os provedores estão mapeados. Não precisa ser o vigente. */
export const ROTULO_LAYOUT_DOS_PROVEDORES = '8'

/** Da mais antiga para a mais nova: a última é a Vigente, as outras ficam Anteriores. */
export const VERSOES_LAYOUT_INVOICY = [
  {
    rotulo: '8',
    descricao:
      'Layout de recepção de NFS-e do InvoiCy, versão 8 (Reforma Tributária, NT 009). Caminhos tirados do XSD de entrada.',
  },
  {
    rotulo: '9',
    // Uma linha por tópico, no formato "Rótulo: texto"; a primeira linha é o resumo da Home.
    descricao: [
      'InvoiCy 3.3.0.0 (NT 009 v1.01).',
      'Grupos novos: Condominio (cobranças, detalhamento e descontos) e RPSSubs (dados da substituição).',
      'Campos novos: nroDUIMP, nroDUE, nroContCambio, ValAjusteBCIBSCBSComExt, DestIE e DestIM.',
      'Mudaram de posição: dados da substituição (de tpOper para RPSSubs); Fornecedor e DocumentoFiscalOutro (agora dentro de Documento); endereço do imóvel (só na unidade imobiliária).',
      'Saíram ou mudaram: ValServEst saiu; nDI e nRE deram lugar a nroDUIMP e nroDUE; ItermIE virou IntermIE; CEPs passam a texto de 8 posições.',
    ].join('\n'),
    normaOrigem: 'NT 009 v1.01',
    homologacaoDesde: '2026-10-01',
    liberadaNoInvoicyEm: '2026-10-22',
    obrigatoriaEm: '2026-12-01',
    // Caminhos canônicos: "antes" na estrutura da 8, "depois" na da 9.
    mudancas: [
      {
        tipo: 'grupoNovo',
        depois: '//Condominio',
        descricao:
          'Grupo do condomínio: cobranças (ListaCobranca), detalhamento de cada cobrança (ListaDetCobranca) e descontos (ListaDesconto).',
      },
      {
        tipo: 'grupoNovo',
        depois: '//RPSSubs',
        descricao: 'Grupo com os dados do RPS ou da NFS-e substituída.',
      },
      {
        tipo: 'campoNovo',
        depois: '//Servico/Valores/nroContCambio',
        descricao: 'Número do contrato de câmbio da operação de comércio exterior.',
      },
      {
        tipo: 'campoNovo',
        depois: '//Servico/Valores/IBSCBS/ValAjusteBCIBSCBSComExt',
        descricao: 'Ajuste da base de cálculo do IBS e da CBS no comércio exterior.',
      },
      {
        tipo: 'campoNovo',
        depois: '//Destinatario/DestIE',
        descricao: 'Inscrição estadual do destinatário.',
      },
      {
        tipo: 'campoNovo',
        depois: '//Destinatario/DestIM',
        descricao: 'Inscrição municipal do destinatário.',
      },
      {
        tipo: 'mudouDeLugar',
        antes: '//tpOper',
        depois: '//RPSSubs',
        descricao:
          'Os dados da substituição (SubsNumero, SubsSerie, SubsTipo, SubsNFSeNumero, SubsDEmisNFSe, SubsChave, SubsCodMotivo e SubsDescMotivo) saem de dentro de tpOper e vão para RPSSubs; tpOper vira campo simples.',
      },
      {
        tipo: 'mudouDeLugar',
        antes: '//ListaBensMoveis/BemMovel/BemMovelcNCM',
        depois: '//ListaDocumentos/Documento/Fornecedor',
        descricao:
          'Os dados do fornecedor (documento, nome e endereço) saem de BemMovelcNCM e passam ao grupo Fornecedor, dentro de Documento.',
      },
      {
        tipo: 'mudouDeLugar',
        antes: '//ListaNFSeReferenciadas/NFSeReferenciada',
        depois: '//ListaDocumentos/Documento/DocumentoFiscalOutro',
        descricao:
          'O outro documento fiscal (DoccMun, DocNumero e DocDescricao) sai da NFS-e referenciada e passa ao grupo DocumentoFiscalOutro, dentro de Documento.',
      },
      {
        tipo: 'mudouDeLugar',
        antes: '//Imovel/ImovEndereco',
        depois: '//Imovel/Locacao/ListaUnidadesImobiliarias/UnidadeImobiliaria/ImovEndereco',
        descricao:
          'O endereço do imóvel (ImovEndereco, ImovNumero, ImovBairro, ImovxMun e ImovCEP) fica só na unidade imobiliária.',
      },
      {
        tipo: 'renomeado',
        antes: '//Servico/Valores/nDI',
        depois: '//Servico/Valores/nroDUIMP',
        descricao: 'O número da declaração de importação passa a ser o da DUIMP.',
      },
      {
        tipo: 'renomeado',
        antes: '//Servico/Valores/nRE',
        depois: '//Servico/Valores/nroDUE',
        descricao: 'O número do registro de exportação passa a ser o da DUE.',
      },
      {
        tipo: 'renomeado',
        antes: '//IntermServico/ItermIE',
        depois: '//IntermServico/IntermIE',
        descricao: 'Inscrição estadual do intermediário com a grafia corrigida.',
      },
      {
        tipo: 'saiu',
        antes: '//Servico/Valores/ValServEst',
        descricao: 'O valor do serviço em moeda estrangeira saiu do layout.',
      },
      {
        tipo: 'alterado',
        antes: '//Prestador/enderPrest/CEP',
        depois: '//Prestador/enderPrest/CEP',
        descricao: 'CEP do prestador passa de número a texto de 8 posições.',
      },
      {
        tipo: 'alterado',
        antes: '//Tomador/TomaCEP',
        depois: '//Tomador/TomaCEP',
        descricao: 'CEP do tomador passa de número a texto de 8 posições.',
      },
      {
        tipo: 'alterado',
        antes: '//ConstCivil/ObraCEP',
        depois: '//ConstCivil/ObraCEP',
        descricao: 'CEP da obra passa de número a texto de 8 posições.',
      },
    ],
  },
]

/** Norma e datas da versão, em "yyyy-MM-dd". Chave ausente na configuração não é tocada na API. */
const CAMPOS_DO_CALENDARIO = [
  'normaOrigem',
  'homologacaoDesde',
  'liberadaNoInvoicyEm',
  'obrigatoriaEm',
]

/** Só os campos do calendário configurados que diferem do que a API tem; vazio quando nada muda. */
export function mudancasDeCalendario(configurada, existente = {}) {
  return Object.fromEntries(
    CAMPOS_DO_CALENDARIO.filter(
      (chave) =>
        configurada[chave] !== undefined && configurada[chave] !== (existente[chave] ?? null),
    ).map((chave) => [chave, configurada[chave]]),
  )
}

/** Mudança no formato que a API devolve: caminho ausente vira nulo. */
function normalizarMudanca({ tipo, antes, depois, descricao }) {
  return { tipo, antes: antes ?? null, depois: depois ?? null, descricao }
}

/**
 * A lista de mudanças configurada quando difere da que a API tem; vazio quando é igual ou quando a
 * versão não configura mudanças. A lista vai inteira: a API substitui, não mescla.
 */
export function mudancasDaLista(configurada, existente = {}) {
  if (configurada.mudancas === undefined) return {}
  const desejada = configurada.mudancas.map(normalizarMudanca)
  const atual = (existente.mudancas ?? []).map(normalizarMudanca)
  return JSON.stringify(desejada) === JSON.stringify(atual) ? {} : { mudancas: desejada }
}

/** Estrutura gerada de uma versão, versionada ao lado do XSD para o teste do backend validar o XML. */
export function arquivoDaEstrutura(rotulo) {
  return join(PASTA_DO_LAYOUT, `layout-invoicy-v${rotulo}-estrutura.json`)
}

/**
 * Nota da versão: as linhas do histórico do InvoiCy que tratam da NT 009 na série 3.x. Linha que
 * manda usar outra versão do layout fica de fora: na página da 9, ela confunde o parceiro.
 */
export function descricaoDaVersao(rotulo, versoesDoInvoicy) {
  const alteracoes = versoesDoInvoicy
    .filter(({ versao, descricao }) => versao.startsWith('3.') && /NT\s?0?09/i.test(descricao))
    .filter(({ descricao }) => !/layout na vers[aã]o \d/i.test(descricao))
    .map(({ versao, descricao }) => `InvoiCy ${versao}: ${descricao.replace(/\.?$/, '.')}`)
  return [
    `Layout de recepção de NFS-e do InvoiCy, versão ${rotulo} (Reforma Tributária, NT 009).`,
    ...alteracoes,
    'Caminhos tirados do XSD de entrada.',
  ].join(' ')
}

/**
 * Plano do vocabulário de várias versões, da mais antiga para a mais nova. O campo é identificado
 * pelo caminho canônico: nasce na primeira versão que o tem e fica descontinuado na primeira que
 * deixou de ter. Texto e grupo vêm da versão mais nova que o tem (a 8 tinha descrições trocadas).
 *
 * A ordem é uma só para todas as versões: a da mais nova, com o que só existe nas antigas
 * encaixado logo depois do campo que o precedia lá.
 */
export function planejarVocabularioMultiversao(versoes) {
  const presenca = new Map()
  versoes.forEach(({ campos }, indice) => {
    for (const campo of campos) {
      const registro = presenca.get(campo.caminhoXml) ?? { indices: [], campo }
      registro.indices.push(indice)
      registro.campo = campo
      presenca.set(campo.caminhoXml, registro)
    }
  })

  const slugs = new Map()
  for (const caminhoXml of presenca.keys()) {
    const chave = chaveDeSlug(caminhoXml)
    if (slugs.has(chave)) throw new Error(`${caminhoXml} e ${slugs.get(chave)} geram o mesmo slug.`)
    slugs.set(chave, caminhoXml)
  }

  const sequencia = versoes.at(-1).campos.map((campo) => campo.caminhoXml)
  for (const { campos } of versoes.slice(0, -1).reverse()) {
    let anterior = null
    for (const { caminhoXml } of campos) {
      if (!sequencia.includes(caminhoXml))
        sequencia.splice(anterior ? sequencia.indexOf(anterior) + 1 : 0, 0, caminhoXml)
      anterior = caminhoXml
    }
  }

  const campos = sequencia.map((caminhoXml, posicao) => {
    const { indices, campo } = presenca.get(caminhoXml)
    const primeira = indices[0]
    const ultima = indices.at(-1)
    if (ultima - primeira + 1 !== indices.length)
      throw new Error(`${caminhoXml} sai e volta entre as versões; o caminho é imutável.`)

    return {
      caminhoXml,
      nome: campo.nome,
      significado: campo.significado,
      grupo: campo.grupo,
      ordem: posicao + 1,
      reformaTributaria: campo.reformaTributaria === true,
      origem: versoes[primeira].rotulo,
      descontinuadoEm: ultima < versoes.length - 1 ? versoes[ultima + 1].rotulo : null,
    }
  })

  const porVersao = new Map(
    versoes.map(({ rotulo, campos: daVersao }) => [
      rotulo,
      daVersao.map(({ caminhoCompleto, caminhoXml }) => ({ caminhoCompleto, caminhoXml })),
    ]),
  )
  return { campos, porVersao }
}

/**
 * Transições para chegar a "última Vigente, demais Anteriores" sem nunca ter duas Vigentes, que o
 * banco proíbe. A API só anda Prevista, Vigente, Anterior; uma antiga ainda Prevista passa por
 * Vigente no caminho.
 */
export function planejarEstados(existentes) {
  const estados = existentes.map(({ estado }) => estado)
  const passos = []
  const mudar = (indice, estado) => {
    passos.push({ indice, estado })
    estados[indice] = estado
  }
  const rebaixarVigenteAtual = () => {
    const vigente = estados.findIndex((estado) => estado === 'Vigente')
    if (vigente >= 0) mudar(vigente, 'Anterior')
  }

  const ultima = estados.length - 1
  if (estados[ultima] === 'Anterior')
    throw new Error(`A versão ${existentes[ultima].rotulo} já é Anterior e não volta a Vigente.`)

  for (let indice = 0; indice < ultima; indice++) {
    if (estados[indice] === 'Prevista') {
      if (estados[ultima] === 'Vigente')
        throw new Error(
          `A versão ${existentes[indice].rotulo} ainda é Prevista e a ${existentes[ultima].rotulo} já é Vigente.`,
        )
      rebaixarVigenteAtual()
      mudar(indice, 'Vigente')
    }
    if (estados[indice] === 'Vigente') mudar(indice, 'Anterior')
  }
  if (estados[ultima] === 'Prevista') mudar(ultima, 'Vigente')
  return passos
}

function itensDe(dados) {
  return dados.items ?? dados
}

/**
 * Cria as versões que faltam e alinha a descrição, a norma, as datas e as mudanças das existentes,
 * na ordem; devolve as da configuração com o estado atual.
 */
async function garantirVersoes(api, versoes) {
  const { dados } = await api.chamar('GET', '/interno/layout/versoes?tamanhoPagina=200')
  const existentes = itensDe(dados)

  const resultado = []
  for (const configurada of versoes) {
    const { rotulo, descricao } = configurada
    let versao = existentes.find((item) => item.rotulo === rotulo)
    if (!versao) {
      ;({ dados: versao } = await api.chamar('POST', '/interno/layout/versoes', {
        rotulo,
        descricao,
        ...mudancasDeCalendario(configurada),
        ...mudancasDaLista(configurada),
      }))
      console.log(`versão ${rotulo} do Layout InvoiCy criada (id ${versao.id})`)
      resultado.push(versao)
      continue
    }

    const alteracoes = {
      ...(versao.descricao !== descricao ? { descricao } : {}),
      ...mudancasDeCalendario(configurada, versao),
      ...mudancasDaLista(configurada, versao),
    }
    if (Object.keys(alteracoes).length > 0) {
      const { dados } = await api.chamar(
        'PUT',
        `/interno/layout/versoes/${versao.id}`,
        alteracoes,
        String(versao.versaoConcorrencia),
      )
      versao = { ...versao, ...dados, ...alteracoes }
      console.log(
        `versão ${rotulo} do Layout InvoiCy atualizada: ${Object.keys(alteracoes).join(', ')}`,
      )
    }
    resultado.push(versao)
  }

  const fora = resultado.findIndex(
    (versao, indice) => indice > 0 && versao.ordem < resultado[indice - 1].ordem,
  )
  if (fora > 0)
    throw new Error(
      `A versão ${resultado[fora].rotulo} tem ordem anterior à ${resultado[fora - 1].rotulo}.`,
    )
  return resultado
}

async function alinharEstados(api, versoes) {
  for (const { indice, estado } of planejarEstados(versoes)) {
    const versao = versoes[indice]
    const { dados } = await api.chamar(
      'PUT',
      `/interno/layout/versoes/${versao.id}`,
      { estado },
      String(versao.versaoConcorrencia),
    )
    console.log(`versão ${versao.rotulo} do Layout InvoiCy: ${versao.estado} → ${estado}`)
    versoes[indice] = { ...versao, ...dados, estado }
  }
}

/** Vocabulário, descrição e estrutura de cada versão configurada, lidos dos arquivos versionados. */
export async function lerLayoutsInvoicy(versoes = VERSOES_LAYOUT_INVOICY) {
  const lidas = []
  for (const versao of versoes) {
    const { planilha } = await lerMaterialDoLayout(versao.rotulo)
    const { vocabulario } = await lerVocabularioInvoicy(versao.rotulo)
    lidas.push({
      ...versao,
      descricao: versao.descricao ?? descricaoDaVersao(versao.rotulo, planilha.versoes ?? []),
      campos: vocabulario,
      estrutura: await gerarEstrutura(versao.rotulo),
    })
  }
  return { versoes: lidas }
}

/**
 * Tags da planilha com caminho errado, por versão. Na 8, os três filhos de RepositorioNacional
 * aparecem sob DoctpReeRepRes, que nem existe no XSD 8; o XSD já os traz no lugar certo.
 */
const TAGS_DA_PLANILHA_COM_CAMINHO_ERRADO = {
  8: [
    'Envio/RPS/ListaDocumentos/Documento/DoctpReeRepRes/DocTipoChaveDFe',
    'Envio/RPS/ListaDocumentos/Documento/DoctpReeRepRes/DocxTipoChaveDFe',
    'Envio/RPS/ListaDocumentos/Documento/DoctpReeRepRes/DocChaveDFe',
  ],
}

/** Gera a estrutura da versão a partir do XSD e da planilha; o arquivo versionado é esta saída. */
export async function gerarEstrutura(rotulo) {
  const { xsd, planilha } = await lerMaterialDoLayout(rotulo)
  const { vocabulario } = await lerVocabularioInvoicy(rotulo)
  return montarEstrutura(
    xsd,
    planilha,
    new Set(vocabulario.map((campo) => campo.caminhoXml)),
    new Set(TAGS_DA_PLANILHA_COM_CAMINHO_ERRADO[rotulo] ?? []),
  )
}

/**
 * Deixa as versões do layout como a configuração diz e devolve o id de cada uma e, por rótulo,
 * caminho completo → id do campo canônico ativo nela. Não publica nada.
 */
export async function sincronizarLayoutInvoicy(api) {
  const { versoes } = await lerLayoutsInvoicy()
  const plano = planejarVocabularioMultiversao(versoes)

  const existentes = await garantirVersoes(api, versoes)
  await alinharEstados(api, existentes)
  const idPorRotulo = new Map(existentes.map((versao) => [versao.rotulo, versao.id]))

  const idPorCaminhoPorRotulo = await sincronizarVocabulario(api, idPorRotulo, plano)

  for (const [indice, versao] of existentes.entries()) {
    await api.chamar(
      'PUT',
      `/interno/layout/versoes/${versao.id}/estrutura`,
      versoes[indice].estrutura,
      String(versao.versaoConcorrencia),
    )
    console.log(`estrutura da versão ${versao.rotulo} gravada`)
  }

  return { idPorRotulo, idPorCaminhoPorRotulo }
}
