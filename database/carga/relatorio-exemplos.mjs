/**
 * Relatório dos exemplos que a carga publicaria nos campos das versões. Roda sem API, sem banco e
 * sem variáveis: monta os mesmos campos que a carga monta e mostra, por provedor, o que vira
 * exemplo e o que fica sem, com o motivo em português.
 *
 * Uso:
 *   node database/carga/relatorio-exemplos.mjs [--completo] [--saida <arquivo>]
 *
 * `--completo` inclui as tags que não aparecem na fonte. `--saida` grava o mesmo texto em UTF-8.
 * O valor de um dado de identificação nunca é impresso.
 */

import { writeFile } from 'node:fs/promises'
import { pathToFileURL } from 'node:url'

import { PROVEDOR, ROTULO_VERSAO } from './emissor-nacional/identificacao.mjs'
import {
  lerMaterialEmissorNacional,
  prepararCamposEmissorNacional,
} from './emissor-nacional/preparar-campos.mjs'
import { FONTES_DE_EXEMPLO, MOTIVOS, lerFonteDeExemploDoDisco } from './exemplos-xml.mjs'
import { PROVEDORES } from './provedores/catalogo.mjs'
import { prepararCarga } from './provedores/preparar.mjs'
import { lerVocabularioInvoicy } from './vocabulario-invoicy.mjs'

export const ID_DO_EMISSOR_NACIONAL = 'emissor-nacional'

/** Os motivos que pedem decisão de quem lê vêm primeiro; os estruturais e a ausência na fonte, depois. */
export const ORDEM_DOS_MOTIVOS = [
  'IDENTIFICACAO',
  'MARCADOR_TESTE',
  'MARCADOR_LETRA_REPETIDA',
  'DESCARTE_EDITORIAL',
  'AMBIGUO',
  'DESTINOS_DIVERGEM',
  'FORA_DO_TIPO',
  'FORA_DO_DOMINIO',
  'ACIMA_DO_TETO',
  'FONTE_COM_VALOR_VAZIO',
  'ALTERNATIVA',
  'ATRIBUTO',
  'GRUPO',
  'ENVELOPE',
  'NAO_ESTA_NA_FONTE',
]

const TAG_SEM_DESTINO = '(sem tag de destino)'

const USO = 'Uso: node database/carga/relatorio-exemplos.mjs [--completo] [--saida <arquivo>]'

export class ErroDeUso extends Error {
  name = 'ErroDeUso'
}

function comparar(a, b) {
  if (a < b) return -1
  return a > b ? 1 : 0
}

/** Ordem do campo na versão; o caminho do campo desempata. */
function compararCampos(a, b) {
  return (
    a.campoVersao.ordem - b.campoVersao.ordem ||
    comparar(a.caminhoInvoicy ?? a.nomeCampo, b.caminhoInvoicy ?? b.nomeCampo)
  )
}

function exigirDecisao(campo) {
  if (!campo.decisaoDeExemplo) {
    throw new Error(
      `O campo ${campo.nomeCampo} não traz decisão de exemplo: a preparação foi feita sem fonte.`,
    )
  }
  return campo.decisaoDeExemplo
}

/** Tags do destino que levaram ao motivo; quando o motivo é do campo todo, todas as tags. */
function tagsDoMotivo(destinos, motivo, exibir) {
  const doMotivo = destinos.filter((destino) => destino.motivo === motivo)
  const escolhidos = doMotivo.length ? doMotivo : destinos
  if (!escolhidos.length) return TAG_SEM_DESTINO
  return escolhidos.map((destino) => exibir(destino.caminho)).join(' | ')
}

/**
 * Um provedor do relatório, a partir dos campos que a carga monta com a fonte de exemplo.
 * `prefixoDestino` é o envelope do lote, tirado da tag exibida. `caminhosDistintos` conta só os
 * caminhos do trecho que a fonte exemplifica.
 */
export function montarProvedorDoRelatorio({ id, nome, fonteLida, campos, prefixoDestino = '' }) {
  const exibir = (caminho) =>
    caminho.startsWith(prefixoDestino) ? caminho.slice(prefixoDestino.length) : caminho

  const aceitos = []
  const semExemplo = []
  for (const campo of [...campos].sort(compararCampos)) {
    const { exemplo, motivo, detalhe, destinos } = exigirDecisao(campo)
    if (exemplo !== null) {
      aceitos.push({
        campo: campo.nomeCampo,
        tag: destinos.map((destino) => exibir(destino.caminho)).join(' | '),
        valor: exemplo,
        ordem: campo.campoVersao.ordem,
      })
      continue
    }
    semExemplo.push({
      campo: campo.nomeCampo,
      tag: tagsDoMotivo(destinos, motivo, exibir),
      motivo,
      detalhe,
    })
  }

  return {
    id,
    nome,
    fonteId: fonteLida.id,
    arquivos: FONTES_DE_EXEMPLO[fonteLida.id].arquivos.length,
    folhasLidas: fonteLida.folhasLidas,
    caminhosDistintos: fonteLida.valoresPorCaminho.size,
    foraDoEnvelope: fonteLida.foraDoEnvelope,
    total: campos.length,
    aceitos,
    semExemplo,
  }
}

/** Monta o relatório do Emissor Nacional e dos provedores do catálogo que têm fonte de exemplo. */
export async function montarRelatorioDeExemplos() {
  const { elementos } = await lerVocabularioInvoicy()

  const fonteNacional = await lerFonteDeExemploDoDisco('dps-nacional')
  const { campos } = prepararCamposEmissorNacional(
    await lerMaterialEmissorNacional(),
    elementos,
    fonteNacional,
  )
  const provedores = [
    montarProvedorDoRelatorio({
      id: ID_DO_EMISSOR_NACIONAL,
      nome: `${PROVEDOR.nome} ${ROTULO_VERSAO}`,
      fonteLida: fonteNacional,
      campos,
    }),
  ]

  const idsDoCatalogo = Object.keys(PROVEDORES)
  for (const id of idsDoCatalogo.filter(
    (idDoCatalogo) => PROVEDORES[idDoCatalogo].fonteDeExemplo,
  )) {
    const carga = await prepararCarga(id, elementos)
    provedores.push(
      montarProvedorDoRelatorio({
        id,
        nome: `${carga.config.provedor.nome} ${carga.config.versao.rotulo}`,
        fonteLida: carga.fonteLida,
        campos: carga.campos,
        prefixoDestino: carga.config.prefixoDestino ?? '',
      }),
    )
  }

  const naoGerenciados = idsDoCatalogo.filter((id) => !PROVEDORES[id].fonteDeExemplo)
  return { provedores, naoGerenciados }
}

/** Quantos campos de cada provedor recebem exemplo: o número que o usuário aprova. */
export function contagemDeExemplos(relatorio) {
  return Object.fromEntries(
    relatorio.provedores.map((provedor) => [provedor.id, provedor.aceitos.length]),
  )
}

function contarPorMotivo(semExemplo) {
  const contagem = new Map()
  for (const { motivo } of semExemplo) contagem.set(motivo, (contagem.get(motivo) ?? 0) + 1)
  return contagem
}

/** O valor de identificação nunca sai: o detalhe só é mostrado para os outros motivos. */
function detalheVisivel({ motivo, detalhe }) {
  return motivo === 'IDENTIFICACAO' ? null : detalhe
}

/** Descartes de um motivo. O detalhe que todos repetem vai uma vez, abaixo do título. */
function linhasDoGrupo(motivo, descartes) {
  const quantidade = `${descartes.length} ${descartes.length === 1 ? 'campo' : 'campos'}`
  const detalhes = new Set(descartes.map(detalheVisivel))
  const [detalheComum] = detalhes
  const comumAoGrupo = descartes.length > 1 && detalhes.size === 1 && detalheComum !== null

  return [
    `  ${MOTIVOS[motivo]} (${quantidade}):`,
    ...(comumAoGrupo ? [`    Em todos: ${detalheComum}`] : []),
    ...descartes.map((descarte) => {
      const detalhe = comumAoGrupo ? null : detalheVisivel(descarte)
      return `    ${[descarte.campo, descarte.tag, detalhe].filter(Boolean).join(' · ')}`
    }),
  ]
}

function linhasDoDetalhamento(provedor, completo) {
  const motivosListados = ORDEM_DOS_MOTIVOS.filter(
    (motivo) => completo || motivo !== 'NAO_ESTA_NA_FONTE',
  )
  const linhas = motivosListados.flatMap((motivo) => {
    const descartes = provedor.semExemplo.filter((descarte) => descarte.motivo === motivo)
    return descartes.length ? linhasDoGrupo(motivo, descartes) : []
  })
  return linhas.length ? linhas : ['  (nenhum)']
}

function linhasDoProvedor(provedor, completo) {
  const contagem = contarPorMotivo(provedor.semExemplo)
  const motivosComDescarte = ORDEM_DOS_MOTIVOS.filter((motivo) => contagem.has(motivo))
  const aceitos = provedor.aceitos.map(({ campo, tag, valor }) => `  ${campo} · ${tag} · ${valor}`)
  const titulo = completo
    ? 'descartes que merecem leitura (inclui os que não aparecem na fonte):'
    : `descartes que merecem leitura (todos, menos "${MOTIVOS.NAO_ESTA_NA_FONTE}"):`

  return [
    `== ${provedor.nome} (fonte: ${provedor.fonteId}) ==`,
    `fonte: ${provedor.arquivos} ${provedor.arquivos === 1 ? 'arquivo' : 'arquivos'} · ` +
      `${provedor.folhasLidas} folhas lidas (${provedor.caminhosDistintos} caminhos distintos) · ` +
      `${provedor.foraDoEnvelope} fora do envelope`,
    `campos: ${provedor.total} · com exemplo: ${provedor.aceitos.length} · ` +
      `sem exemplo: ${provedor.semExemplo.length}`,
    '',
    'exemplos atribuídos (ordem do campo):',
    ...(aceitos.length ? aceitos : ['  (nenhum)']),
    '',
    'sem exemplo, por motivo:',
    ...(motivosComDescarte.length
      ? motivosComDescarte.map((motivo) => `  ${MOTIVOS[motivo]}: ${contagem.get(motivo)}`)
      : ['  (nenhum)']),
    '',
    titulo,
    ...linhasDoDetalhamento(provedor, completo),
  ]
}

export function formatarRelatorio(relatorio, { completo = false } = {}) {
  const contagem = Object.entries(contagemDeExemplos(relatorio))
    .map(([id, quantidade]) => `${id}=${quantidade}`)
    .join(' ')

  return [
    ...relatorio.provedores.flatMap((provedor) => [...linhasDoProvedor(provedor, completo), '']),
    `não gerenciados (sem fonte de exemplo): ${relatorio.naoGerenciados.join(', ') || '(nenhum)'}`,
    `CONTAGEM_APROVADA_M2 ${contagem}`,
  ].join('\n')
}

export function lerArgumentos(argumentos) {
  let completo = false
  let saida = null
  for (let posicao = 0; posicao < argumentos.length; posicao++) {
    const argumento = argumentos[posicao]
    if (argumento === '--completo') {
      completo = true
    } else if (argumento === '--saida') {
      saida = argumentos[++posicao]
      if (!saida || saida.startsWith('--')) {
        throw new ErroDeUso(`--saida exige o caminho do arquivo.\n${USO}`)
      }
    } else {
      throw new ErroDeUso(`Argumento desconhecido: ${argumento}.\n${USO}`)
    }
  }
  return { completo, saida }
}

/** Imprime o relatório e, com `--saida`, grava o mesmo texto em UTF-8. */
export async function executarRelatorio({
  argumentos,
  montar = montarRelatorioDeExemplos,
  imprimir = console.log,
  gravar = writeFile,
}) {
  const { completo, saida } = lerArgumentos(argumentos)
  const texto = formatarRelatorio(await montar(), { completo })
  imprimir(texto)
  if (saida) await gravar(saida, `${texto}\n`, 'utf8')
  return texto
}

const ehExecucaoDireta = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href

if (ehExecucaoDireta) {
  try {
    await executarRelatorio({ argumentos: process.argv.slice(2) })
  } catch (erro) {
    if (!(erro instanceof ErroDeUso)) throw erro
    console.error(erro.message)
    process.exitCode = 1
  }
}
