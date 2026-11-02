// Rodar com: node --test database/carga/provedores/
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { lerVocabularioInvoicy } from '../vocabulario-invoicy.mjs'
import { avisosDasObservacoes } from './avisos.mjs'
import { PROVEDORES } from './catalogo.mjs'
import { lerObservacoes, SEM_SUBSECAO, textoPuro } from './ler-observacoes.mjs'
import { prepararCarga } from './preparar.mjs'

const SECAO = [
  '## Provedor',
  '',
  '- **Layout:** [Provedor](../01-layouts/layouts-provedores.md#provedor)',
  '- **Fontes:** [obs]; [portal](https://portal.exemplo/padrao)',
  '',
  '### Regras de preenchimento',
  '',
  '- **Natureza da operação (leiaute nacional):** usa a [tabela](../artigos/natureza.md).',
  '- **Tomador do exterior:** sem CEP. Pendente: a regra antiga citava outra coisa.',
  '- **Regime especial:** Pendente. Os prints vêm do portal antigo.',
  '- Use a série 8. **Pendente:** o guia citado não foi encontrado.',
  '',
  '  ![Print legado](../assets/imagens/print.png)',
  '',
  '- **Série:** use a série `10001`. [obs, validado]',
  '',
  '### Processamento',
  '',
  'As regras abaixo são do [portal antigo] e valem para o WS:',
  '',
  '| Status | Significado | Dados adicionais |',
  '| :--- | :--- | :--- |',
  '| `aprovado` | Emitida | - |',
  '| `rejeitado` | Rejeitada | Lista de motivos |',
  '',
  'Tabela por município:',
  '',
  '![Tabela](../assets/imagens/tabela.png)',
].join('\n')

test('observações: pendência sai a partir da marca, e o item que só tinha o rótulo sai inteiro', () => {
  const [regras] = lerObservacoes(SECAO)
  assert.equal(regras.subsecao, 'Regras de preenchimento')
  assert.equal(
    regras.corpo,
    '- Tomador do exterior: sem CEP.\n- Use a série 8.\n- Série: use a série 10001.',
  )
  assert.deepEqual(
    regras.removidos.map((item) => item.motivo),
    [
      'A lista dos provedores que usam a tabela do Padrão Nacional ainda não foi confirmada provedor a provedor.',
      'Marcado como pendente.',
      'Marcado como pendente.',
      'Marcado como pendente.',
    ],
  )
})

test('observações: tabela vira lista, e o parágrafo que só apresentava a imagem sai', () => {
  const [, processamento] = lerObservacoes(SECAO)
  assert.equal(
    processamento.corpo,
    'As regras abaixo são do portal antigo e valem para o WS:\n\n' +
      '- aprovado: Emitida\n' +
      '- rejeitado: Rejeitada; dados adicionais: Lista de motivos',
  )
  assert.deepEqual(processamento.removidos, [
    { trecho: 'Tabela por município:', motivo: 'Apresenta só uma imagem.' },
  ])
})

test('observações: sem subseção, o texto depois da identificação vira a parte sem subseção', () => {
  const secoes = lerObservacoes(
    '## João Pessoa\n\n- **Layout:** [x](../x.md)\n\n- O layout atual é a **DPS nacional**.',
  )
  assert.deepEqual(
    secoes.map((secao) => [secao.subsecao, secao.corpo]),
    [[SEM_SUBSECAO, '- O layout atual é a DPS nacional.']],
  )
})

test('texto puro: link externo mantém a URL, o da base fica só com o rótulo', () => {
  assert.equal(
    textoPuro(
      'Ver [Retenções](../artigos/retencoes.md) e o [portal](https://portal.exemplo/p). ' +
        'Produção: <https://www.nfse.gov.br/EmissorNacional>. Códigos da DPS em [betha.md](../03-rejeicoes/betha.md).',
    ),
    'Ver Retenções e o portal (https://portal.exemplo/p). Produção: ' +
      'https://www.nfse.gov.br/EmissorNacional. Códigos da DPS.',
  )
})

test('texto puro: a fonte com link perde os colchetes e mantém a URL completa', () => {
  assert.equal(
    textoPuro('Peça a liberação. [[Perguntas Frequentes](https://faq.exemplo/), 22/09/2026]'),
    'Peça a liberação. (Perguntas Frequentes, https://faq.exemplo/, 22/09/2026)',
  )
})

test('texto puro: meia-risca e travessão entre espaços viram hífen; intervalo colado fica', () => {
  const [meiaRisca, travessao] = [0x2013, 0x2014].map((codigo) => String.fromCodePoint(codigo))
  assert.equal(
    textoPuro(
      `COPLAN ${meiaRisca} Modelo Nacional; E405 ${travessao} Método; faixa 3${meiaRisca}5`,
    ),
    `COPLAN - Modelo Nacional; E405 - Método; faixa 3${meiaRisca}5`,
  )
})

test('avisos das observações: subseção sem decisão ou decisão sem subseção interrompem', () => {
  const subsecoes = [{ subsecao: 'Cancelamento', corpo: 'x', removidos: [] }]
  assert.throws(() => avisosDasObservacoes(subsecoes, {}), /não tem título/)
  assert.throws(
    () =>
      avisosDasObservacoes(subsecoes, {
        Cancelamento: { severidade: 'Alerta', titulo: 't' },
        Outra: { severidade: 'Alerta', titulo: 'u' },
      }),
    /Outra/,
  )
})

test('avisos das observações: subseção ignorada ou vazia não vira aviso e vai para o relatório', () => {
  const { avisos, foraDoAviso } = avisosDasObservacoes(
    [
      { subsecao: 'A', corpo: 'texto', removidos: [] },
      { subsecao: 'B', corpo: '', removidos: [] },
      { subsecao: 'C', corpo: 'lista', removidos: [] },
    ],
    {
      A: { severidade: 'Alerta', titulo: 'Título A' },
      B: { severidade: 'Informativo', titulo: 'Título B' },
      C: { ignorar: 'Tem carga própria.' },
    },
  )
  assert.deepEqual(avisos, [{ severidade: 'Alerta', titulo: 'Título A', corpo: 'texto' }])
  assert.deepEqual(
    foraDoAviso.map((item) => [item.subsecao, item.motivo]),
    [
      ['B', 'Nada sobrou depois das exclusões.'],
      ['C', 'Tem carga própria.'],
    ],
  )
})

const PROVEDORES_EXISTENTES = ['e-l', 'elotech', 'giss', 'ipm', 'issnet']
const PROVEDORES_DE_LEIAUTE_NACIONAL = Object.keys(PROVEDORES).filter(
  (id) => !PROVEDORES_EXISTENTES.includes(id),
)

// Montados pelo código de ponto: o travessão e o emoji não podem aparecer no próprio arquivo.
const ALERTA = String.fromCodePoint(0x26a0)
const TRAVESSAO = String.fromCodePoint(0x2014)
const MEIA_RISCA_NA_FRASE = ` ${String.fromCodePoint(0x2013)} `
const MARCAS_DE_MARKDOWN = [
  String.raw`\[(obs|portal|validado)`,
  String.raw`!\[`,
  String.raw`\]\(`,
  String.raw`\*\*`,
  '`',
]
const MARCAS_DO_MATERIAL = new RegExp(['Pendente', ALERTA, ...MARCAS_DE_MARKDOWN].join('|'))
const PROIBIDOS_NA_ESCRITA = new RegExp(
  [TRAVESSAO, MEIA_RISCA_NA_FRASE, String.raw`\p{Extended_Pictographic}`].join('|'),
  'u',
)

test('material real: nenhum aviso tirado das observações leva marca, imagem ou Markdown', async () => {
  const { elementos } = await lerVocabularioInvoicy()
  assert.equal(PROVEDORES_DE_LEIAUTE_NACIONAL.length, 10)
  for (const id of PROVEDORES_DE_LEIAUTE_NACIONAL) {
    const { avisos } = await prepararCarga(id, elementos)
    assert.ok(avisos.length > 1, id)
    for (const { titulo, corpo } of avisos) {
      assert.doesNotMatch(corpo, MARCAS_DO_MATERIAL, titulo)
      assert.doesNotMatch(`${titulo}${corpo}`, PROIBIDOS_NA_ESCRITA, titulo)
    }
    assert.equal(new Set(avisos.map((aviso) => aviso.titulo)).size, avisos.length, id)
  }
})

test('catálogo: os provedores já carregados não ganham avisos das observações', () => {
  for (const id of PROVEDORES_EXISTENTES) assert.ok(!PROVEDORES[id].observacoes, id)
})
