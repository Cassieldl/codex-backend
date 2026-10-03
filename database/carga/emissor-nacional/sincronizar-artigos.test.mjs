// Rodar com: node --test database/carga/emissor-nacional/sincronizar-artigos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { resolverReferencias } from './sincronizar-artigos.mjs'

const ARTIGO = {
  titulo: 'Emitir NFS-e por decisão judicial',
  campos: ['Envio/RPS/tpSusp', 'Envio/RPS/NumProcesso'],
  erros: ['E0595'],
}

test('referências viram os ids do vocabulário e dos erros, na ordem declarada', () => {
  const referencias = resolverReferencias(
    ARTIGO,
    new Map([
      ['Envio/RPS/NumProcesso', 50],
      ['Envio/RPS/tpSusp', 59],
    ]),
    new Map([['E0595', 7]]),
  )
  assert.deepEqual(referencias, { campoLayoutIds: [59, 50], erroIds: [7] })
})

test('campo citado que não está no vocabulário interrompe a carga', () => {
  assert.throws(
    () => resolverReferencias(ARTIGO, new Map([['Envio/RPS/tpSusp', 59]]), new Map([['E0595', 7]])),
    /cita o campo Envio\/RPS\/NumProcesso/,
  )
})

test('erro citado que não está no provedor interrompe a carga', () => {
  assert.throws(
    () =>
      resolverReferencias(
        ARTIGO,
        new Map([
          ['Envio/RPS/NumProcesso', 50],
          ['Envio/RPS/tpSusp', 59],
        ]),
        new Map(),
      ),
    /cita o erro E0595/,
  )
})
