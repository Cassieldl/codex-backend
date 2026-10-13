/**
 * Prepara os campos da versão 0.20 do Emissor Nacional: lê o material da Sefin e o mapeamento do
 * InvoiCy e monta as tags e os campos. Fica fora de importar.mjs, que fala com a API ao ser
 * carregado, para que o relatório de exemplos e os testes alcancem a montagem.
 */

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { exigirGruposConhecidos } from '../vocabulario-invoicy.mjs'
import { notasDeComposicao, resolverMapeamento } from './resolver-mapeamento.mjs'
import {
  indexarDefinicoes,
  lerMapeamento,
  montarCampos,
  obrigatoriosNaNotaRegular,
} from './transformar-campos.mjs'

const PASTA_DA_CARGA = dirname(fileURLToPath(import.meta.url))

export const PASTA_DO_MATERIAL_SEFIN = join(PASTA_DA_CARGA, 'material-sefin')

async function lerJson(pasta, arquivo) {
  return JSON.parse(await readFile(join(pasta, arquivo), 'utf8'))
}

/**
 * Lê do disco o material da Sefin (da pasta informada, por padrão a versionada) e o mapeamento do
 * InvoiCy, que mora ao lado deste arquivo.
 */
export async function lerMaterialEmissorNacional(pastaMaterial = PASTA_DO_MATERIAL_SEFIN) {
  const [rejeicoes, diferencas, leiaute, regras] = await Promise.all([
    lerJson(pastaMaterial, 'rejeicoes-emissao.json'),
    lerJson(pastaMaterial, 'diferencas-anexoI-v1.01.json'),
    lerJson(pastaMaterial, 'dps-leiaute.json'),
    lerJson(pastaMaterial, 'dps-regras.json'),
  ])
  const mapeamento = await readFile(join(PASTA_DA_CARGA, 'mapeamento-invoicy.csv'), 'utf8')
  return { rejeicoes, diferencas, leiaute, regras, mapeamento }
}

/**
 * Tags resolvidas no XSD do InvoiCy, campos do mapeamento que esperam resposta do InvoiCy
 * (`pendentes`) e os campos da versão. Sem `fonteLida` os campos não trazem a chave `exemplo`.
 * Um campo sem caminho resolvido, ou um grupo mapeado fora do vocabulário, interrompe a montagem.
 */
export function prepararCamposEmissorNacional(material, elementosXsd, fonteLida = null) {
  const { tags, pendentes } = resolverMapeamento(lerMapeamento(material.mapeamento), elementosXsd)
  exigirGruposConhecidos(tags.map((tag) => tag.caminhoInvoicy).filter(Boolean), elementosXsd)
  const definicoes = indexarDefinicoes(
    material.leiaute.campos,
    material.diferencas,
    obrigatoriosNaNotaRegular(material.regras.regras),
  )
  const campos = montarCampos(tags, definicoes, notasDeComposicao(tags), fonteLida)
  return { tags, pendentes, campos }
}
