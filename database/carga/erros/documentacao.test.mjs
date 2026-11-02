// Rodar com: node --test database/carga/erros/documentacao.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import {
  MARCA_PENDENTE,
  lerTabelas,
  sanitizarDaDocumentacao,
  temMarcaDePendencia,
  textoSimples,
} from './documentacao.mjs'

const TRAVESSAO = String.fromCodePoint(0x2014)
const ALERTA = String.fromCodePoint(0x26a0, 0xfe0f)
const VISTO = String.fromCodePoint(0x2705)

test('na cópia, o travessão vira dois-pontos no título e hífen no texto, e o alerta vira a marca', () => {
  const origem = `# E0037 ${TRAVESSAO} Produção Restrita\n\nErro ${TRAVESSAO} contate.\n| x | ${TRAVESSAO} |\n**${ALERTA} Pendente:** falta`
  const copia = sanitizarDaDocumentacao(origem)

  assert.equal(
    copia,
    `# E0037: Produção Restrita\n\nErro - contate.\n| x | - |\n**${MARCA_PENDENTE} Pendente:** falta`,
  )
  assert.ok(temMarcaDePendencia(copia))
})

test('emoji que não é a marca de pendência interrompe a cópia', () => {
  assert.throws(() => sanitizarDaDocumentacao(`Pronto ${VISTO}`), /Emoji sem tratamento/)
})

test('a palavra Pendente fora da marca não é pendência (verbete do glossário)', () => {
  assert.equal(
    temMarcaDePendencia('| **Pendente / em processamento** | Na fila do InvoiCy. |'),
    false,
  )
})

test('aviso de pendência fora da tabela marca a seção inteira; na linha, só a linha', () => {
  const markdown = [
    '## Lista legada',
    `> **${MARCA_PENDENTE} Pendente:** sem solução na origem.`,
    '| Código | Descrição |',
    '| :--- | :--- |',
    '| L1 | Algo. |',
    '## Lista nova',
    '| Código | Descrição |',
    '| :--- | :--- |',
    '| S1 | Certo. |',
    `| S2 | Duvidoso. ${MARCA_PENDENTE} |`,
  ].join('\n')

  const [legada, nova] = lerTabelas(markdown)

  assert.equal(legada.secaoPendente, true)
  assert.equal(nova.secaoPendente, false)
  assert.deepEqual(
    nova.linhas.map((linha) => temMarcaDePendencia(linha.texto)),
    [false, true],
  )
})

test('texto simples guarda o endereço do link externo e só o texto do link interno', () => {
  assert.equal(
    textoSimples(
      'Veja o [site do IBGE](https://ibge.gov.br) e o [artigo](../artigos/x.md); use `RegEspTrib` \\<3\\>.',
    ),
    'Veja o site do IBGE (https://ibge.gov.br) e o artigo; use RegEspTrib <3>.',
  )
})
