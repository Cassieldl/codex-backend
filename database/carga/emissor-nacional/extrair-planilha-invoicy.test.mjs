// Rodar com: node --test database/carga/emissor-nacional/extrair-planilha-invoicy.test.mjs
import { strict as assert } from 'node:assert'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'

import {
  lerEstilosRiscados,
  lerLinhas,
  lerTextosCompartilhados,
  montarTagsDaPlanilha,
} from './extrair-planilha-invoicy.mjs'
import { lerXsd } from './transformar-layout.mjs'

const RISCADO = '<rPr><strike/></rPr>'

test('trecho riscado de um texto formatado fica de fora: só o nome novo da tag sobra', () => {
  const xml = `<sst>
    <si><t>Envio</t></si>
    <si><r>${RISCADO}<t xml:space="preserve">nDI
</t></r><r><rPr><b/></rPr><t>nroDUIMP</t></r></si>
  </sst>`

  assert.deepEqual(lerTextosCompartilhados(xml), ['Envio', 'nroDUIMP'])
})

test('célula com estilo de fonte riscada conta como vazia, e a linha sem nome sai das tags', () => {
  const estilos = `<styleSheet>
    <fonts count="2"><font><sz val="11"/></font><font><strike/><sz val="11"/></font></fonts>
    <cellXfs count="2"><xf fontId="0"/><xf fontId="1"/></cellXfs>
  </styleSheet>`
  const planilha = `<sheetData>
    <row r="2"><c r="B2" t="str"><v>C01</v></c><c r="C2" t="s"><v>0</v></c><c r="H2" t="s"><v>1</v></c></row>
    <row r="3"><c r="B3" t="str"><v>C02</v></c><c r="C3" s="1" t="s"><v>2</v></c><c r="H3" t="str"><v>C01</v></c></row>
  </sheetData>`

  const linhas = lerLinhas(planilha, ['Envio', '-', 'ValServEst'], lerEstilosRiscados(estilos))

  assert.deepEqual(
    montarTagsDaPlanilha(linhas).map((tag) => tag.caminho),
    ['Envio'],
  )
})

test('caminho pela coluna Pai, e aplicação de negócio "-" vira nulo', () => {
  const linhas = [
    { celulas: { B: 'C01', C: 'Envio', H: '-' } },
    { celulas: { B: 'C10', C: 'RPS', H: 'C01', K: '-' } },
    { celulas: { B: 'C11', C: 'RPSNumero', H: 'C10', K: 'Número sequencial por série.' } },
  ]

  const tags = montarTagsDaPlanilha(linhas)

  assert.deepEqual(
    tags.map(({ caminho, aplicacaoNegocio }) => [caminho, aplicacaoNegocio]),
    [
      ['Envio', null],
      ['Envio/RPS', null],
      ['Envio/RPS/RPSNumero', 'Número sequencial por série.'],
    ],
  )
})

test('o extrato versionado da planilha 9 tem exatamente as tags do XSD 9', async () => {
  const pasta = new URL('./', import.meta.url)
  const [xsd, planilha] = await Promise.all([
    readFile(new URL('layout-invoicy-v9.xsd', pasta), 'utf8'),
    readFile(new URL('layout-invoicy-v9-planilha.json', pasta), 'utf8'),
  ])

  const doXsd = lerXsd(xsd).map((elemento) => elemento.caminho)
  const daPlanilha = JSON.parse(planilha).tags.map((tag) => tag.caminho)

  assert.deepEqual(daPlanilha, doXsd)
})
