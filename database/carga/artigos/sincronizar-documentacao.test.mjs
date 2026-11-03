// Rodar com: node --test database/carga/artigos/sincronizar-documentacao.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { sincronizarArtigoDaDocumentacao } from './sincronizar-documentacao.mjs'

const DESEJADO = {
  tipo: 'Procedimento',
  titulo: 'Emissão em lote',
  corpo: '> [!RESUMO]\n> Texto novo.\n',
  campoLayoutIds: [],
  erroIds: [7],
}

const EXISTENTES = new Map([[DESEJADO.titulo, { id: 42, titulo: DESEJADO.titulo }]])

function apiComArtigoSalvo(salvo) {
  const chamadas = []
  return {
    chamadas,
    async chamar(metodo, rota, corpo, etag) {
      chamadas.push({ metodo, rota, corpo, etag })
      return { dados: metodo === 'GET' ? salvo : {} }
    },
  }
}

function artigoSalvo(publicado) {
  return {
    id: 42,
    tipo: 'Procedimento',
    corpo: 'Texto antigo.\n',
    campoLayoutIds: [],
    erroIds: [7],
    publicado,
    versaoConcorrencia: 9,
  }
}

function sincronizar(api, opcoes) {
  return sincronizarArtigoDaDocumentacao(api, DESEJADO, EXISTENTES, new Map(), opcoes)
}

test('sem a opção, publicado com corpo novo fica no relatório e rascunho atualiza', async () => {
  const apiPublicado = apiComArtigoSalvo(artigoSalvo(true))
  const apiRascunho = apiComArtigoSalvo(artigoSalvo(false))

  const publicado = await sincronizar(apiPublicado)
  const rascunho = await sincronizar(apiRascunho)

  assert.deepEqual(publicado, { situacao: 'só relatório', detalhe: 'artigo já publicado' })
  assert.deepEqual(apiPublicado.chamadas.map(({ metodo }) => metodo), ['GET'])
  assert.equal(rascunho.situacao, 'atualizado')
  assert.deepEqual(
    apiRascunho.chamadas.map(({ metodo, rota }) => `${metodo} ${rota}`),
    ['GET /interno/artigos/42', 'PUT /interno/artigos/42'],
  )
})

test('com a opção, publicado recebe o corpo pelo PUT, sem despublicar', async () => {
  const api = apiComArtigoSalvo(artigoSalvo(true))

  const resultado = await sincronizar(api, { atualizarPublicados: true })

  assert.equal(resultado.situacao, 'atualizado')
  assert.deepEqual(api.chamadas, [
    { metodo: 'GET', rota: '/interno/artigos/42', corpo: undefined, etag: undefined },
    { metodo: 'PUT', rota: '/interno/artigos/42', corpo: DESEJADO, etag: '9' },
  ])
})
