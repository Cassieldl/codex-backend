/**
 * Carga do provedor Emissor Nacional a partir do material coletado da Sefin Nacional.
 *
 * Uso, com a API no ar:
 *   CODEX_DEV_USUARIO=... CODEX_DEV_SENHA=... node database/carga/emissor-nacional/importar.mjs <pasta-do-material>
 *
 * Sincroniza, em vez de só criar: cria o que falta, corrige o texto do que mudou e remove o que
 * deixou de valer para a versão — pode rodar de novo a qualquer momento. Nada é publicado: o
 * provedor fica oculto até o InvoiCy responder pelos campos pendentes (relatório da demanda 008).
 *
 * A versão 0.20 envia o leiaute vigente (Anexo I v1.01), não o RTC: os erros são os do material
 * menos os exclusivos do RTC, mais os exclusivos do Anexo I. Os campos vêm do mapeamento do
 * provedor (mapeamento-invoicy.csv), com caminho no layout de entrada v8 do InvoiCy
 * (layout-invoicy-v8.xsd) — arquivos versionados ao lado deste.
 */

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { criarClienteApi } from '../cliente-api.mjs'
import {
  CAMINHOS_EM_DUVIDA,
  notasDeComposicao,
  resolverMapeamento,
} from './resolver-mapeamento.mjs'
import { resolverReferencias, sincronizarArtigo } from './sincronizar-artigos.mjs'
import { sincronizarCamposDaVersao, sincronizarVocabulario } from './sincronizar-campos.mjs'
import {
  INTRODUCAO_VERSAO,
  codigosSomenteRtc,
  montarAvisos,
  montarErros,
  rejeicoesSomenteAnexoI,
} from './transformar.mjs'
import {
  indexarDefinicoes,
  lerMapeamento,
  montarCampos,
  obrigatoriosNaNotaRegular,
} from './transformar-campos.mjs'
import { lerXsd, montarVocabulario } from './transformar-layout.mjs'

const PASTA_DA_CARGA = dirname(fileURLToPath(import.meta.url))

const PROVEDOR = {
  nome: 'Emissor Nacional',
  identificadorInvoicy: 'EMISSOR_NACIONAL',
  categoria: 'Nacional',
  aliases: ['ADN', 'Sefin Nacional', 'Padrão Nacional', 'NFS-e Nacional'],
}

const ROTULO_VERSAO = '0.20'

/**
 * Artigos do provedor. Campos pelo caminho completo no XSD de entrada; erros pelo código. A emissão
 * judicial liga os erros do teto de alíquota (E0595, E1300): é por eles que o analista chega quando a
 * nota que devia ser judicial seguiu o fluxo normal.
 */
const ARTIGOS = [
  {
    arquivo: 'artigo-emissao-judicial.md',
    tipo: 'Procedimento',
    titulo: 'Emitir NFS-e por decisão judicial no Emissor Nacional',
    campos: [
      'Envio/RPS/nNFSeJudicial',
      'Envio/RPS/NumProcesso',
      'Envio/RPS/tpSusp',
      'Envio/RPS/RPSNumero',
      'Envio/RPS/RPSSerie',
      'Envio/RPS/IDUnico',
      'Envio/RPS/Servico/Valores/ValAliqISS',
    ],
    erros: ['E0595', 'E1300'],
  },
]

const LAYOUT_INVOICY = {
  rotulo: '8',
  descricao:
    'Layout de recepção de NFS-e do InvoiCy, versão 8 (Reforma Tributária, NT 009). Caminhos tirados do XSD de entrada.',
}

async function lerJson(pasta, arquivo) {
  return JSON.parse(await readFile(join(pasta, arquivo), 'utf8'))
}

async function lerTexto(arquivo) {
  return readFile(join(PASTA_DA_CARGA, arquivo), 'utf8')
}

function itensDe(dados) {
  return dados.items ?? dados
}

async function garantirLayoutInvoicy(api) {
  const { dados } = await api.chamar('GET', '/interno/layout/versoes')
  const existente = itensDe(dados).find((versao) => versao.rotulo === LAYOUT_INVOICY.rotulo)
  if (existente) return existente.id

  const { dados: criada } = await api.chamar('POST', '/interno/layout/versoes', LAYOUT_INVOICY)
  console.log(`versão ${LAYOUT_INVOICY.rotulo} do Layout InvoiCy criada (id ${criada.id})`)
  return criada.id
}

async function garantirProvedor(api) {
  const provedores = await api.listarTudo(
    `/interno/provedores?q=${encodeURIComponent(PROVEDOR.nome)}`,
  )
  const existente = provedores.find((provedor) => provedor.nome === PROVEDOR.nome)
  if (existente) return existente

  const { dados: criado } = await api.chamar('POST', '/interno/provedores', PROVEDOR)
  console.log(`provedor ${PROVEDOR.nome} criado (id ${criado.id})`)
  return criado
}

/**
 * Alinha introdução e layout de origem da versão existente. Trocar o layout só é aceito pela API
 * enquanto a versão não tem campos — por isso esta etapa roda antes da carga dos campos.
 */
async function sincronizarVersao(api, versaoId, versaoLayoutId) {
  const { dados: versao } = await api.chamar('GET', `/interno/versoes/${versaoId}`)
  if (versao.introducao === INTRODUCAO_VERSAO && versao.versaoLayoutId === versaoLayoutId) return

  await api.chamar(
    'PUT',
    `/interno/versoes/${versaoId}`,
    { rotulo: versao.rotulo, introducao: INTRODUCAO_VERSAO, versaoLayoutId },
    String(versao.versaoConcorrencia),
  )
  console.log('versão alinhada: introdução e layout de origem')
}

async function garantirVersao(api, provedorId, versaoLayoutId) {
  const { dados } = await api.chamar('GET', `/interno/provedores/${provedorId}/versoes`)
  const existente = itensDe(dados).find((versao) => versao.rotulo === ROTULO_VERSAO)
  if (existente) {
    await sincronizarVersao(api, existente.id, versaoLayoutId)
    return existente.id
  }

  const { dados: criada } = await api.chamar('POST', `/interno/provedores/${provedorId}/versoes`, {
    versaoLayoutId,
    rotulo: ROTULO_VERSAO,
    introducao: INTRODUCAO_VERSAO,
  })
  console.log(`versão ${ROTULO_VERSAO} criada (id ${criada.id})`)
  return criada.id
}

function avisoMudou(existente, desejado) {
  return (
    existente.severidade !== desejado.severidade ||
    existente.corpo !== desejado.corpo ||
    (desejado.vigenciaInicio !== undefined && existente.vigenciaInicio !== desejado.vigenciaInicio)
  )
}

/** O título é a identidade do aviso para a sincronização: título novo = aviso novo. */
async function sincronizarAvisos(api, versaoId, avisos) {
  const { dados: versao } = await api.chamar('GET', `/interno/versoes/${versaoId}`)
  const existentes = new Map((versao.avisos ?? []).map((aviso) => [aviso.titulo, aviso]))
  const titulosDesejados = new Set(avisos.map((aviso) => aviso.titulo))
  const contagem = { criados: 0, atualizados: 0, removidos: 0 }

  for (const aviso of avisos) {
    const existente = existentes.get(aviso.titulo)
    if (!existente) {
      await api.chamar('POST', `/interno/versoes/${versaoId}/avisos`, {
        ...aviso,
        campoVersaoIds: [],
      })
      contagem.criados++
    } else if (avisoMudou(existente, aviso)) {
      await api.chamar(
        'PUT',
        `/interno/avisos/${existente.id}`,
        aviso,
        String(existente.versaoConcorrencia),
      )
      contagem.atualizados++
    }
  }

  for (const [titulo, existente] of existentes) {
    if (titulosDesejados.has(titulo)) continue
    await api.chamar(
      'DELETE',
      `/interno/avisos/${existente.id}`,
      undefined,
      String(existente.versaoConcorrencia),
    )
    contagem.removidos++
  }

  console.log(
    `avisos: ${contagem.criados} criados, ${contagem.atualizados} atualizados, ${contagem.removidos} removidos`,
  )
}

/**
 * Só considera os erros já vinculados a este provedor: código de erro não é único entre
 * provedores, e o mesmo código registrado para outro provedor não pode impedir a criação deste.
 */
async function sincronizarErros(api, provedorSlug, versaoId, erros) {
  const existentes = await api.listarTudo(
    `/interno/erros?provedor=${encodeURIComponent(provedorSlug)}`,
  )
  const codigosExistentes = new Set(existentes.map((erro) => erro.codigo))
  const codigosDesejados = new Set(erros.map((erro) => erro.codigo))

  const novos = erros.filter((erro) => !codigosExistentes.has(erro.codigo))
  for (const [indice, erro] of novos.entries()) {
    await api.chamar('POST', '/interno/erros', {
      ...erro,
      versoesProvedorIds: [versaoId],
      camposLayoutIds: [],
    })
    if ((indice + 1) % 50 === 0) console.log(`  erros: ${indice + 1}/${novos.length}`)
  }

  let removidos = 0
  for (const erro of existentes.filter((existente) => !codigosDesejados.has(existente.codigo))) {
    const { dados: detalhe } = await api.chamar('GET', `/interno/erros/${erro.id}`)
    const somenteDestaVersao =
      detalhe.versoesProvedorIds.length === 1 && detalhe.versoesProvedorIds[0] === versaoId

    // Desvincular exige PUT com as variantes, e o detalhe interno hoje não as devolve: regravar a
    // partir dele apagaria as mensagens. Erro compartilhado com outra versão fica para ajuste manual.
    if (!somenteDestaVersao) {
      console.warn(`  ${erro.codigo} também vale para outra versão — desvincule manualmente.`)
      continue
    }

    await api.chamar(
      'DELETE',
      `/interno/erros/${erro.id}`,
      undefined,
      String(detalhe.versaoConcorrencia),
    )
    removidos++
  }

  console.log(
    `erros: ${novos.length} criados, ${removidos} removidos, ${codigosExistentes.size - removidos} mantidos`,
  )
}

const pastaMaterial = process.argv[2]
if (!pastaMaterial) {
  console.error('Uso: node database/carga/emissor-nacional/importar.mjs <pasta-do-material>')
  process.exit(1)
}

const [rejeicoes, diferencas, leiaute, regras] = await Promise.all([
  lerJson(pastaMaterial, 'rejeicoes-emissao.json'),
  lerJson(pastaMaterial, 'diferencas-anexoI-v1.01.json'),
  lerJson(pastaMaterial, 'dps-leiaute.json'),
  lerJson(pastaMaterial, 'dps-regras.json'),
])
const [xsd, planilha, mapeamento] = await Promise.all([
  lerTexto('layout-invoicy-v8.xsd'),
  lerTexto('layout-invoicy-v8-planilha.json'),
  lerTexto('mapeamento-invoicy.csv'),
])

// Transformar tudo antes de tocar a API: um conteúdo fora do limite, ou um campo sem caminho
// resolvido, interrompe a carga sem deixá-la pela metade.
const erros = montarErros([...rejeicoes.rejeicoes, ...rejeicoesSomenteAnexoI(diferencas)], {
  codigosForaDaVersao: codigosSomenteRtc(diferencas),
})
const avisos = montarAvisos()

const elementosXsd = lerXsd(xsd)
const { tags, pendentes } = resolverMapeamento(lerMapeamento(mapeamento), elementosXsd)
const gruposUsados = new Set(
  tags.filter((tag) => tag.caminhoInvoicy).map((tag) => tag.caminhoInvoicy),
)
const vocabulario = montarVocabulario(
  elementosXsd,
  JSON.parse(planilha),
  gruposUsados,
  CAMINHOS_EM_DUVIDA,
)
const definicoes = indexarDefinicoes(
  leiaute.campos,
  diferencas,
  obrigatoriosNaNotaRegular(regras.regras),
)
const campos = montarCampos(tags, definicoes, notasDeComposicao(tags))

const api = await criarClienteApi()
const versaoLayoutId = await garantirLayoutInvoicy(api)
const provedor = await garantirProvedor(api)
const versaoId = await garantirVersao(api, provedor.id, versaoLayoutId)
await sincronizarAvisos(api, versaoId, avisos)
await sincronizarErros(api, provedor.slug, versaoId, erros)
const idPorCaminho = await sincronizarVocabulario(api, versaoLayoutId, vocabulario)
await sincronizarCamposDaVersao(api, versaoId, campos, idPorCaminho)

const errosDoProvedor = await api.listarTudo(
  `/interno/erros?provedor=${encodeURIComponent(provedor.slug)}`,
)
const idErroPorCodigo = new Map(errosDoProvedor.map((erro) => [erro.codigo, erro.id]))
for (const artigo of ARTIGOS) {
  const referencias = resolverReferencias(artigo, idPorCaminho, idErroPorCodigo)
  await sincronizarArtigo(api, artigo, await lerTexto(artigo.arquivo), referencias)
}

console.log(`\n${pendentes.size} campos do mapeamento esperam resposta do InvoiCy:`)
for (const [codigo, { nomeCampo, motivo }] of pendentes)
  console.log(`  ${codigo} ${nomeCampo} — ${motivo}`)
console.log('\ncarga concluída. Nada foi publicado.')
