// Rodar com: node --test database/carga/conferir-exemplos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import {
  ErroDeConferencia,
  conferirContagem,
  contarExemplos,
  executarConferencia,
  lerArgumentos,
  lerPaginaPublicaDaVersao,
} from './conferir-exemplos.mjs'

const URL_BASE = 'http://api.teste/api/v1'

// Forma de VersaoProvedorPaginaDto dentro do ApiResponse, como o servidor serializa.
const PAGINA_PUBLICA = {
  data: {
    provedor: {
      slug: 'elotech',
      nome: 'Elotech',
      categoria: 'Proprio',
      identificadorInvoicy: 'ELOTECH',
    },
    versao: {
      slug: '2-03',
      rotulo: '2.03',
      verificadoEm: null,
      introducao: null,
      versaoLayoutRotulo: '8',
    },
    camposPorGrupo: [
      {
        grupo: 'RPS',
        grupoSlug: 'rps',
        campos: [
          {
            campoLayoutSlug: 'envio-rps-rpsnumero',
            nome: 'RPSNumero',
            caminhoXml: 'Envio/RPS/RPSNumero',
            significado: 'Número do RPS.',
            tipo: 'Numérico',
            tamanho: null,
            mascara: null,
            dominio: null,
            estadoPreenchimento: 'Obrigatorio',
            condicao: null,
            exemplo: '579',
            notaPreenchimento: null,
            destinoXml: 'InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Numero',
            notaTransformacao: null,
            ordem: 10,
            errosConhecidos: [],
          },
          {
            campoLayoutSlug: 'envio-rps-rpsserie',
            nome: 'RPSSerie',
            caminhoXml: 'Envio/RPS/RPSSerie',
            significado: 'Série do RPS.',
            tipo: 'Texto',
            tamanho: null,
            mascara: null,
            dominio: null,
            estadoPreenchimento: 'Obrigatorio',
            condicao: null,
            exemplo: null,
            notaPreenchimento: null,
            destinoXml: 'InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Serie',
            notaTransformacao: null,
            ordem: 20,
            errosConhecidos: [],
          },
        ],
      },
      {
        grupo: 'Serviço',
        grupoSlug: 'servico',
        campos: [
          {
            campoLayoutSlug: 'envio-rps-servico-discriminacao',
            nome: 'Discriminacao',
            caminhoXml: 'Envio/RPS/Servico/Discriminacao',
            significado: 'Descrição do serviço.',
            tipo: 'Texto',
            tamanho: null,
            mascara: null,
            dominio: null,
            estadoPreenchimento: 'Obrigatorio',
            condicao: null,
            exemplo: null,
            notaPreenchimento: null,
            destinoXml: 'InfDeclaracaoPrestacaoServico/Servico/Discriminacao',
            notaTransformacao: null,
            ordem: 30,
            errosConhecidos: [],
          },
        ],
      },
    ],
    totalCampos: 3,
    avisos: [],
    errosResumo: { total: 0, itens: [] },
    outrasVersoes: [],
    exemplos: [],
  },
}

function paginaComExemplos(exemplos) {
  return {
    data: {
      camposPorGrupo: [
        {
          grupo: 'Geral',
          grupoSlug: 'geral',
          campos: exemplos.map((exemplo, indice) => ({ nome: `Campo ${indice + 1}`, exemplo })),
        },
      ],
    },
  }
}

function respostaFalsa(status, corpo = {}) {
  return { ok: status >= 200 && status < 300, status, json: async () => corpo }
}

/** `fetch` falso: registra as chamadas e devolve sempre a mesma resposta. */
function enviarFalso(resposta) {
  const chamadas = []
  const enviar = async (url, opcoes) => {
    chamadas.push({ url, opcoes })
    return resposta
  }
  return { enviar, chamadas }
}

test('conta 1 campo com exemplo de 3, em 2 grupos', () => {
  assert.deepEqual(contarExemplos(PAGINA_PUBLICA), {
    total: 3,
    comExemplo: [{ nome: 'RPSNumero', exemplo: '579' }],
  })
})

test('exemplo vazio ou nulo não conta como exemplo', () => {
  const contagem = contarExemplos(paginaComExemplos(['Consultoria mensal', '', null, '0.00']))

  assert.equal(contagem.total, 4)
  assert.deepEqual(contagem.comExemplo, [
    { nome: 'Campo 1', exemplo: 'Consultoria mensal' },
    { nome: 'Campo 4', exemplo: '0.00' },
  ])
})

test('a página crua, sem o envelope data, é recusada nomeando data', () => {
  assert.throws(
    () => contarExemplos(PAGINA_PUBLICA.data),
    (erro) => erro instanceof ErroDeConferencia && /envelope "data"/.test(erro.message),
  )
})

test('o envelope de listagem, no lugar do da página, também é recusado', () => {
  assert.throws(
    () => contarExemplos({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }),
    /envelope "data"/,
  )
})

test('data sem camposPorGrupo é recusado nomeando a lista', () => {
  assert.throws(() => contarExemplos({ data: { totalCampos: 3 } }), /sem a lista "camposPorGrupo"/)
})

test('a conferência imprime a contagem e um campo por linha, e aprova só a quantidade exata', () => {
  const contagem = contarExemplos(PAGINA_PUBLICA)

  assert.deepEqual(conferirContagem(contagem, 1), {
    linhas: ['1 campos com exemplo de 3', 'RPSNumero · 579'],
    aprovado: true,
  })
  assert.equal(conferirContagem(contagem, 2).aprovado, false)
  assert.equal(conferirContagem(contagem, 0).aprovado, false)
})

test('lê a página pública com um GET, sem token', async () => {
  const { enviar, chamadas } = enviarFalso(respostaFalsa(200, PAGINA_PUBLICA))

  const pagina = await lerPaginaPublicaDaVersao({
    provedorSlug: 'elotech',
    versaoSlug: '2-03',
    enviar,
    urlBase: URL_BASE,
  })

  assert.equal(pagina, PAGINA_PUBLICA)
  assert.deepEqual(chamadas, [
    { url: 'http://api.teste/api/v1/provedores/elotech/versoes/2-03', opcoes: { method: 'GET' } },
  ])
})

test('versão despublicada (410) explica que a página só responde versão publicada', async () => {
  const { enviar } = enviarFalso(respostaFalsa(410))

  await assert.rejects(
    lerPaginaPublicaDaVersao({
      provedorSlug: 'elotech',
      versaoSlug: '2-03',
      enviar,
      urlBase: URL_BASE,
    }),
    {
      name: 'ErroDeConferencia',
      message:
        'A versão elotech/2-03 está despublicada (410): a página pública só responde versão ' +
        'publicada. Publique a versão antes de conferir.',
    },
  )
})

test('slug que não existe (404) pede para conferir os slugs', async () => {
  const { enviar } = enviarFalso(respostaFalsa(404))

  await assert.rejects(
    lerPaginaPublicaDaVersao({ provedorSlug: 'e-l', versaoSlug: '1-1', enviar, urlBase: URL_BASE }),
    {
      message: 'Provedor ou versão não encontrados (404): confira os slugs "e-l" e "1-1".',
    },
  )
})

test('limite de requisições (429) e outros status têm mensagem própria', async () => {
  const ler = (status) =>
    lerPaginaPublicaDaVersao({
      provedorSlug: 'elotech',
      versaoSlug: '2-03',
      enviar: enviarFalso(respostaFalsa(status)).enviar,
      urlBase: URL_BASE,
    })

  await assert.rejects(ler(429), {
    message: 'A API limitou as requisições (429). Espere um pouco e tente de novo.',
  })
  await assert.rejects(ler(500), { message: 'A API respondeu 500 para a versão elotech/2-03.' })
})

test('API fora do ar vira erro de conferência com o endereço', async () => {
  const enviar = async () => {
    throw new TypeError('fetch failed')
  }

  await assert.rejects(
    lerPaginaPublicaDaVersao({
      provedorSlug: 'elotech',
      versaoSlug: '2-03',
      enviar,
      urlBase: URL_BASE,
    }),
    {
      name: 'ErroDeConferencia',
      message: 'Não foi possível falar com a API em http://api.teste/api/v1: fetch failed',
    },
  )
})

test('argumentos: dois slugs e a quantidade esperada, inteira e sem sobra', () => {
  assert.deepEqual(lerArgumentos(['elotech', '2-03', '38']), {
    provedorSlug: 'elotech',
    versaoSlug: '2-03',
    esperado: 38,
  })
  assert.equal(lerArgumentos(['elotech', '2-03', '0']).esperado, 0)
  for (const invalido of [
    [],
    ['elotech', '2-03'],
    ['elotech', '2-03', 'muitos'],
    ['elotech', '2-03', '-1'],
    ['elotech', '2-03', '3.5'],
    ['elotech', '2-03', '38', 'sobra'],
  ]) {
    assert.throws(() => lerArgumentos(invalido), /Uso: node database\/carga\/conferir-exemplos.mjs/)
  }
})

function executar(argumentos, resposta) {
  const { enviar, chamadas } = enviarFalso(resposta)
  const saida = []
  const erros = []
  const codigo = executarConferencia({
    argumentos,
    enviar,
    urlBase: URL_BASE,
    imprimir: (linha) => saida.push(linha),
    imprimirErro: (linha) => erros.push(linha),
  })
  return { codigo, saida, erros, chamadas }
}

test('contagem igual à esperada: imprime os campos e sai com 0', async () => {
  const { codigo, saida, erros } = executar(
    ['elotech', '2-03', '1'],
    respostaFalsa(200, PAGINA_PUBLICA),
  )

  assert.equal(await codigo, 0)
  assert.deepEqual(saida, ['1 campos com exemplo de 3', 'RPSNumero · 579'])
  assert.deepEqual(erros, [])
})

test('contagem diferente da esperada: imprime os campos, avisa e sai com 1', async () => {
  const { codigo, saida, erros } = executar(
    ['elotech', '2-03', '38'],
    respostaFalsa(200, PAGINA_PUBLICA),
  )

  assert.equal(await codigo, 1)
  assert.deepEqual(saida, ['1 campos com exemplo de 3', 'RPSNumero · 579'])
  assert.deepEqual(erros, ['Esperados 38 campos com exemplo em elotech/2-03.'])
})

test('versão despublicada: sai com 1 e a mensagem diz o motivo, sem imprimir contagem', async () => {
  const { codigo, saida, erros } = executar(['elotech', '2-03', '38'], respostaFalsa(410))

  assert.equal(await codigo, 1)
  assert.deepEqual(saida, [])
  assert.equal(erros.length, 1)
  assert.match(erros[0], /está despublicada \(410\)/)
})

test('argumentos inválidos: sai com 1 sem chamar a API', async () => {
  const { codigo, erros, chamadas } = executar(['elotech'], respostaFalsa(200, PAGINA_PUBLICA))

  assert.equal(await codigo, 1)
  assert.match(erros[0], /^Uso: /)
  assert.deepEqual(chamadas, [])
})

test('página crua na resposta: sai com 1 nomeando o envelope', async () => {
  const { codigo, erros } = executar(
    ['elotech', '2-03', '1'],
    respostaFalsa(200, PAGINA_PUBLICA.data),
  )

  assert.equal(await codigo, 1)
  assert.match(erros[0], /envelope "data"/)
})
