// Rodar com: node --test database/carga/erros/montar.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { MARCA_PENDENTE } from './documentacao.mjs'
import { agruparErros, lerFonte } from './montar.mjs'

function fonte(regras, extra = {}) {
  return {
    arquivo: 'teste.md',
    origem: 'Prefeitura',
    provedor: 'GISS',
    tabelas: regras,
    fora: [],
    ...extra,
  }
}

const TABELA_SIMPLES = { secao: /^Erros/, colunas: { codigo: 'Código', mensagem: 'Mensagem' } }

test('linha marcada como pendente fica de fora com o motivo, e o resto da tabela entra', () => {
  const markdown = [
    '## Erros',
    '| Código | Mensagem |',
    '| :--- | :--- |',
    '| E1 | Assinatura inválida. |',
    `| E2 | Ainda sem resposta. ${MARCA_PENDENTE} Pendente |`,
  ].join('\n')

  const { itens, pulados } = lerFonte(fonte([TABELA_SIMPLES]), markdown)

  assert.deepEqual(
    itens.map((item) => item.codigo),
    ['E1'],
  )
  assert.equal(pulados[0].motivo, 'linha marcada como pendente')
})

test('seção pendente fica de fora inteira, mesmo sem regra de carga para ela', () => {
  const markdown = [
    '## Legado',
    `> ${MARCA_PENDENTE} Pendente: a coluna Solução não tem o link.`,
    '| Código | Descrição |',
    '| :--- | :--- |',
    '| 00001 | Data inferior. |',
    '| BTH01 | Alíquota inválida. |',
  ].join('\n')

  const { itens, pulados } = lerFonte(fonte([TABELA_SIMPLES]), markdown)

  assert.equal(itens.length, 0)
  assert.deepEqual(
    pulados.map((pulado) => [pulado.codigo, pulado.motivo]),
    [
      ['00001', 'seção marcada como pendente'],
      ['BTH01', 'seção marcada como pendente'],
    ],
  )
})

test('código no início da célula: o texto ao lado só vira variante se não houver mensagem', () => {
  const regra = {
    secao: /^Problemas/,
    colunas: { codigo: 'Sintoma', solucao: 'Causa e solução' },
    codigoNoInicio: true,
    codigoValido: /^[SCPL]\d{4}$/,
  }
  const markdown = [
    '## Problemas',
    '| Sintoma | Causa e solução |',
    '| :--- | :--- |',
    '| C0011 - emitente difere | Use a chave do emitente. |',
    '| HTTP 401 em toda requisição | ApiKey errada. |',
    '| E1235 - Falha no esquema | Ver o artigo. |',
  ].join('\n')

  const { itens, pulados } = lerFonte(fonte([regra]), markdown)
  const [erro] = agruparErros(itens)

  assert.deepEqual(erro.variantes, [])
  assert.deepEqual(erro.variantesParaCriar, ['emitente difere'])
  assert.equal(erro.solucaoSugerida, 'Use a chave do emitente.')
  assert.deepEqual(
    pulados.map((pulado) => pulado.motivo),
    ['linha sem código: só a mensagem ou o sintoma', 'código E1235 não é desta lista'],
  )
})

test('linha secundária que contradiz a lista oficial fica de fora; a que confere funde no mesmo erro', () => {
  const regras = [
    {
      secao: /^Mais comuns/,
      colunas: { codigo: 'Código', mensagem: 'Mensagem', causa: 'Causa' },
      conferirCom: /^Lista completa/,
    },
    { secao: /^Lista completa/, colunas: { codigo: 'Código', mensagem: 'Mensagem' } },
  ]
  const markdown = [
    '## Mais comuns',
    '| Código | Mensagem | Causa |',
    '| :--- | :--- | :--- |',
    '| E10 | RPS já informado | RPS repetido. |',
    '| E20 | Tomador não informado | Sem tomador. |',
    '## Lista completa',
    '| Código | Mensagem |',
    '| :--- | :--- |',
    '| E10 | RPS já informado. |',
    '| E20 | O valor das deduções deverá ser positivo. |',
    '| P0010 / P0011 | Inconsistência na declaração. |',
  ].join('\n')

  const { itens, pulados } = lerFonte(fonte(regras), markdown)
  const erros = agruparErros(itens)

  const e10 = erros.find((erro) => erro.codigo === 'E10')
  assert.deepEqual(e10.variantes, ['RPS já informado'])
  assert.equal(e10.causaProvavel, 'RPS repetido.')
  assert.equal(erros.find((erro) => erro.codigo === 'E20').causaProvavel, null)
  assert.match(pulados[0].motivo, /diverge da lista oficial/)
  assert.deepEqual(
    erros.map((erro) => erro.codigo),
    ['E10', 'E20', 'P0010', 'P0011'],
  )
})

test('seção com pendência só na solução entra sem solução; linha marcada continua de fora', () => {
  const regra = {
    secao: /^Legado/,
    colunas: { codigo: 'Código', mensagem: 'Descrição' },
    pendenciaSoNaSolucao: true,
  }
  const markdown = [
    '## Legado',
    `> ${MARCA_PENDENTE} Pendente: a coluna Solução não tem o link.`,
    '| Código | Descrição |',
    '| :--- | :--- |',
    '| BTH01 | Alíquota inválida. |',
    `| BTH02 | Duvidoso. ${MARCA_PENDENTE} |`,
  ].join('\n')

  const { itens, pulados } = lerFonte(fonte([regra]), markdown)

  assert.deepEqual(
    itens.map((item) => [item.codigo, item.mensagem, item.solucao]),
    [['BTH01', 'Alíquota inválida.', null]],
  )
  assert.deepEqual(
    pulados.map((pulado) => [pulado.codigo ?? null, pulado.motivo]),
    [[null, 'linha marcada como pendente']],
  )
  assert.throws(
    () =>
      lerFonte(
        fonte([{ ...regra, colunas: { ...regra.colunas, solucao: 'Descrição' } }]),
        markdown,
      ),
    /tem pendência na solução e lê a solução/,
  )
})
