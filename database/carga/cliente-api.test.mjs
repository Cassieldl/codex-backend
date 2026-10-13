// Rodar com: node --test database/carga/cliente-api.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import {
  ErroDeSimulacao,
  criarChamada,
  criarClienteApi,
  executarCarga,
  simulacaoLigada,
} from './cliente-api.mjs'

/** `fetch` falso: registra cada requisição e responde 200 com um corpo mínimo. */
function criarFetchFalso() {
  const requisicoes = []
  async function enviar(url, opcoes) {
    requisicoes.push({ url, metodo: opcoes.method, corpo: opcoes.body })
    return new Response(JSON.stringify({ data: { id: 1 } }), {
      status: 200,
      headers: { etag: '"7"' },
    })
  }
  return { enviar, requisicoes }
}

function criarChamadaFalsa({ simular }) {
  const { enviar, requisicoes } = criarFetchFalso()
  const api = criarChamada({
    obterToken: async () => 'token',
    enviar,
    simular,
    urlBase: 'http://api',
  })
  return { api, requisicoes }
}

function capturarLogs(t) {
  const logs = []
  t.mock.method(console, 'log', (...partes) => logs.push(partes.join(' ')))
  return logs
}

test('simulando: o GET chega ao fetch; o PUT não chega e vira registro', async () => {
  const { api, requisicoes } = criarChamadaFalsa({ simular: true })

  const leitura = await api.chamar('GET', '/interno/versoes/3/campos')
  const escrita = await api.chamar('PUT', '/interno/campos-versao/7', { exemplo: '579' }, '12')

  assert.deepEqual(requisicoes, [
    { url: 'http://api/interno/versoes/3/campos', metodo: 'GET', corpo: undefined },
  ])
  assert.deepEqual(leitura.dados, { id: 1 })
  assert.deepEqual(escrita, { dados: { exemplo: '579' }, etag: null })
})

test('simulando: POST e DELETE lançam ErroDeSimulacao e não chegam ao fetch', async (t) => {
  capturarLogs(t)
  const { api, requisicoes } = criarChamadaFalsa({ simular: true })

  await assert.rejects(
    api.chamar('POST', '/interno/versoes/3/campos', { campoLayoutId: 1 }),
    (erro) => {
      assert.ok(erro instanceof ErroDeSimulacao)
      assert.equal(
        erro.message,
        'Simulação interrompida: a carga criaria /interno/versoes/3/campos. ' +
          'O esperado nesta demanda é só PUT de exemplo. Nada foi gravado.',
      )
      return true
    },
  )
  await assert.rejects(api.chamar('DELETE', '/interno/avisos/9', undefined, '2'), (erro) => {
    assert.ok(erro instanceof ErroDeSimulacao)
    assert.ok(
      erro.message.startsWith('Simulação interrompida: a carga removeria /interno/avisos/9.'),
    )
    return true
  })

  assert.deepEqual(requisicoes, [])
})

test('simulando: o resumo sai antes da interrupção e inclui a escrita que interrompeu', async (t) => {
  const logs = capturarLogs(t)
  const { api } = criarChamadaFalsa({ simular: true })

  await api.chamar('GET', '/interno/versoes/3/campos')
  await api.chamar('PUT', '/interno/campos-versao/7', { exemplo: 'a' })
  await assert.rejects(api.chamar('POST', '/interno/erros', {}), ErroDeSimulacao)

  assert.ok(logs.includes('leituras reais (GET): 1'))
  assert.ok(logs.includes('escritas que a carga faria: 2'))
  assert.ok(logs.includes('  PUT /interno/campos-versao/{id}: 1'))
  assert.ok(logs.includes('  POST /interno/erros: 1'))
})

test('sem simular: os quatro verbos chegam ao fetch e o resumo não imprime nada', async (t) => {
  const logs = capturarLogs(t)
  const { api, requisicoes } = criarChamadaFalsa({ simular: false })

  await api.chamar('GET', '/interno/a')
  await api.chamar('PUT', '/interno/b/1', { x: 1 }, '5')
  await api.chamar('POST', '/interno/c', { y: 2 })
  await api.chamar('DELETE', '/interno/d/2', undefined, '6')
  api.imprimirResumoDaSimulacao()

  assert.deepEqual(
    requisicoes.map(({ metodo, url }) => `${metodo} ${url}`),
    [
      'GET http://api/interno/a',
      'PUT http://api/interno/b/1',
      'POST http://api/interno/c',
      'DELETE http://api/interno/d/2',
    ],
  )
  assert.equal(requisicoes[1].corpo, '{"x":1}')
  assert.equal(api.simulando, false)
  assert.deepEqual(logs, [])
})

test('resumo agrupa por método e rota com ids trocados; query não vira rota', async (t) => {
  const logs = capturarLogs(t)
  const { api } = criarChamadaFalsa({ simular: true })

  await api.chamar('GET', '/interno/erros?provedor=x&pagina=1')
  await api.chamar('GET', '/interno/erros?provedor=x&pagina=2')
  await api.chamar('GET', '/interno/versoes/3/campos')
  await api.chamar('PUT', '/interno/campos-versao/7', {})
  await api.chamar('PUT', '/interno/campos-versao/8', {})
  await api.chamar('PUT', '/interno/campos-versao/9?forcar=1', {})
  await api.chamar('PUT', '/interno/avisos/3', {})
  api.imprimirResumoDaSimulacao()

  assert.deepEqual(logs, [
    '\n== SIMULAÇÃO: nenhuma escrita foi enviada ==',
    'leituras reais (GET): 3',
    'escritas que a carga faria: 4',
    '  PUT /interno/campos-versao/{id}: 3',
    '  PUT /interno/avisos/{id}: 1',
  ])
})

test('simulacaoLigada só aceita o valor exato "1"', () => {
  assert.equal(simulacaoLigada({ CODEX_CARGA_SIMULAR: '1' }), true)
  for (const valor of ['0', 'true', '', 'sim', ' 1', undefined]) {
    assert.equal(simulacaoLigada({ CODEX_CARGA_SIMULAR: valor }), false, `valor ${valor}`)
  }
})

test('criarClienteApi lê CODEX_CARGA_SIMULAR do ambiente e só liga com "1"', async () => {
  const anterior = { token: process.env.CODEX_TOKEN, simular: process.env.CODEX_CARGA_SIMULAR }
  process.env.CODEX_TOKEN = 'token-de-teste'
  try {
    const resultados = {}
    for (const valor of ['1', '0', 'true', '']) {
      process.env.CODEX_CARGA_SIMULAR = valor
      resultados[valor] = (await criarClienteApi()).simulando
    }
    delete process.env.CODEX_CARGA_SIMULAR
    resultados.ausente = (await criarClienteApi()).simulando

    assert.deepEqual(resultados, { 1: true, 0: false, true: false, '': false, ausente: false })
  } finally {
    restaurarVariavel('CODEX_TOKEN', anterior.token)
    restaurarVariavel('CODEX_CARGA_SIMULAR', anterior.simular)
  }
})

function restaurarVariavel(nome, valor) {
  if (valor === undefined) delete process.env[nome]
  else process.env[nome] = valor
}

test('executarCarga: interrupção da simulação vira mensagem e exit code 1; outro erro propaga', async (t) => {
  const mensagens = []
  t.mock.method(console, 'error', (...partes) => mensagens.push(partes.join(' ')))
  const codigoAnterior = process.exitCode

  try {
    await executarCarga(async () => {
      throw new ErroDeSimulacao('Simulação interrompida: teste.')
    })
    assert.equal(process.exitCode, 1)
    assert.deepEqual(mensagens, ['\nSimulação interrompida: teste.'])

    process.exitCode = undefined
    await assert.rejects(
      executarCarga(async () => {
        throw new Error('falha real')
      }),
      /falha real/,
    )
    assert.equal(process.exitCode, undefined)
  } finally {
    process.exitCode = codigoAnterior
  }
})
