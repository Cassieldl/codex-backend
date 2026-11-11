// Rodar com: node --test database/carga/emissor-nacional/extrair-planilha-invoicy.test.mjs
import { strict as assert } from 'node:assert'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'

import {
  lerEstilosDaReformaTributaria,
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

test('tag com o nome pintado de verde ou amarelo é da reforma tributária; outra cor não é', () => {
  const estilos = `<styleSheet>
    <fills count="4">
      <fill><patternFill patternType="none"/></fill>
      <fill><patternFill patternType="solid"><fgColor rgb="FF00FF00"/></patternFill></fill>
      <fill><patternFill patternType="solid"><fgColor rgb="FFFFFF00"/></patternFill></fill>
      <fill><patternFill patternType="solid"><fgColor rgb="FF8DB3E2"/></patternFill></fill>
    </fills>
    <cellXfs count="4"><xf fillId="0"/><xf fillId="1"/><xf fillId="2"/><xf fillId="3"/></cellXfs>
  </styleSheet>`
  const linhas = [
    { celulas: { B: 'C01', C: 'Envio', H: '-' }, estilos: { C: 3 } },
    { celulas: { B: 'C02', C: 'finNFSe', H: 'C01' }, estilos: { C: 1 } },
    { celulas: { B: 'C03', C: 'ListaDedIBSCBS', H: 'C01' }, estilos: { C: 2 } },
    { celulas: { B: 'C04', C: 'RPSNumero', H: 'C01' }, estilos: { B: 1, C: 0 } },
  ]

  const tags = montarTagsDaPlanilha(linhas, lerEstilosDaReformaTributaria(estilos))

  assert.deepEqual(
    tags.map(({ caminho, reformaTributaria }) => [caminho, reformaTributaria]),
    [
      ['Envio', false],
      ['Envio/finNFSe', true],
      ['Envio/ListaDedIBSCBS', true],
      ['Envio/RPSNumero', false],
    ],
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
