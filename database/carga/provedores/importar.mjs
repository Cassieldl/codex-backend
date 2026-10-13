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
 */

import { criarClienteApi, executarCarga } from '../cliente-api.mjs'
import { LAYOUT_INVOICY, ESTADO_LAYOUT_INVOICY } from '../emissor-nacional/identificacao.mjs'
import {
  sincronizarCamposDaVersao,
  sincronizarVocabulario,
} from '../emissor-nacional/sincronizar-campos.mjs'
import {
  garantirLayoutInvoicy,
  garantirProvedor,
  garantirVersao,
  sincronizarAvisos,
  sincronizarErros,
} from '../sincronizar-provedor.mjs'
import { lerVocabularioInvoicy } from '../vocabulario-invoicy.mjs'
import { PROVEDORES } from './catalogo.mjs'
import { prepararCarga } from './preparar.mjs'

const argumentos = process.argv.slice(2)
const ids = argumentos.includes('--todos') ? Object.keys(PROVEDORES) : argumentos
if (!ids.length) {
  console.error(
    `Uso: node database/carga/provedores/importar.mjs <id>... | --todos\nProvedores: ${Object.keys(PROVEDORES).join(', ')}`,
  )
  process.exit(1)
}

const { elementos, vocabulario } = await lerVocabularioInvoicy()
// Todos preparados antes da primeira chamada: um material com problema não deixa carga pela metade.
const cargas = []
for (const id of ids) cargas.push({ id, ...(await prepararCarga(id, elementos)) })

const api = await criarClienteApi()

await executarCarga(async () => {
  const versaoLayoutId = await garantirLayoutInvoicy(api, LAYOUT_INVOICY, ESTADO_LAYOUT_INVOICY)
  const idPorCaminho = await sincronizarVocabulario(api, versaoLayoutId, vocabulario)

  for (const { id, config, campos, erros, avisos, pendentes, internos } of cargas) {
    console.log(`\n== ${config.provedor.nome} (${id}) ==`)
    const provedor = await garantirProvedor(api, config.provedor)
    const versaoId = await garantirVersao(api, provedor.id, versaoLayoutId, config.versao)
    await sincronizarAvisos(api, versaoId, avisos)
    await sincronizarCamposDaVersao(api, versaoId, campos, idPorCaminho)
    await sincronizarErros(api, provedor.slug, versaoId, erros, idPorCaminho)

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
