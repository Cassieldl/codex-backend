/**
 * Publica o que a carga dos provedores do catálogo criou. Separado da carga de propósito: carregar é
 * repetível e não muda o que o leitor vê; publicar é decisão editorial.
 *
 *   node database/carga/provedores/publicar.mjs <id>... | --todos [--verificado-em AAAA-MM-DD]
 *
 * Sem `--verificado-em`, a versão vai ao ar como "ainda não verificada": o selo afirma que alguém
 * conferiu o conteúdo naquele dia, e quem afirma é o editor, não o script. Idempotente: o que já está
 * publicado não é tocado.
 */

import { criarClienteApi } from '../cliente-api.mjs'
import { ROTULO_LAYOUT_DOS_PROVEDORES } from '../layout-invoicy.mjs'
import { lerDataDeVerificacao, pendentesDePublicacao } from '../emissor-nacional/publicacao.mjs'
import { PROVEDORES } from './catalogo.mjs'

function itensDe(dados) {
  return dados.items ?? dados
}

async function publicarTodos(api, rotaBase, itens, descricao) {
  const pendentes = pendentesDePublicacao(itens)
  for (const item of pendentes) {
    await api.publicar(rotaBase, item.id, String(item.versaoConcorrencia))
  }
  console.log(
    `${descricao}: ${pendentes.length} publicados, ${itens.length - pendentes.length} já estavam`,
  )
}

/** Os campos canônicos novos (grupos de lista) entram junto com o primeiro provedor que os usa. */
async function publicarLayoutInvoicy(api) {
  const { dados: versoesLayout } = await api.chamar('GET', '/interno/layout/versoes')
  const layout = itensDe(versoesLayout).find(
    (versao) => versao.rotulo === ROTULO_LAYOUT_DOS_PROVEDORES,
  )
  if (!layout) throw new Error('Layout InvoiCy não encontrado: rode a carga (importar.mjs) antes.')

  await publicarTodos(api, '/interno/layout/versoes', [layout], 'versão do Layout InvoiCy')
  const { dados: campos } = await api.chamar('GET', `/interno/layout/versoes/${layout.id}/campos`)
  await publicarTodos(api, '/interno/campos-layout', campos, 'campos do Layout InvoiCy')
}

async function registrarVerificacao(api, versaoId, verificadoEm) {
  const { dados: versao } = await api.chamar('GET', `/interno/versoes/${versaoId}`)
  if (!verificadoEm || versao.verificadoEm === verificadoEm) return versao

  const { dados: atualizada } = await api.chamar(
    'PUT',
    `/interno/versoes/${versaoId}/verificacao`,
    { verificadoEm },
    String(versao.versaoConcorrencia),
  )
  console.log(`verificação: registrada em ${verificadoEm}`)
  return atualizada
}

async function publicarProvedor(api, config, verificadoEm) {
  const provedores = await api.listarTudo(
    `/interno/provedores?q=${encodeURIComponent(config.provedor.nome)}`,
  )
  const provedor = provedores.find((item) => item.nome === config.provedor.nome)
  if (!provedor) throw new Error(`${config.provedor.nome} não encontrado: rode a carga antes.`)

  const { dados: versoes } = await api.chamar('GET', `/interno/provedores/${provedor.id}/versoes`)
  const versao = itensDe(versoes).find((item) => item.rotulo === config.versao.rotulo)
  if (!versao) throw new Error(`Versão ${config.versao.rotulo} não encontrada: rode a carga antes.`)

  await publicarTodos(api, '/interno/provedores', [provedor], 'provedor')
  const versaoAtual = await registrarVerificacao(api, versao.id, verificadoEm)
  await publicarTodos(api, '/interno/versoes', [versaoAtual], `versão ${config.versao.rotulo}`)

  const { dados: detalhe } = await api.chamar('GET', `/interno/versoes/${versao.id}`)
  await publicarTodos(api, '/interno/avisos', detalhe.avisos, 'avisos da versão')

  const erros = await api.listarTudo(`/interno/erros?provedor=${encodeURIComponent(provedor.slug)}`)
  await publicarTodos(api, '/interno/erros', erros, 'erros do provedor')
}

const argumentos = process.argv.slice(2)
const verificadoEm = argumentos.includes('--verificado-em')
  ? lerDataDeVerificacao(argumentos)
  : null
const ids = argumentos.includes('--todos')
  ? Object.keys(PROVEDORES)
  : argumentos.filter((argumento, indice) => {
      const anterior = argumentos[indice - 1]
      return !argumento.startsWith('--') && anterior !== '--verificado-em'
    })
if (!ids.length) {
  console.error(
    'Uso: node database/carga/provedores/publicar.mjs <id>... | --todos [--verificado-em AAAA-MM-DD]',
  )
  process.exit(1)
}
for (const id of ids) if (!PROVEDORES[id]) throw new Error(`Provedor ${id} não está no catálogo.`)

const api = await criarClienteApi()
await publicarLayoutInvoicy(api)
for (const id of ids) {
  console.log(`\n== ${PROVEDORES[id].provedor.nome} ==`)
  await publicarProvedor(api, PROVEDORES[id], verificadoEm)
}
console.log('\npublicação concluída.')
