/**
 * Carga do Layout InvoiCy: versões, estados, vocabulário e estrutura de todas as versões. As cargas de provedor fazem o mesmo antes dos próprios campos; este script serve para
 * atualizar o layout sem tocar em provedor.
 *
 *   CODEX_DEV_USUARIO=... CODEX_DEV_SENHA=... node database/carga/importar-layout-invoicy.mjs
 *
 * Nada é publicado aqui. Com CODEX_CARGA_SIMULAR=1 só lê a API.
 */

import { criarClienteApi, executarCarga } from './cliente-api.mjs'
import { sincronizarLayoutInvoicy } from './layout-invoicy.mjs'

const api = await criarClienteApi()

await executarCarga(async () => {
  await sincronizarLayoutInvoicy(api)

  api.imprimirResumoDaSimulacao()
  console.log(
    api.simulando
      ? '\nsimulação concluída. Nada foi gravado.'
      : '\ncarga concluída. Nada foi publicado.',
  )
})
