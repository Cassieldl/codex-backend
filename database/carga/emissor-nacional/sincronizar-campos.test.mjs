// Rodar com: node --test database/carga/emissor-nacional/sincronizar-campos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { ErroDeSimulacao, criarChamada } from '../cliente-api.mjs'
import {
  chavesAlteradas,
  sincronizarCamposDaVersao,
  sincronizarVocabulario,
} from './sincronizar-campos.mjs'

/** API falsa: responde ao GET com os existentes e registra todas as escritas. */
function criarApiFalsa(existentes) {
  const escritas = []
  return {
    escritas,
    async chamar(metodo, rota, corpo, etag) {
      if (metodo === 'GET') return { dados: existentes }
      escritas.push({ metodo, rota, corpo, etag })
      return { dados: { id: 900 + escritas.length } }
    },
  }
}

const campoDoVocabulario = (caminhoXml, grupo, ordem) => ({
  caminhoCompleto: `Envio/RPS/${caminhoXml.slice(2)}`,
  nome: caminhoXml.split('/').at(-1),
  caminhoXml,
  significado: 'Significado.',
  grupo,
  ordem,
})

test('existente com grupo antigo é reposicionado com If-Match, sem tocar nome nem significado', async () => {
  const api = criarApiFalsa([
    {
      id: 7,
      caminhoXml: '//Servico/Valores/IBSCBS/IBS/ValpIBSUF',
      grupo: 'Servico',
      ordem: 3,
      versaoConcorrencia: 41,
    },
  ])

  await sincronizarVocabulario(api, 1, [
    campoDoVocabulario('//Servico/Valores/IBSCBS/IBS/ValpIBSUF', 'Servico/Valores/IBSCBS/IBS', 3),
  ])

  assert.deepEqual(api.escritas, [
    {
      metodo: 'PUT',
      rota: '/interno/campos-layout/7',
      corpo: { grupo: 'Servico/Valores/IBSCBS/IBS', ordem: 3 },
      etag: '41',
    },
  ])
})

test('existente com ordem antiga também é reposicionado', async () => {
  const api = criarApiFalsa([
    { id: 7, caminhoXml: '//Tomador/TomaCNPJ', grupo: 'Tomador', ordem: 2, versaoConcorrencia: 5 },
  ])

  await sincronizarVocabulario(api, 1, [campoDoVocabulario('//Tomador/TomaCNPJ', 'Tomador', 9)])

  assert.equal(api.escritas.length, 1)
  assert.deepEqual(api.escritas[0].corpo, { grupo: 'Tomador', ordem: 9 })
})

test('rodar de novo com tudo no lugar não escreve nada', async () => {
  const api = criarApiFalsa([
    { id: 7, caminhoXml: '//Tomador/TomaCNPJ', grupo: 'Tomador', ordem: 2, versaoConcorrencia: 5 },
  ])

  await sincronizarVocabulario(api, 1, [campoDoVocabulario('//Tomador/TomaCNPJ', 'Tomador', 2)])

  assert.deepEqual(api.escritas, [])
})

test('campo que falta é criado, e o id devolvido entra no mapa por caminho completo', async () => {
  const api = criarApiFalsa([])

  const idPorCaminho = await sincronizarVocabulario(api, 1, [
    campoDoVocabulario('//Tomador/TomaCNPJ', 'Tomador', 1),
  ])

  assert.equal(api.escritas[0].metodo, 'POST')
  assert.equal(api.escritas[0].corpo.grupo, 'Tomador')
  assert.equal(idPorCaminho.get('Envio/RPS/Tomador/TomaCNPJ'), 901)
})

// --- Campos da versão: exemplo ---

const COMPORTAMENTO = {
  tipo: 'Texto',
  tamanho: '1-20',
  mascara: null,
  dominio: null,
  estadoPreenchimento: 'Obrigatório',
  condicao: null,
  notaPreenchimento: null,
  destinoXml: '//Rps/Numero',
  notaTransformacao: null,
  ordem: 1,
}

const ID_DO_CAMPO_CANONICO = { NumeroRps: 10, Serie: 20, Tipo: 30, Cep: 40 }

const idPorCaminho = new Map(
  Object.entries(ID_DO_CAMPO_CANONICO).map(([nome, id]) => [`Envio/${nome}`, id]),
)

/** Campo como a carga o monta: `campoVersao` sem a chave `exemplo` é provedor sem fonte. */
function campoDaCarga(nomeCampo, campoVersao = {}) {
  return {
    nomeCampo,
    caminhoInvoicy: `Envio/${nomeCampo}`,
    campoVersao: { ...COMPORTAMENTO, ...campoVersao },
  }
}

/** Campo como a API o devolve; o id do campo da versão é o do canônico + 1. */
function campoNaApi(nomeCampo, sobrescritas = {}) {
  const campoLayoutId = ID_DO_CAMPO_CANONICO[nomeCampo]
  return {
    id: campoLayoutId + 1,
    campoLayoutId,
    versaoConcorrencia: 40 + campoLayoutId,
    ...COMPORTAMENTO,
    ...sobrescritas,
  }
}

/** API falsa do GET `/interno/versoes/{id}/campos`: devolve `{ campos }` e registra as escritas. */
function criarApiFalsaDeCampos(campos, { simulando = false } = {}) {
  const escritas = []
  return {
    escritas,
    simulando,
    async chamar(metodo, rota, corpo, etag) {
      if (metodo === 'GET') return { dados: { campos } }
      escritas.push({ metodo, rota, corpo, etag })
      return { dados: { id: 900 + escritas.length } }
    },
  }
}

function capturarConsole(t) {
  const logs = []
  const avisos = []
  t.mock.method(console, 'log', (...partes) => logs.push(partes.join(' ')))
  t.mock.method(console, 'warn', (...partes) => avisos.push(partes.join(' ')))
  return { logs, avisos }
}

const serializado = (corpo) => JSON.parse(JSON.stringify(corpo))

test('gerenciado: mudança só de exemplo gera PUT com o valor no corpo', async (t) => {
  capturarConsole(t)
  const api = criarApiFalsaDeCampos([campoNaApi('NumeroRps', { exemplo: null })])

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { exemplo: '579' })],
    idPorCaminho,
  )

  assert.equal(api.escritas.length, 1)
  assert.equal(api.escritas[0].metodo, 'PUT')
  assert.equal(api.escritas[0].rota, '/interno/campos-versao/11')
  assert.equal(api.escritas[0].etag, '50')
  assert.equal(serializado(api.escritas[0].corpo).exemplo, '579')
})

test('gerenciado com exemplo igual ao existente não escreve nada', async (t) => {
  const { logs } = capturarConsole(t)
  const api = criarApiFalsaDeCampos([campoNaApi('NumeroRps', { exemplo: '579' })])

  const contagem = await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { exemplo: '579' })],
    idPorCaminho,
  )

  assert.deepEqual(api.escritas, [])
  assert.deepEqual(contagem, {
    criados: 0,
    atualizados: 0,
    atualizadosSoExemplo: 0,
    exemplosDefinidos: 0,
    exemplosAlterados: 0,
    exemplosApagados: 0,
  })
  assert.ok(
    logs.includes(
      'campos da versão 5: 0 criados, 0 atualizados (0 só de exemplo, 0 com outro atributo) · ' +
        'exemplos: 0 definidos, 0 alterados, 0 apagados',
    ),
  )
})

test('gerenciado sem exemplo (null) e sem exemplo na API também não escreve nada', async (t) => {
  capturarConsole(t)
  const api = criarApiFalsaDeCampos([campoNaApi('NumeroRps')])

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { exemplo: null })],
    idPorCaminho,
  )

  assert.deepEqual(api.escritas, [])
})

test('gerenciado com exemplo descartado apaga o existente: PUT com exemplo null e aviso', async (t) => {
  const { avisos } = capturarConsole(t)
  const api = criarApiFalsaDeCampos([campoNaApi('NumeroRps', { exemplo: '579' })])

  const contagem = await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { exemplo: null })],
    idPorCaminho,
  )

  assert.equal(api.escritas.length, 1)
  const corpo = serializado(api.escritas[0].corpo)
  assert.equal('exemplo' in corpo, true)
  assert.equal(corpo.exemplo, null)
  assert.equal(contagem.exemplosApagados, 1)
  assert.equal(avisos.length, 1)
  assert.ok(avisos[0].includes('NumeroRps'))
})

test('não gerenciado: PUT por outro atributo reenvia o exemplo existente', async (t) => {
  capturarConsole(t)
  const api = criarApiFalsaDeCampos([campoNaApi('NumeroRps', { exemplo: '579' })])

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { tipo: 'Numérico' })],
    idPorCaminho,
  )

  assert.equal(api.escritas.length, 1)
  const corpo = serializado(api.escritas[0].corpo)
  assert.equal(corpo.tipo, 'Numérico')
  assert.equal(corpo.exemplo, '579')
})

test('não gerenciado sem exemplo na API: o JSON do PUT leva a chave exemplo como null', async (t) => {
  capturarConsole(t)
  const semExemplo = campoNaApi('NumeroRps')
  const comNulo = campoNaApi('Serie', { exemplo: null })
  const api = criarApiFalsaDeCampos([semExemplo, comNulo])

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { tipo: 'Numérico' }), campoDaCarga('Serie', { tipo: 'Numérico' })],
    idPorCaminho,
  )

  assert.equal(api.escritas.length, 2)
  for (const { corpo } of api.escritas) {
    const texto = JSON.stringify(corpo)
    assert.ok(texto.includes('"exemplo":null'), texto)
  }
})

test('não gerenciado: diferença só em exemplo entre existente e desejado não escreve nada', async (t) => {
  capturarConsole(t)
  const api = criarApiFalsaDeCampos([campoNaApi('NumeroRps', { exemplo: '579' })])

  await sincronizarCamposDaVersao(api, 5, [campoDaCarga('NumeroRps')], idPorCaminho)

  assert.deepEqual(api.escritas, [])
})

test('chavesAlteradas só inclui exemplo quando o desejado o traz', () => {
  const existente = { ...COMPORTAMENTO, exemplo: '579' }

  assert.deepEqual(chavesAlteradas(existente, { ...COMPORTAMENTO }), [])
  assert.deepEqual(chavesAlteradas(existente, { ...COMPORTAMENTO, exemplo: null }), ['exemplo'])
  assert.deepEqual(chavesAlteradas(existente, { ...COMPORTAMENTO, exemplo: '579' }), [])
  assert.deepEqual(
    chavesAlteradas(existente, { ...COMPORTAMENTO, tipo: 'Data', exemplo: '2026-06-02' }),
    ['tipo', 'exemplo'],
  )
})

test('POST de criação: gerenciado leva exemplo; não gerenciado não leva a chave', async (t) => {
  capturarConsole(t)
  const api = criarApiFalsaDeCampos([])

  await sincronizarCamposDaVersao(
    api,
    5,
    [
      campoDaCarga('NumeroRps', { exemplo: '579' }),
      campoDaCarga('Serie', { exemplo: null }),
      campoDaCarga('Tipo'),
    ],
    idPorCaminho,
  )

  assert.deepEqual(
    api.escritas.map(({ metodo, rota }) => `${metodo} ${rota}`),
    [
      'POST /interno/versoes/5/campos',
      'POST /interno/versoes/5/campos',
      'POST /interno/versoes/5/campos',
    ],
  )
  const [numeroRps, serie, tipo] = api.escritas.map(({ corpo }) => serializado(corpo))
  assert.equal(numeroRps.campoLayoutId, 10)
  assert.equal(numeroRps.exemplo, '579')
  assert.equal(serie.exemplo, null)
  assert.equal('exemplo' in serie, true)
  assert.equal('exemplo' in tipo, false)
})

test('contagem: um PUT só de exemplo e um PUT com outro atributo', async (t) => {
  const { logs } = capturarConsole(t)
  const api = criarApiFalsaDeCampos([
    campoNaApi('NumeroRps', { exemplo: null }),
    campoNaApi('Serie', { exemplo: 'A' }),
  ])

  const contagem = await sincronizarCamposDaVersao(
    api,
    5,
    [
      campoDaCarga('NumeroRps', { exemplo: '579' }),
      campoDaCarga('Serie', { exemplo: 'A', tipo: 'Numérico' }),
    ],
    idPorCaminho,
  )

  assert.equal(contagem.atualizados, 2)
  assert.equal(contagem.atualizadosSoExemplo, 1)
  assert.ok(
    logs.includes(
      'campos da versão 5: 0 criados, 2 atualizados (1 só de exemplo, 1 com outro atributo) · ' +
        'exemplos: 1 definidos, 0 alterados, 0 apagados',
    ),
  )
})

test('contagem de exemplos definidos, alterados e apagados', async (t) => {
  const { logs } = capturarConsole(t)
  const api = criarApiFalsaDeCampos([
    campoNaApi('NumeroRps', { exemplo: null }),
    campoNaApi('Serie', { exemplo: 'B' }),
    campoNaApi('Tipo', { exemplo: 'D' }),
    campoNaApi('Cep', { exemplo: null }),
  ])

  const contagem = await sincronizarCamposDaVersao(
    api,
    5,
    [
      campoDaCarga('NumeroRps', { exemplo: '579' }),
      campoDaCarga('Serie', { exemplo: 'C' }),
      campoDaCarga('Tipo', { exemplo: null }),
      campoDaCarga('Cep', { exemplo: '29155', tipo: 'Numérico' }),
    ],
    idPorCaminho,
  )

  assert.deepEqual(contagem, {
    criados: 0,
    atualizados: 4,
    atualizadosSoExemplo: 3,
    exemplosDefinidos: 2,
    exemplosAlterados: 1,
    exemplosApagados: 1,
  })
  assert.ok(
    logs.includes(
      'campos da versão 5: 0 criados, 4 atualizados (3 só de exemplo, 1 com outro atributo) · ' +
        'exemplos: 2 definidos, 1 alterados, 1 apagados',
    ),
  )
})

test('em simulação imprime a lista de exemplos por campo; fora dela, não', async (t) => {
  const { logs } = capturarConsole(t)
  const existentes = [
    campoNaApi('NumeroRps', { exemplo: null }),
    campoNaApi('Serie', { exemplo: 'B' }),
  ]
  const campos = [
    campoDaCarga('NumeroRps', { exemplo: '579' }),
    campoDaCarga('Serie', { exemplo: null }),
  ]

  await sincronizarCamposDaVersao(
    criarApiFalsaDeCampos(existentes, { simulando: true }),
    5,
    campos,
    idPorCaminho,
  )

  assert.ok(logs.includes('  exemplo NumeroRps: ∅ → "579"'))
  assert.ok(logs.includes('  exemplo Serie: "B" → ∅'))

  logs.length = 0
  await sincronizarCamposDaVersao(
    criarApiFalsaDeCampos(existentes, { simulando: false }),
    5,
    campos,
    idPorCaminho,
  )

  assert.equal(logs.filter((linha) => linha.startsWith('  exemplo ')).length, 0)
  assert.ok(logs.some((linha) => linha.startsWith('campos da versão 5:')))
})

// Round-trip pelo cliente real, com o `fetch` falso: o que vai no corpo HTTP é o texto serializado.

function criarFetchFalso(camposDaApi) {
  const requisicoes = []
  async function enviar(url, opcoes) {
    requisicoes.push({ url, metodo: opcoes.method, corpo: opcoes.body })
    if (opcoes.method === 'GET') {
      return new Response(JSON.stringify({ data: { campos: camposDaApi } }), { status: 200 })
    }
    return new Response(JSON.stringify({ data: { id: 1 } }), { status: 200 })
  }
  return { enviar, requisicoes }
}

test('round-trip HTTP: o exemplo existente continua no corpo enviado quando outro atributo muda', async (t) => {
  capturarConsole(t)
  const { enviar, requisicoes } = criarFetchFalso([campoNaApi('NumeroRps', { exemplo: '579' })])
  const api = criarChamada({ obterToken: async () => 't', enviar, urlBase: 'http://api' })

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { tipo: 'Numérico' })],
    idPorCaminho,
  )

  const puts = requisicoes.filter(({ metodo }) => metodo === 'PUT')
  assert.equal(puts.length, 1)
  assert.equal(puts[0].url, 'http://api/interno/campos-versao/11')
  assert.equal(JSON.parse(puts[0].corpo).exemplo, '579')
  assert.equal(JSON.parse(puts[0].corpo).tipo, 'Numérico')
})

test('round-trip HTTP: sem exemplo na API o corpo enviado traz "exemplo":null', async (t) => {
  capturarConsole(t)
  const { enviar, requisicoes } = criarFetchFalso([campoNaApi('NumeroRps')])
  const api = criarChamada({ obterToken: async () => 't', enviar, urlBase: 'http://api' })

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { tipo: 'Numérico' })],
    idPorCaminho,
  )

  const puts = requisicoes.filter(({ metodo }) => metodo === 'PUT')
  assert.equal(puts.length, 1)
  assert.ok(puts[0].corpo.includes('"exemplo":null'), puts[0].corpo)
})

test('simulação de ponta a ponta: só o GET sai, o PUT vira resumo e criar interrompe', async (t) => {
  const { logs } = capturarConsole(t)
  const { enviar, requisicoes } = criarFetchFalso([
    campoNaApi('NumeroRps', { exemplo: null }),
    campoNaApi('Serie', { exemplo: null }),
  ])
  const api = criarChamada({
    obterToken: async () => 't',
    enviar,
    simular: true,
    urlBase: 'http://api',
  })

  await sincronizarCamposDaVersao(
    api,
    5,
    [campoDaCarga('NumeroRps', { exemplo: '579' }), campoDaCarga('Serie', { exemplo: 'A' })],
    idPorCaminho,
  )
  api.imprimirResumoDaSimulacao()

  assert.deepEqual(
    requisicoes.map(({ metodo }) => metodo),
    ['GET'],
  )
  assert.ok(logs.includes('  exemplo NumeroRps: ∅ → "579"'))
  assert.ok(logs.includes('  PUT /interno/campos-versao/{id}: 2'))

  await assert.rejects(
    sincronizarCamposDaVersao(api, 5, [campoDaCarga('Tipo', { exemplo: 'X' })], idPorCaminho),
    ErroDeSimulacao,
  )
  assert.equal(requisicoes.filter(({ metodo }) => metodo !== 'GET').length, 0)
})
