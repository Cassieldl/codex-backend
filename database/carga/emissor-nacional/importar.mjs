/**
 * Carga do provedor Emissor Nacional a partir do material coletado da Sefin Nacional.
 *
 * Uso, com a API no ar:
 *   CODEX_DEV_USUARIO=... CODEX_DEV_SENHA=... node database/carga/emissor-nacional/importar.mjs <pasta-do-material>
 *
 * Sincroniza, em vez de só criar: cria o que falta, corrige o texto do que mudou e remove o que
 * deixou de valer para a versão — pode rodar de novo a qualquer momento. Nada é publicado aqui:
 * publicar é decisão editorial, feita por publicar.mjs.
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
  garantirLayoutInvoicy,
  garantirProvedor,
  garantirVersao,
  sincronizarAvisos,
  sincronizarErros,
} from '../sincronizar-provedor.mjs'
import { exigirGruposConhecidos, lerVocabularioInvoicy } from '../vocabulario-invoicy.mjs'
import { notasDeComposicao, resolverMapeamento } from './resolver-mapeamento.mjs'
import { resolverReferencias, sincronizarArtigo } from './sincronizar-artigos.mjs'
import { sincronizarCamposDaVersao, sincronizarVocabulario } from './sincronizar-campos.mjs'
import {
  INTRODUCAO_VERSAO,
  codigosSomenteRtc,
  indexarCamposPorCaminhoDps,
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
import {
  ARTIGOS,
  ESTADO_LAYOUT_INVOICY,
  LAYOUT_INVOICY,
  PROVEDOR,
  ROTULO_VERSAO,
} from './identificacao.mjs'

const PASTA_DA_CARGA = dirname(fileURLToPath(import.meta.url))

async function lerJson(pasta, arquivo) {
  return JSON.parse(await readFile(join(pasta, arquivo), 'utf8'))
}

async function lerTexto(arquivo) {
  return readFile(join(PASTA_DA_CARGA, arquivo), 'utf8')
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
const mapeamento = await lerTexto('mapeamento-invoicy.csv')
const { elementos: elementosXsd, vocabulario } = await lerVocabularioInvoicy()

// Transformar tudo antes de tocar a API: um conteúdo fora do limite, ou um campo sem caminho
// resolvido, interrompe a carga sem deixá-la pela metade.
const avisos = montarAvisos()

const { tags, pendentes } = resolverMapeamento(lerMapeamento(mapeamento), elementosXsd)
exigirGruposConhecidos(tags.map((tag) => tag.caminhoInvoicy).filter(Boolean), elementosXsd)
const definicoes = indexarDefinicoes(
  leiaute.campos,
  diferencas,
  obrigatoriosNaNotaRegular(regras.regras),
)
const campos = montarCampos(tags, definicoes, notasDeComposicao(tags))
const erros = montarErros([...rejeicoes.rejeicoes, ...rejeicoesSomenteAnexoI(diferencas)], {
  codigosForaDaVersao: codigosSomenteRtc(diferencas),
  camposPorCaminhoDps: indexarCamposPorCaminhoDps(tags, vocabulario),
})

const api = await criarClienteApi()
const versaoLayoutId = await garantirLayoutInvoicy(api, LAYOUT_INVOICY, ESTADO_LAYOUT_INVOICY)
const provedor = await garantirProvedor(api, PROVEDOR)
const versaoId = await garantirVersao(api, provedor.id, versaoLayoutId, {
  rotulo: ROTULO_VERSAO,
  introducao: INTRODUCAO_VERSAO,
})
await sincronizarAvisos(api, versaoId, avisos)
const idPorCaminho = await sincronizarVocabulario(api, versaoLayoutId, vocabulario)
await sincronizarCamposDaVersao(api, versaoId, campos, idPorCaminho)
// Depois do vocabulário: o vínculo do erro com o campo precisa do id do campo canônico.
await sincronizarErros(api, provedor.slug, versaoId, erros, idPorCaminho)

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
