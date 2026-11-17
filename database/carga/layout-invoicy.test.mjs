// Rodar com: node --test database/carga/layout-invoicy.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { readFile } from 'node:fs/promises'

import {
  ROTULO_LAYOUT_DOS_PROVEDORES,
  arquivoDaEstrutura,
  descricaoDaVersao,
  lerLayoutsInvoicy,
  mudancasDaLista,
  planejarEstados,
  planejarVocabularioMultiversao,
  sincronizarLayoutInvoicy,
  VERSOES_LAYOUT_INVOICY,
} from './layout-invoicy.mjs'
import { lerMaterialDoLayout, lerVocabularioInvoicy } from './vocabulario-invoicy.mjs'

const campo = (caminhoXml, nome = caminhoXml.split('/').at(-1)) => ({
  caminhoXml,
  caminhoCompleto: `Envio/RPS/${caminhoXml.slice(2)}`,
  nome,
  significado: `Significado de ${nome}.`,
  grupo: 'RPS',
})

test('plano multiversão: mantido nasce no 8, novo nasce no 9, saído fica descontinuado no 9', () => {
  const { campos } = planejarVocabularioMultiversao([
    { rotulo: '8', campos: [campo('//A'), campo('//nDI'), campo('//C')] },
    { rotulo: '9', campos: [campo('//A'), campo('//nroDUIMP'), campo('//C')] },
  ])

  assert.deepEqual(
    campos.map(({ caminhoXml, origem, descontinuadoEm, ordem }) => [
      caminhoXml,
      origem,
      descontinuadoEm,
      ordem,
    ]),
    [
      ['//A', '8', null, 1],
      ['//nDI', '8', '9', 2],
      ['//nroDUIMP', '9', null, 3],
      ['//C', '8', null, 4],
    ],
  )
})

test('texto do campo mantido vem da versão mais nova, que corrigiu descrições trocadas', () => {
  const no8 = { ...campo('//ConstCivil/ObraCPFNF'), significado: 'CNPJ do fornecedor.' }
  const no9 = { ...campo('//ConstCivil/ObraCPFNF'), significado: 'CPF do fornecedor.' }

  const { campos } = planejarVocabularioMultiversao([
    { rotulo: '8', campos: [no8] },
    { rotulo: '9', campos: [no9] },
  ])

  assert.equal(campos[0].significado, 'CPF do fornecedor.')
})

test('campo que sai e volta entre versões interrompe o plano: o caminho é imutável', () => {
  assert.throws(
    () =>
      planejarVocabularioMultiversao([
        { rotulo: '7', campos: [campo('//A')] },
        { rotulo: '8', campos: [] },
        { rotulo: '9', campos: [campo('//A')] },
      ]),
    /sai e volta/,
  )
})

test('estados: a 8 Vigente passa a Anterior antes de a 9 virar Vigente', () => {
  const passos = planejarEstados([
    { rotulo: '8', estado: 'Vigente' },
    { rotulo: '9', estado: 'Prevista' },
  ])

  assert.deepEqual(passos, [
    { indice: 0, estado: 'Anterior' },
    { indice: 1, estado: 'Vigente' },
  ])
})

test('estados: banco novo com as duas Previstas leva a 8 por Vigente até Anterior', () => {
  const passos = planejarEstados([
    { rotulo: '8', estado: 'Prevista' },
    { rotulo: '9', estado: 'Prevista' },
  ])

  assert.deepEqual(passos, [
    { indice: 0, estado: 'Vigente' },
    { indice: 0, estado: 'Anterior' },
    { indice: 1, estado: 'Vigente' },
  ])
  assert.deepEqual(
    planejarEstados([
      { rotulo: '8', estado: 'Anterior' },
      { rotulo: '9', estado: 'Vigente' },
    ]),
    [],
  )
})

test('nota da versão 9: as linhas da NT 009 da série 3.x, sem a que manda usar o layout 8', () => {
  const descricao = descricaoDaVersao('9', [
    { versao: '2.98.0.4', descricao: 'Ajustes conforme NT007' },
    {
      versao: '3.1.0.0',
      descricao:
        'Ajustes conforme NT009 (após a primeira quinzena de agosto/2026, utilizar layout na versão 8, com ajustes no grupo Imovel)',
    },
    { versao: '3.3.0.0', descricao: 'Ajustes conforme NT009 v1.01' },
  ])

  assert.match(descricao, /versão 9/)
  assert.match(descricao, /InvoiCy 3\.3\.0\.0: Ajustes conforme NT009 v1\.01\./)
  assert.doesNotMatch(descricao, /NT007/)
  assert.doesNotMatch(descricao, /3\.1\.0\.0|versão 8/)
})

test('com os arquivos reais: todo campo do 8 segue mapeado na 8, e as mudanças da 9 aparecem', async () => {
  const { versoes } = await lerLayoutsInvoicy()
  const plano = planejarVocabularioMultiversao(versoes)
  const { vocabulario: vocabulario8 } = await lerVocabularioInvoicy(ROTULO_LAYOUT_DOS_PROVEDORES)

  const mapeados8 = new Set(plano.porVersao.get('8').map((item) => item.caminhoCompleto))
  assert.ok(vocabulario8.every((item) => mapeados8.has(item.caminhoCompleto)))

  const porCaminho = new Map(plano.campos.map((item) => [item.caminhoXml, item]))
  assert.equal(porCaminho.get('//Servico/Valores/nDI').descontinuadoEm, '9')
  assert.equal(porCaminho.get('//Servico/Valores/ValServEst').descontinuadoEm, '9')
  assert.equal(porCaminho.get('//Servico/Valores/nroDUIMP').origem, '9')
  assert.equal(porCaminho.get('//RPSSubs/SubsNumero').origem, '9')
  assert.equal(porCaminho.get('//Tomador/TomaCNPJ').origem, '8')
  assert.equal(porCaminho.get('//Tomador/TomaCNPJ').descontinuadoEm, null)
})

/** Caminhos de uma versão, da estrutura gerada do XSD e da planilha, como caminho canônico. */
async function caminhosCanonicosDaVersao(rotulo) {
  const estrutura = JSON.parse(await readFile(arquivoDaEstrutura(rotulo), 'utf8'))
  const { planilha } = await lerMaterialDoLayout(rotulo)
  const caminhos = []
  const visitar = (no) => {
    caminhos.push(no.caminho)
    no.filhos.forEach(visitar)
  }
  visitar(estrutura.xml)
  caminhos.push(...planilha.tags.map(({ caminho }) => caminho))
  return new Set(caminhos.map((caminho) => `//${caminho.replace(/^Envio\/(RPS\/)?/, '')}`))
}

test('mudanças da 9: antes existe na 8, depois existe na 9, e cada tipo leva os caminhos que pede', async () => {
  const [versao8, versao9] = VERSOES_LAYOUT_INVOICY
  const [caminhos8, caminhos9] = await Promise.all([
    caminhosCanonicosDaVersao(versao8.rotulo),
    caminhosCanonicosDaVersao(versao9.rotulo),
  ])
  const pedeAntes = new Set(['mudouDeLugar', 'renomeado', 'saiu', 'alterado'])
  const pedeDepois = new Set(['grupoNovo', 'campoNovo', 'mudouDeLugar', 'renomeado', 'alterado'])

  for (const { tipo, antes, depois, descricao } of versao9.mudancas) {
    assert.ok(descricao.length > 0 && descricao.length <= 500, descricao)
    assert.equal(antes !== undefined, pedeAntes.has(tipo), `${tipo} ${antes}`)
    assert.equal(depois !== undefined, pedeDepois.has(tipo), `${tipo} ${depois}`)
    if (antes) assert.ok(caminhos8.has(antes), `${antes} não existe na 8`)
    if (depois) assert.ok(caminhos9.has(depois), `${depois} não existe na 9`)
    if (tipo === 'grupoNovo' || tipo === 'campoNovo')
      // RPSSubs já existia vazio na 8; a 9 é a primeira em que ele é um grupo.
      assert.ok(!caminhos8.has(depois) || depois === '//RPSSubs', `${depois} já existia na 8`)
    if (tipo === 'saiu') assert.ok(!caminhos9.has(antes), `${antes} ainda existe na 9`)
  }

  const renomeados = versao9.mudancas
    .filter(({ tipo }) => tipo === 'renomeado')
    .map(({ antes, depois }) => [antes.split('/').at(-1), depois.split('/').at(-1)])
  assert.deepEqual(renomeados, [
    ['nDI', 'nroDUIMP'],
    ['nRE', 'nroDUE'],
    ['ItermIE', 'IntermIE'],
  ])
  assert.equal(versao8.mudancas, undefined)
})

test('lista de mudanças: igual não escreve, diferente vai inteira, sem configuração não toca', () => {
  const configurada = {
    mudancas: [
      { tipo: 'saiu', antes: '//A', descricao: 'A saiu.' },
      { tipo: 'campoNovo', depois: '//B', descricao: 'B entrou.' },
    ],
  }
  const naApi = [
    { tipo: 'saiu', antes: '//A', depois: null, descricao: 'A saiu.' },
    { tipo: 'campoNovo', antes: null, depois: '//B', descricao: 'B entrou.' },
  ]

  assert.deepEqual(mudancasDaLista(configurada, { mudancas: naApi }), {})
  assert.deepEqual(mudancasDaLista(configurada, { mudancas: naApi.slice(0, 1) }), {
    mudancas: naApi,
  })
  assert.deepEqual(mudancasDaLista(configurada, { mudancas: [...naApi].reverse() }), {
    mudancas: naApi,
  })
  assert.deepEqual(mudancasDaLista(configurada), { mudancas: naApi })
  assert.deepEqual(mudancasDaLista({}, { mudancas: naApi }), {})
})

/**
 * API falsa com as regras que importam aqui: versão nasce Prevista, estado só anda para frente,
 * uma Vigente por vez, caminho de campo único e If-Match conferido em toda escrita.
 */
function criarApiDeLayout(versoes, campos) {
  let proximoId = 1000
  let proximaConcorrencia = 1
  const escritas = []
  const versaoPorId = (id) => versoes.find((versao) => versao.id === Number(id))
  const exigirIfMatch = (recurso, etag) =>
    assert.equal(etag, String(recurso.versaoConcorrencia), 'If-Match velho')
  const transicoes = { Prevista: 'Vigente', Vigente: 'Anterior' }

  async function chamar(metodo, rota, corpo, etag) {
    if (metodo !== 'GET') escritas.push(`${metodo} ${rota}`)
    let id

    if (rota.startsWith('/interno/layout/versoes?'))
      return { dados: { items: structuredClone(versoes) } }

    if (metodo === 'POST' && rota === '/interno/layout/versoes') {
      const versao = {
        id: proximoId++,
        ...corpo,
        estado: 'Prevista',
        ordem: Math.max(0, ...versoes.map((item) => item.ordem)) + 1,
        versaoConcorrencia: proximaConcorrencia++,
      }
      versoes.push(versao)
      return { dados: { ...versao } }
    }

    if ((id = rota.match(/^\/interno\/layout\/versoes\/(\d+)$/)?.[1])) {
      const versao = versaoPorId(id)
      exigirIfMatch(versao, etag)
      for (const chave of [
        'descricao',
        'normaOrigem',
        'homologacaoDesde',
        'liberadaNoInvoicyEm',
        'obrigatoriaEm',
        'mudancas',
      ])
        if (corpo[chave] !== undefined) versao[chave] = corpo[chave]
      if (corpo.estado !== undefined) {
        assert.equal(transicoes[versao.estado], corpo.estado, `${versao.estado} → ${corpo.estado}`)
        if (corpo.estado === 'Vigente')
          assert.ok(!versoes.some((item) => item.estado === 'Vigente'), 'duas Vigentes')
        versao.estado = corpo.estado
      }
      versao.versaoConcorrencia = proximaConcorrencia++
      return { dados: { ...versao } }
    }

    if ((id = rota.match(/^\/interno\/layout\/versoes\/(\d+)\/estrutura$/)?.[1])) {
      const versao = versaoPorId(id)
      exigirIfMatch(versao, etag)
      Object.assign(versao, { estrutura: corpo, versaoConcorrencia: proximaConcorrencia++ })
      return { dados: { ...versao } }
    }

    if ((id = rota.match(/^\/interno\/layout\/versoes\/(\d+)\/campos$/)?.[1])) {
      if (metodo === 'GET')
        return {
          dados: structuredClone(campos.filter((campo) => campo.versaoLayoutId === Number(id))),
        }
      assert.ok(!campos.some((campo) => campo.caminhoXml === corpo.caminhoXml), 'caminho repetido')
      const campo = {
        id: proximoId++,
        versaoLayoutId: Number(id),
        ...corpo,
        estado: 'Ativo',
        versaoLayoutDescontinuadoId: null,
        versaoConcorrencia: proximaConcorrencia++,
      }
      campos.push(campo)
      return { dados: { ...campo } }
    }

    if ((id = rota.match(/^\/interno\/campos-layout\/(\d+)$/)?.[1])) {
      const campo = campos.find((item) => item.id === Number(id))
      exigirIfMatch(campo, etag)
      if (corpo.estado === 'Descontinuado')
        assert.ok(
          versaoPorId(corpo.versaoLayoutDescontinuadoId).ordem >
            versaoPorId(campo.versaoLayoutId).ordem,
        )
      Object.assign(campo, corpo, { versaoConcorrencia: proximaConcorrencia++ })
      return { dados: { ...campo } }
    }

    throw new Error(`rota não prevista na API falsa: ${metodo} ${rota}`)
  }

  return { chamar, escritas, simulando: false }
}

test('carga sobre o banco de hoje: 9 Vigente, 8 Anterior, ids do 8 intactos, estrutura nas duas e nada a fazer na segunda vez', async (t) => {
  t.mock.method(console, 'log', () => {})
  t.mock.method(console, 'warn', () => {})

  const { vocabulario: vocabulario8 } = await lerVocabularioInvoicy('8')
  const versoes = [{ id: 1, rotulo: '8', estado: 'Vigente', ordem: 1, versaoConcorrencia: 500 }]
  const campos = vocabulario8.map((campo, indice) => ({
    id: indice + 1,
    versaoLayoutId: 1,
    nome: campo.nome,
    caminhoXml: campo.caminhoXml,
    significado: campo.significado,
    grupo: campo.grupo,
    ordem: campo.ordem,
    estado: 'Ativo',
    versaoLayoutDescontinuadoId: null,
    versaoConcorrencia: 600 + indice,
  }))
  const idOriginal = new Map(campos.map((campo) => [campo.caminhoXml, campo.id]))
  const api = criarApiDeLayout(versoes, campos)

  const { idPorRotulo, idPorCaminhoPorRotulo } = await sincronizarLayoutInvoicy(api)

  const [versao8, versao9] = versoes
  assert.deepEqual([versao8.estado, versao9.estado], ['Anterior', 'Vigente'])
  assert.deepEqual(
    [versao8.descricao, versao9.descricao],
    VERSOES_LAYOUT_INVOICY.map(({ descricao }) => descricao),
  )
  assert.deepEqual(
    [
      versao9.normaOrigem,
      versao9.homologacaoDesde,
      versao9.liberadaNoInvoicyEm,
      versao9.obrigatoriaEm,
    ],
    ['NT 009 v1.01', '2026-10-01', '2026-10-22', '2026-12-01'],
  )
  assert.equal(versao8.normaOrigem, undefined, 'a 8 não tem calendário configurado')
  assert.equal(versao8.mudancas, undefined, 'a 8 não tem mudanças configuradas')
  assert.equal(versao9.mudancas.length, VERSOES_LAYOUT_INVOICY[1].mudancas.length)
  assert.deepEqual(versao9.mudancas.find(({ tipo }) => tipo === 'saiu'), {
    tipo: 'saiu',
    antes: '//Servico/Valores/ValServEst',
    depois: null,
    descricao: 'O valor do serviço em moeda estrangeira saiu do layout.',
  })
  assert.ok(versao8.estrutura.xml && versao8.estrutura.json)
  assert.ok(versao9.estrutura.xml && versao9.estrutura.json)
  assert.notDeepEqual(versao8.estrutura, versao9.estrutura)

  const do8 = idPorCaminhoPorRotulo.get('8')
  assert.equal(do8.size, vocabulario8.length)
  for (const campo of vocabulario8)
    assert.equal(do8.get(campo.caminhoCompleto), idOriginal.get(campo.caminhoXml))

  const novos = campos.filter((campo) => campo.versaoLayoutId === idPorRotulo.get('9'))
  const descontinuados = campos.filter((campo) => campo.estado === 'Descontinuado')
  assert.ok(novos.some((campo) => campo.caminhoXml === '//Servico/Valores/nroDUIMP'))
  assert.ok(
    descontinuados.every((campo) => campo.versaoLayoutDescontinuadoId === idPorRotulo.get('9')),
  )
  assert.ok(descontinuados.some((campo) => campo.caminhoXml === '//Servico/Valores/nDI'))

  const escritasDaPrimeira = api.escritas.length
  await sincronizarLayoutInvoicy(api)
  assert.deepEqual(api.escritas.slice(escritasDaPrimeira), [
    `PUT /interno/layout/versoes/${idPorRotulo.get('8')}/estrutura`,
    `PUT /interno/layout/versoes/${idPorRotulo.get('9')}/estrutura`,
  ])
})
