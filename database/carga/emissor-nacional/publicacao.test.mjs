// Rodar com: node --test database/carga/emissor-nacional/publicacao.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { lerDataDeVerificacao, pendentesDePublicacao } from './publicacao.mjs'

test('a data de verificação vem explícita, em AAAA-MM-DD', () => {
  assert.equal(lerDataDeVerificacao(['--verificado-em', '2026-10-06']), '2026-10-06')
})

test('sem a data, ou com data malformada ou impossível, nada é publicado', () => {
  assert.throws(() => lerDataDeVerificacao([]), /--verificado-em/)
  assert.throws(() => lerDataDeVerificacao(['--verificado-em']), /--verificado-em/)
  assert.throws(() => lerDataDeVerificacao(['--verificado-em', '06/10/2026']), /--verificado-em/)
  assert.throws(() => lerDataDeVerificacao(['--verificado-em', '2026-13-40']), /--verificado-em/)
  assert.throws(() => lerDataDeVerificacao(['--verificado-em', '2026-02-30']), /--verificado-em/)
})

test('só o que ainda não está publicado é publicado de novo', () => {
  const itens = [
    { id: 1, publicado: true },
    { id: 2, publicado: false },
    { id: 3, publicado: false },
  ]
  assert.deepEqual(
    pendentesDePublicacao(itens).map((item) => item.id),
    [2, 3],
  )
})
