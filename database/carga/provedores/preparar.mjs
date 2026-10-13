/**
 * Prepara a carga de um provedor do catálogo sem tocar a API: lê o material, resolve os campos e
 * monta campos, erros e avisos. Um conteúdo fora do limite, um campo sem caminho resolvido ou uma
 * fonte de exemplo ausente interrompe aqui — a carga nunca fica pela metade. `fonteLida` é `null`
 * para o provedor sem fonte de exemplo.
 */

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { LIMITE_CORPO_AVISO } from '../emissor-nacional/transformar.mjs'
import { lerFonteDeExemploDoDisco } from '../exemplos-xml.mjs'
import { exigirGruposConhecidos } from '../vocabulario-invoicy.mjs'
import { avisoDeCamposPendentes } from './avisos.mjs'
import { PROVEDORES } from './catalogo.mjs'
import { lerMapeamento } from './ler-mapeamento.mjs'
import { comPrefixo, definicoesPorCaminho, indexarEsquema } from './ler-xsd.mjs'
import { definicoesDeLeiauteTranscrito } from './leiaute-transcrito.mjs'
import { montarCampos } from './montar-campos.mjs'
import { montarErros } from './montar-erros.mjs'
import { resolverCampos } from './resolver-campos.mjs'

const PASTA_MATERIAL = join(dirname(fileURLToPath(import.meta.url)), 'material')

function lerMaterial(caminho) {
  return readFile(join(PASTA_MATERIAL, caminho), 'utf8')
}

/** Comportamento das tags: do XSD do provedor, da tabela transcrita do manual ou de lugar nenhum. */
async function definicoesDe(config, tags) {
  if (config.esquema) {
    const textos = await Promise.all(config.esquema.arquivos.map(lerMaterial))
    const definicoes = definicoesPorCaminho(indexarEsquema(textos), config.esquema.raiz)
    return comPrefixo(definicoes, config.esquema.prefixo)
  }
  if (config.leiauteTranscrito) {
    const transcricao = JSON.parse(await lerMaterial(config.leiauteTranscrito))
    return definicoesDeLeiauteTranscrito(transcricao, tags)
  }
  return new Map()
}

export async function prepararCarga(id, elementosXsd) {
  const config = PROVEDORES[id]
  if (!config) throw new Error(`Provedor ${id} não está no catálogo.`)

  const { tags, pendentes, internos } = resolverCampos(
    lerMapeamento(await lerMaterial(config.mapeamento)),
    elementosXsd,
    config.familia,
  )
  exigirGruposConhecidos(
    tags.flatMap((tag) => tag.campos.map((campo) => campo.caminhoInvoicy)),
    elementosXsd,
  )

  // Lida antes de montar: fonte declarada e ausente interrompe a carga, sem exemplo pela metade.
  const fonteLida = config.fonteDeExemplo
    ? await lerFonteDeExemploDoDisco(config.fonteDeExemplo)
    : null

  const campos = montarCampos(tags, await definicoesDe(config, tags), {
    prefixoDestino: config.prefixoDestino,
    fonteLida,
  })
  const erros = config.erros
    ? montarErros(JSON.parse(await lerMaterial(config.erros)).erros, tags)
    : []
  const avisos = [...config.avisos, avisoDeCamposPendentes(pendentes)].filter(Boolean)
  for (const aviso of avisos) {
    if (aviso.corpo.length > LIMITE_CORPO_AVISO)
      throw new Error(
        `O aviso "${aviso.titulo}" de ${id} passa de ${LIMITE_CORPO_AVISO} caracteres.`,
      )
  }

  return { config, campos, erros, avisos, pendentes, internos, fonteLida }
}
