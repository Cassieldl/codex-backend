/**
 * Carga dos provedores do catálogo (catalogo.mjs) a partir do material versionado em `material/`.
 *
 * Uso, com a API no ar:
 *   CODEX_DEV_USUARIO=... CODEX_DEV_SENHA=... node database/carga/provedores/importar.mjs <id>... | --todos
 *
 * Sincroniza, em vez de só criar, e pode rodar de novo a qualquer momento. Nada é publicado aqui:
 * publicar é decisão editorial.
 *
 * Com CODEX_CARGA_SIMULAR=1 só lê a API: imprime o que escreveria e interrompe no primeiro POST ou
 * DELETE (exit 1).
 *
 * Com --layout-carregado o Layout InvoiCy não é sincronizado: a carga só lê a versão dos provedores
 * que já está no banco. Serve para carregar provedores enquanto o layout tem trabalho em andamento.
 */

import { criarClienteApi, executarCarga } from '../cliente-api.mjs'
import { sincronizarCamposDaVersao } from '../emissor-nacional/sincronizar-campos.mjs'
import { ROTULO_LAYOUT_DOS_PROVEDORES, sincronizarLayoutInvoicy } from '../layout-invoicy.mjs'
import {
  garantirProvedor,
  garantirVersao,
  sincronizarAvisos,
  sincronizarErros,
  sincronizarNaturezasOperacao,
} from '../sincronizar-provedor.mjs'
import { lerVocabularioInvoicy } from '../vocabulario-invoicy.mjs'
import { PROVEDORES } from './catalogo.mjs'
import { prepararCarga } from './preparar.mjs'

const OPCAO_LAYOUT_CARREGADO = '--layout-carregado'
const argumentos = process.argv.slice(2)
const comLayoutCarregado = argumentos.includes(OPCAO_LAYOUT_CARREGADO)
const ids = argumentos.includes('--todos')
  ? Object.keys(PROVEDORES)
  : argumentos.filter((argumento) => argumento !== OPCAO_LAYOUT_CARREGADO)
if (!ids.length) {
  console.error(
    `Uso: node database/carga/provedores/importar.mjs <id>... | --todos [${OPCAO_LAYOUT_CARREGADO}]\nProvedores: ${Object.keys(PROVEDORES).join(', ')}`,
  )
  process.exit(1)
}

const { elementos, vocabulario } = await lerVocabularioInvoicy(ROTULO_LAYOUT_DOS_PROVEDORES)
// Todos preparados antes da primeira chamada: um material com problema não deixa carga pela metade.
const cargas = []
for (const id of ids) cargas.push({ id, ...(await prepararCarga(id, elementos)) })

const api = await criarClienteApi()

/** Versão do layout dos provedores e caminho completo para id dos campos dela, só lidos da API. */
async function lerLayoutCarregado() {
  const { dados } = await api.chamar('GET', '/interno/layout/versoes?tamanhoPagina=200')
  const versao = (dados.items ?? dados).find((item) => item.rotulo === ROTULO_LAYOUT_DOS_PROVEDORES)
  if (!versao)
    throw new Error(`A versão ${ROTULO_LAYOUT_DOS_PROVEDORES} do layout não está no banco.`)

  const { dados: campos } = await api.chamar('GET', `/interno/layout/versoes/${versao.id}/campos`)
  const idPorCaminhoXml = new Map(campos.map((campo) => [campo.caminhoXml, campo.id]))
  const faltando = vocabulario.filter((campo) => !idPorCaminhoXml.has(campo.caminhoXml))
  if (faltando.length)
    throw new Error(
      `O layout no banco não tem ${faltando.length} campos do vocabulário; rode sem ${OPCAO_LAYOUT_CARREGADO}.`,
    )
  const idPorCaminho = new Map(
    vocabulario.map((campo) => [campo.caminhoCompleto, idPorCaminhoXml.get(campo.caminhoXml)]),
  )
  return { versaoLayoutId: versao.id, idPorCaminho }
}

async function sincronizarLayoutDosProvedores() {
  const layouts = await sincronizarLayoutInvoicy(api)
  return {
    versaoLayoutId: layouts.idPorRotulo.get(ROTULO_LAYOUT_DOS_PROVEDORES),
    idPorCaminho: layouts.idPorCaminhoPorRotulo.get(ROTULO_LAYOUT_DOS_PROVEDORES),
  }
}

await executarCarga(async () => {
  const { versaoLayoutId, idPorCaminho } = comLayoutCarregado
    ? await lerLayoutCarregado()
    : await sincronizarLayoutDosProvedores()

  for (const {
    id,
    config,
    campos,
    erros,
    removerErrosAusentes,
    avisos,
    naturezasOperacao,
    pendentes,
    internos,
  } of cargas) {
    console.log(`\n== ${config.provedor.nome} (${id}) ==`)
    const provedor = await garantirProvedor(api, config.provedor)
    const versaoId = await garantirVersao(api, provedor.id, versaoLayoutId, config.versao)
    await sincronizarAvisos(api, versaoId, avisos)
    if (naturezasOperacao) await sincronizarNaturezasOperacao(api, versaoId, naturezasOperacao)
    await sincronizarCamposDaVersao(api, versaoId, campos, idPorCaminho)
    await sincronizarErros(api, provedor.slug, versaoId, erros, idPorCaminho, {
      removerAusentes: removerErrosAusentes,
    })

    console.log(`${pendentes.size} campos esperam o InvoiCy: ${[...pendentes.keys()].join(', ')}`)
    console.log(
      `campos internos do InvoiCy, fora da tabela: ${[...internos].join(', ') || 'nenhum'}`,
    )
  }

  api.imprimirResumoDaSimulacao()
  console.log(
    api.simulando
      ? '\nsimulação concluída. Nada foi gravado.'
      : '\ncarga concluída. Nada foi publicado.',
  )
})
