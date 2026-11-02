/**
 * Copia os artigos da base de conhecimento da Documentação para `documentacao/`, já saneados (sem
 * travessão e com a marca de pendência em texto). A carga lê só a cópia versionada.
 *
 *   node database/carga/artigos/copiar-documentacao.mjs "<pasta da Documentação>"
 */

import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { copiarDaDocumentacao } from '../erros/documentacao.mjs'
import { ARTIGOS_DA_DOCUMENTACAO, aplicarCorrecoesDeSeguranca } from './documentacao.mjs'

const pastaDocumentacao = process.argv[2]
if (!pastaDocumentacao) {
  console.error(
    'Uso: node database/carga/artigos/copiar-documentacao.mjs "<pasta da Documentação>"',
  )
  process.exit(1)
}

const destino = join(dirname(fileURLToPath(import.meta.url)), 'documentacao')
await copiarDaDocumentacao(
  pastaDocumentacao,
  ARTIGOS_DA_DOCUMENTACAO.map((artigo) => ({ origem: artigo.origem, destino: artigo.arquivo })),
  destino,
  aplicarCorrecoesDeSeguranca,
)
