/**
 * Publica as versões do Layout InvoiCy da configuração e os campos canônicos de cada uma. Separado
 * da carga de propósito: publicar é decisão editorial. Idempotente: o que já está publicado não é
 * tocado.
 *
 *   node database/carga/publicar-layout-invoicy.mjs
 */

import { criarClienteApi } from './cliente-api.mjs'
import { pendentesDePublicacao } from './emissor-nacional/publicacao.mjs'
import { VERSOES_LAYOUT_INVOICY } from './layout-invoicy.mjs'

async function publicarTodos(api, rotaBase, itens, descricao) {
  const pendentes = pendentesDePublicacao(itens)
  for (const item of pendentes)
    await api.publicar(rotaBase, item.id, String(item.versaoConcorrencia))
  console.log(
    `${descricao}: ${pendentes.length} publicados, ${itens.length - pendentes.length} já estavam`,
  )
}

const api = await criarClienteApi()
const { dados } = await api.chamar('GET', '/interno/layout/versoes?tamanhoPagina=200')
const existentes = dados.items ?? dados

for (const { rotulo } of VERSOES_LAYOUT_INVOICY) {
  const versao = existentes.find((item) => item.rotulo === rotulo)
  if (!versao)
    throw new Error(`Versão ${rotulo} do Layout InvoiCy não encontrada: rode a carga antes.`)

  await publicarTodos(
    api,
    '/interno/layout/versoes',
    [versao],
    `versão ${rotulo} do Layout InvoiCy`,
  )
  const { dados: campos } = await api.chamar('GET', `/interno/layout/versoes/${versao.id}/campos`)
  await publicarTodos(
    api,
    '/interno/campos-layout',
    campos,
    `campos com origem na versão ${rotulo}`,
  )
}

console.log('\npublicação concluída.')
