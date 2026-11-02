// Rodar com: node --test database/carga/erros/sincronizar.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { planejarAcrescimo, resolverCamposPorNome } from './sincronizar.mjs'

const EXISTENTE = {
  origem: 'Prefeitura',
  titulo: 'RPS já informado',
  causaProvavel: 'Causa escrita na área interna.',
  solucaoSugerida: null,
  variantes: ['RPS já informado'],
  versoesProvedorIds: [7],
  camposLayoutIds: [3],
}

test('erro existente que já tem tudo não é regravado', () => {
  const { corpo } = planejarAcrescimo(
    EXISTENTE,
    { variantes: ['RPS ja informado.'], causaProvavel: 'Outra causa.', solucaoSugerida: null },
    [3],
  )

  assert.equal(corpo, null)
})

test('acrescenta o que falta e nunca reescreve o que o erro existente já tem', () => {
  const { corpo, acrescimos } = planejarAcrescimo(
    EXISTENTE,
    {
      variantes: ['Lote com RPS repetido.'],
      causaProvavel: 'Causa da Documentação.',
      solucaoSugerida: 'Consulte o RPS.',
    },
    [9],
  )

  assert.deepEqual(corpo, {
    origem: 'Prefeitura',
    titulo: 'RPS já informado',
    causaProvavel: 'Causa escrita na área interna.',
    solucaoSugerida: 'Consulte o RPS.',
    variantes: ['RPS já informado', 'Lote com RPS repetido.'],
    versoesProvedorIds: [7],
    camposLayoutIds: [3, 9],
  })
  assert.equal(acrescimos.length, 3)
})

test('nome de campo só vira vínculo quando aponta para um campo só do layout', () => {
  const campos = [
    { id: 1, caminhoXml: 'Envio/RPS/RegEspTrib' },
    { id: 2, caminhoXml: 'Envio/RPS/Prestador/CNPJ' },
    { id: 3, caminhoXml: 'Envio/RPS/Tomador/CNPJ' },
  ]

  assert.deepEqual(resolverCamposPorNome(['RegEspTrib', 'CNPJ', 'dhEmi'], campos), {
    ids: [1],
    ignorados: ['CNPJ (ambíguo)', 'dhEmi (não está no layout)'],
  })
})

test('erro igual de outro provedor ganha a versão deste nas aplicáveis, sem trocar o resto', () => {
  const { corpo, acrescimos } = planejarAcrescimo(
    EXISTENTE,
    { variantes: ['RPS já informado.'], causaProvavel: null, solucaoSugerida: 'Outra solução.' },
    [],
    [10],
  )

  assert.deepEqual(corpo.versoesProvedorIds, [7, 10])
  assert.deepEqual(corpo.variantes, ['RPS já informado'])
  assert.deepEqual(acrescimos, ['solução sugerida', 'versão 10'])
})
