/**
 * Artigos independentes de provedor (hoje, o reservado Como funciona), com o corpo versionado ao
 * lado deste arquivo. Sincroniza pela API, como a carga do Emissor Nacional.
 *
 *   node database/carga/artigos/importar.mjs
 *   node database/carga/artigos/importar.mjs --verificado-em 2026-10-07
 *
 * Sem data, só carrega. Com a data, também registra a verificação e publica: a data é uma afirmação
 * editorial e nunca é presumida. Atenção: o artigo reservado, uma vez publicado, não pode ser
 * despublicado nem removido pela API.
 */

import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { criarClienteApi } from '../cliente-api.mjs'
import { lerDataDeVerificacao, pendentesDePublicacao } from '../emissor-nacional/publicacao.mjs'
import { sincronizarArtigo } from '../emissor-nacional/sincronizar-artigos.mjs'

const PASTA = dirname(fileURLToPath(import.meta.url))

const ARTIGOS = [{ arquivo: 'como-funciona.md', tipo: 'Conceito', titulo: 'Como funciona' }]

const SEM_REFERENCIAS = { campoLayoutIds: [], erroIds: [] }

async function verificarEPublicar(api, artigo, verificadoEm) {
  const existentes = await api.listarTudo('/interno/artigos')
  const resumo = existentes.find((item) => item.titulo === artigo.titulo)
  let { dados: atual } = await api.chamar('GET', `/interno/artigos/${resumo.id}`)

  if (atual.verificadoEm !== verificadoEm) {
    ;({ dados: atual } = await api.chamar(
      'PUT',
      `/interno/artigos/${atual.id}/verificacao`,
      { verificadoEm },
      String(atual.versaoConcorrencia),
    ))
    console.log(`verificação registrada em ${verificadoEm}: ${artigo.titulo}`)
  }

  for (const pendente of pendentesDePublicacao([atual])) {
    await api.publicar('/interno/artigos', pendente.id, String(pendente.versaoConcorrencia))
    console.log(`artigo publicado: ${artigo.titulo}`)
  }
}

const argumentos = process.argv.slice(2)
const verificadoEm = argumentos.includes('--verificado-em')
  ? lerDataDeVerificacao(argumentos)
  : null

const api = await criarClienteApi()
for (const artigo of ARTIGOS) {
  const corpo = await readFile(join(PASTA, artigo.arquivo), 'utf8')
  await sincronizarArtigo(api, artigo, corpo, SEM_REFERENCIAS)
  if (verificadoEm) await verificarEPublicar(api, artigo, verificadoEm)
}
console.log(
  verificadoEm ? '\nartigos carregados e publicados.' : '\nartigos carregados. Nada foi publicado.',
)
