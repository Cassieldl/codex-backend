// Rodar com: node --test database/carga/emissor-nacional/sincronizar-campos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { ErroDeSimulacao, criarChamada } from '../cliente-api.mjs'
import {
  chavesAlteradas,
  sincronizarCamposDaVersao,
  sincronizarVocabulario,
} from './sincronizar-campos.mjs'

const ID_DO_LAYOUT = new Map([
  ['8', 1],
  ['9', 2],
])

/** API falsa: responde ao GET de campos pela versão de origem e registra todas as escritas. */
function criarApiFalsa(existentes) {
  const escritas = []
  return {
    escritas,
    async chamar(metodo, rota, corpo, etag) {
      if (metodo === 'GET') {
        const versaoLayoutId = Number(rota.match(/versoes\/(\d+)\/campos/)[1])
        return { dados: existentes.filter((campo) => campo.versaoLayoutId === versaoLayoutId) }
      }
      escritas.push({ metodo, rota, corpo, etag })
      // Como a API: o criado volta com o que foi enviado, ativo.
      return {
        dados: { ...corpo, id: 900 + escritas.length, estado: 'Ativo', versaoConcorrencia: 70 },
      }
    },
  }
}

const existenteNo8 = (caminhoXml, grupo, ordem, extra = {}) => ({
  id: 7,
  versaoLayoutId: 1,
  caminhoXml,
  grupo,
  ordem,
  estado: 'Ativo',
  versaoConcorrencia: 41,
  ...extra,
})

function plano(campos, porVersao = {}) {
  return {
    campos: campos.map((campo) => ({
      nome: campo.caminhoXml.split('/').at(-1),
      significado: 'Significado.',
      origem: '8',
      descontinuadoEm: null,
      ...campo,
    })),
    porVersao: new Map(
      Object.entries(porVersao).map(([rotulo, caminhos]) => [
        rotulo,
        caminhos.map((caminhoXml) => ({
          caminhoXml,
          caminhoCompleto: `Envio/RPS/${caminhoXml.slice(2)}`,
        })),
      ]),
    ),
  }
}

test('existente com grupo antigo é reposicionado com If-Match, sem tocar nome nem significado', async () => {
  const caminho = '//Servico/Valores/IBSCBS/IBS/ValpIBSUF'
  const api = criarApiFalsa([existenteNo8(caminho, 'Servico', 3)])

  await sincronizarVocabulario(
    api,
    ID_DO_LAYOUT,
    plano([{ caminhoXml: caminho, grupo: 'Servico/Valores/IBSCBS/IBS', ordem: 3 }]),
  )

  assert.deepEqual(api.escritas, [
    {
      metodo: 'PUT',
      rota: '/interno/campos-layout/7',
      corpo: { grupo: 'Servico/Valores/IBSCBS/IBS', ordem: 3 },
      etag: '41',
    },
  ])
})

test('rodar de novo com tudo no lugar não escreve nada', async () => {
  const api = criarApiFalsa([existenteNo8('//Tomador/TomaCNPJ', 'Tomador', 2)])

  await sincronizarVocabulario(
    api,
    ID_DO_LAYOUT,
    plano([{ caminhoXml: '//Tomador/TomaCNPJ', grupo: 'Tomador', ordem: 2 }]),
  )

  assert.deepEqual(api.escritas, [])
})

test('campo que já existe no 8 e segue no 9 mantém a origem 8: não é recriado', async () => {
  const api = criarApiFalsa([existenteNo8('//Tomador/TomaCNPJ', 'Tomador', 2)])

  const idPorCaminho = await sincronizarVocabulario(
    api,
    ID_DO_LAYOUT,
    plano([{ caminhoXml: '//Tomador/TomaCNPJ', grupo: 'Tomador', ordem: 2 }], {
      8: ['//Tomador/TomaCNPJ'],
      9: ['//Tomador/TomaCNPJ'],
    }),
  )

  assert.deepEqual(api.escritas, [])
  assert.equal(idPorCaminho.get('9').get('Envio/RPS/Tomador/TomaCNPJ'), 7)
})

test('campo só do 9 nasce na versão 9 e entra só no mapa do 9', async () => {
  const api = criarApiFalsa([])

  const idPorCaminho = await sincronizarVocabulario(
    api,
    ID_DO_LAYOUT,
    plano(
      [
        {
          caminhoXml: '//Servico/Valores/nroDUIMP',
          grupo: 'Servico/Valores',
          ordem: 1,
          origem: '9',
        },
      ],
      {
        8: [],
        9: ['//Servico/Valores/nroDUIMP'],
      },
    ),
  )

  assert.equal(api.escritas.length, 1)
  assert.equal(api.escritas[0].metodo, 'POST')
  assert.equal(api.escritas[0].rota, '/interno/layout/versoes/2/campos')
  assert.equal(idPorCaminho.get('9').get('Envio/RPS/Servico/Valores/nroDUIMP'), 901)
  assert.equal(idPorCaminho.get('8').size, 0)
})

test('campo do 8 que saiu do 9 é descontinuado na versão 9, uma vez só', async () => {
  const caminho = '//Servico/Valores/nDI'
  const plano9 = plano([
    { caminhoXml: caminho, grupo: 'Servico/Valores', ordem: 5, descontinuadoEm: '9' },
  ])

  const api = criarApiFalsa([existenteNo8(caminho, 'Servico/Valores', 5)])
  await sincronizarVocabulario(api, ID_DO_LAYOUT, plano9)
  assert.deepEqual(api.escritas, [
    {
      metodo: 'PUT',
      rota: '/interno/campos-layout/7',
      corpo: { estado: 'Descontinuado', versaoLayoutDescontinuadoId: 2 },
      etag: '41',
    },
  ])

  const jaDescontinuado = existenteNo8(caminho, 'Servico/Valores', 5, {
    estado: 'Descontinuado',
    versaoLayoutDescontinuadoId: 2,
  })
  const apiDeNovo = criarApiFalsa([jaDescontinuado])
  await sincronizarVocabulario(apiDeNovo, ID_DO_LAYOUT, plano9)
  assert.deepEqual(apiDeNovo.escritas, [])
})

test('campo criado agora e já fora da versão mais nova nasce na origem e é descontinuado em seguida', async () => {
  const api = criarApiFalsa([])

  await sincronizarVocabulario(
    api,
    ID_DO_LAYOUT,
    plano([
      {
        caminhoXml: '//Servico/Valores/nRE',
        grupo: 'Servico/Valores',
        ordem: 1,
        descontinuadoEm: '9',
      },
    ]),
  )

  assert.deepEqual(
    api.escritas.map(({ metodo, rota, corpo, etag }) => ({
      metodo,
      rota,
      estado: corpo.estado,
      etag,
    })),
    [
      {
        metodo: 'POST',
        rota: '/interno/layout/versoes/1/campos',
        estado: undefined,
        etag: undefined,
      },
      { metodo: 'PUT', rota: '/interno/campos-layout/901', estado: 'Descontinuado', etag: '70' },
    ],
  )
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
