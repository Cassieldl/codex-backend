// Rodar com: node --test database/carga/artigos/documentacao.test.mjs
import { strict as assert } from 'node:assert'
import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { test } from 'node:test'
import { fileURLToPath } from 'node:url'

import {
  CORRECOES_DE_SEGURANCA,
  aplicarCorrecoesDeSeguranca,
  planejarPublicacao,
} from './documentacao.mjs'

const PASTA_DA_COPIA = join(dirname(fileURLToPath(import.meta.url)), 'documentacao')

test('a cópia troca o trecho inseguro e falha se a origem não tiver mais o trecho', () => {
  const [correcao] = CORRECOES_DE_SEGURANCA
  const origem = `Antes.\n\n${correcao.trecho}\n\nDepois.`

  assert.equal(
    aplicarCorrecoesDeSeguranca(correcao.arquivo, origem),
    `Antes.\n\n${correcao.troca}\n\nDepois.`,
  )
  assert.throws(
    () => aplicarCorrecoesDeSeguranca(correcao.arquivo, 'Texto reescrito na origem.'),
    /não está mais em/,
  )
})

test('nenhum artigo copiado tem o trecho inseguro, e cada um tem a troca', async () => {
  for (const { arquivo, trecho, troca } of CORRECOES_DE_SEGURANCA) {
    const copia = await readFile(join(PASTA_DA_COPIA, arquivo), 'utf8')
    assert.ok(!copia.includes(trecho), `${arquivo} voltou a ter o trecho inseguro`)
    assert.ok(copia.includes(troca), `${arquivo} não tem a troca de segurança`)
  }
})

test('publicação registra a verificação que falta, publica o rascunho e recusa artigo reservado', () => {
  assert.deepEqual(
    planejarPublicacao(
      { titulo: 'Retenção de ISS', reservado: false, publicado: false, verificadoEm: null },
      '2026-11-02',
    ),
    { registrarVerificacao: true, publicar: true },
  )
  assert.deepEqual(
    planejarPublicacao(
      { titulo: 'Retenção de ISS', reservado: false, publicado: true, verificadoEm: '2026-11-02' },
      '2026-11-02',
    ),
    { registrarVerificacao: false, publicar: false },
  )
  assert.throws(
    () =>
      planejarPublicacao(
        { titulo: 'Como funciona', reservado: true, publicado: false, verificadoEm: null },
        '2026-11-02',
      ),
    /reservado/,
  )
})
