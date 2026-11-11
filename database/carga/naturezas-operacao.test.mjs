// Rodar com: node --test database/carga/naturezas-operacao.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { ROTULO_LAYOUT_DOS_PROVEDORES } from './layout-invoicy.mjs'
import {
  TABELAS_NATUREZA,
  lerArtigoNaturezaOperacao,
  montarNaturezasOperacao,
} from './naturezas-operacao.mjs'
import { PROVEDORES } from './provedores/catalogo.mjs'
import { prepararCarga } from './provedores/preparar.mjs'
import { lerVocabularioInvoicy } from './vocabulario-invoicy.mjs'

const SEM_TIPO_DE_IMUNIDADE = ['elotech', 'giss', 'ipm']

const artigo = await lerArtigoNaturezaOperacao()

test('tabela nacional tem só os códigos 1, 4, 7, 8 e 9, com o valor do leiaute nacional', () => {
  const itens = montarNaturezasOperacao(
    artigo,
    TABELAS_NATUREZA.nacional,
    'tpImunidade (grupo tribMun)',
  )

  assert.deepEqual(
    itens.map((item) => [item.codigoInvoicy, item.valorProvedor]),
    [
      [1, '1'],
      [4, '2'],
      [7, '1'],
      [8, '4'],
      [9, '3'],
    ],
  )
})

test('IPM: sigla na descrição, regras com o código de rejeição e o 16 a confirmar', () => {
  const itens = montarNaturezasOperacao(artigo, TABELAS_NATUREZA.ipm, null)
  const porCodigo = new Map(itens.map((item) => [item.codigoInvoicy, item]))

  assert.equal(porCodigo.get(20).descricao, 'Tributada Integralmente com ISSRF (TIRF)')
  assert.equal(
    porCodigo.get(20).observacao,
    'Dedução: não (rejeição 64). ISS retido: obrigatório (rejeição 65-67, 175).',
  )
  assert.equal(porCodigo.get(16).descricao, 'Não Tributada (NTRIB)')
  assert.equal(porCodigo.get(16).aConfirmar, true)
  assert.equal(porCodigo.get(1).aConfirmar, false)
})

test('seção ausente no artigo interrompe a carga', () => {
  assert.throws(
    () => montarNaturezasOperacao(artigo, 'Provedor que não existe', null),
    /não existe/,
  )
})

test('todo provedor do catálogo tem tabela, e a imunidade diz para onde vai o tipo', async () => {
  const { elementos } = await lerVocabularioInvoicy(ROTULO_LAYOUT_DOS_PROVEDORES)

  for (const id of Object.keys(PROVEDORES)) {
    const { naturezasOperacao } = await prepararCarga(id, elementos)
    assert.ok(naturezasOperacao?.length, `${id} sem natureza da operação`)

    const imunidade = naturezasOperacao.find((item) => item.codigoInvoicy === 4)
    assert.ok(imunidade, `${id} sem a linha da imunidade`)

    const esperado = SEM_TIPO_DE_IMUNIDADE.includes(id)
      ? /não recebe o tipo de imunidade/
      : /campo tpImunidade do layout InvoiCy\. Neste provedor, o tipo vai na tag tpImunidade \(grupo tribMun\)\./
    assert.match(imunidade.observacao, esperado, id)
  }
})
