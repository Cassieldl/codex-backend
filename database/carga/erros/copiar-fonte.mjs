/**
 * Copia as listas de rejeição da Documentação para `fonte/`, já saneadas (sem travessão e com a
 * marca de pendência em texto). A carga lê só a cópia versionada; rodar de novo atualiza a cópia
 * quando a Documentação muda.
 *
 *   node database/carga/erros/copiar-fonte.mjs "<pasta da Documentação>"
 */

import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { copiarDaDocumentacao } from './documentacao.mjs'
import { FONTES } from './fontes.mjs'

const pastaDocumentacao = process.argv[2]
if (!pastaDocumentacao) {
  console.error('Uso: node database/carga/erros/copiar-fonte.mjs "<pasta da Documentação>"')
  process.exit(1)
}

const destino = join(dirname(fileURLToPath(import.meta.url)), 'fonte')
await copiarDaDocumentacao(
  pastaDocumentacao,
  FONTES.map((fonte) => ({ origem: fonte.origemNaDocumentacao, destino: fonte.arquivo })),
  destino,
)
