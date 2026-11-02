/**
 * Publica o que a carga do Emissor Nacional criou e registra a verificação da versão. Separado da
 * carga de propósito: carregar é repetível e não muda o que o leitor vê; publicar e dar o selo de
 * verificação são decisões editoriais, tomadas uma vez e com data.
 *
 *   node database/carga/emissor-nacional/publicar.mjs --verificado-em 2026-10-06
 *
 * A data é obrigatória e explícita: o selo afirma que alguém conferiu o conteúdo naquele dia, e uma
 * data presumida pelo script seria uma afirmação que ninguém fez. Idempotente: o que já está
 * publicado não é tocado, e a verificação só é regravada se a data mudar.
 */

import { criarClienteApi } from '../cliente-api.mjs'
import { ROTULO_LAYOUT_DOS_PROVEDORES } from '../layout-invoicy.mjs'
import { ARTIGOS, PROVEDOR, ROTULO_VERSAO } from './identificacao.mjs'
import { lerDataDeVerificacao, pendentesDePublicacao } from './publicacao.mjs'

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

async function localizar(api) {
  const { dados: versoesLayout } = await api.chamar('GET', '/interno/layout/versoes')
  const layout = itensDe(versoesLayout).find(
    (versao) => versao.rotulo === ROTULO_LAYOUT_DOS_PROVEDORES,
  )

  const provedores = await api.listarTudo(
    `/interno/provedores?q=${encodeURIComponent(PROVEDOR.nome)}`,
  )
  const provedor = provedores.find((item) => item.nome === PROVEDOR.nome)

  if (!layout || !provedor) {
    throw new Error('Layout InvoiCy ou provedor não encontrado: rode a carga (importar.mjs) antes.')
  }

  const { dados: versoes } = await api.chamar('GET', `/interno/provedores/${provedor.id}/versoes`)
  const versao = itensDe(versoes).find((item) => item.rotulo === ROTULO_VERSAO)
  if (!versao) throw new Error(`Versão ${ROTULO_VERSAO} não encontrada: rode a carga antes.`)

  return { layout, provedor, versao }
}

async function registrarVerificacao(api, versaoId, verificadoEm) {
  const { dados: versao } = await api.chamar('GET', `/interno/versoes/${versaoId}`)
  if (versao.verificadoEm === verificadoEm) {
    console.log(`verificação: já registrada em ${verificadoEm}`)
    return versao
  }

  const { dados: atualizada } = await api.chamar(
    'PUT',
    `/interno/versoes/${versaoId}/verificacao`,
    { verificadoEm },
    String(versao.versaoConcorrencia),
  )
  console.log(`verificação: registrada em ${verificadoEm}`)
  return atualizada
}

async function publicarTudo(api, verificadoEm) {
  const { layout, provedor, versao } = await localizar(api)

  await publicarTodos(api, '/interno/layout/versoes', [layout], 'versão do Layout InvoiCy')

  const { dados: camposLayout } = await api.chamar(
    'GET',
    `/interno/layout/versoes/${layout.id}/campos`,
  )
  await publicarTodos(api, '/interno/campos-layout', camposLayout, 'campos do Layout InvoiCy')

  await publicarTodos(api, '/interno/provedores', [provedor], 'provedor')

  const versaoVerificada = await registrarVerificacao(api, versao.id, verificadoEm)
  await publicarTodos(api, '/interno/versoes', [versaoVerificada], `versão ${ROTULO_VERSAO}`)

  const { dados: detalheVersao } = await api.chamar('GET', `/interno/versoes/${versao.id}`)
  await publicarTodos(api, '/interno/avisos', detalheVersao.avisos, 'avisos da versão')

  const erros = await api.listarTudo(`/interno/erros?provedor=${encodeURIComponent(provedor.slug)}`)
  await publicarTodos(api, '/interno/erros', erros, 'erros do provedor')

  const titulos = new Set(ARTIGOS.map((artigo) => artigo.titulo))
  const artigos = (await api.listarTudo('/interno/artigos')).filter((artigo) =>
    titulos.has(artigo.titulo),
  )
  await publicarTodos(api, '/interno/artigos', artigos, 'artigos do provedor')
}

const verificadoEm = lerDataDeVerificacao(process.argv.slice(2))
await publicarTudo(await criarClienteApi(), verificadoEm)
console.log('\npublicação concluída.')
