// Rodar com: node --test "database/carga/**/*.test.mjs"
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { prepararCarga } from './provedores/preparar.mjs'
import { garantirProvedor, sincronizarErros } from './sincronizar-provedor.mjs'
import { lerVocabularioInvoicy } from './vocabulario-invoicy.mjs'

const VERSAO_ID = 15

/** API falsa com um erro gravado por outra carga, ligado só a esta versão. */
function apiComErroExistente() {
  const chamadas = []
  const existente = {
    id: 900,
    codigo: 'E9999',
    versoesProvedorIds: [VERSAO_ID],
    versaoConcorrencia: 1,
  }
  return {
    chamadas,
    listarTudo: async () => [{ id: existente.id, codigo: existente.codigo }],
    chamar: async (metodo, rota) => {
      chamadas.push(`${metodo} ${rota}`)
      return { dados: metodo === 'GET' ? existente : { id: 1 } }
    },
  }
}

const QUALQUER_CAMPO = { get: () => 1 }

async function sincronizarComoACargaDeProvedores(contexto, id) {
  const { elementos } = await lerVocabularioInvoicy()
  const carga = await prepararCarga(id, elementos)
  const api = apiComErroExistente()
  contexto.mock.method(console, 'log', () => {})
  await sincronizarErros(api, id, VERSAO_ID, carga.erros, QUALQUER_CAMPO, {
    removerAusentes: carga.removerErrosAusentes,
  })
  return api.chamadas
}

test('erros: provedor sem lista de erros no catálogo não apaga o erro gravado por outra carga', async (contexto) => {
  const chamadas = await sincronizarComoACargaDeProvedores(contexto, 'betha')
  assert.deepEqual(chamadas, [])
})

test('erros: provedor dono da lista (IPM) continua removendo o erro que saiu do material', async (contexto) => {
  const chamadas = await sincronizarComoACargaDeProvedores(contexto, 'ipm')
  assert.ok(chamadas.includes('DELETE /interno/erros/900'))
})

test('erros: sem a opção, a sincronização nunca remove', async (contexto) => {
  const api = apiComErroExistente()
  contexto.mock.method(console, 'log', () => {})
  await sincronizarErros(api, 'x', VERSAO_ID, [], QUALQUER_CAMPO)
  assert.deepEqual(api.chamadas, [])
})

function apiComProvedor(aliases) {
  const chamadas = []
  const existente = { id: 16, nome: 'Coplan', categoria: 'Proprio', aliases, versaoConcorrencia: 3 }
  return {
    chamadas,
    listarTudo: async () => [{ id: existente.id, nome: existente.nome }],
    chamar: async (metodo, rota, corpo) => {
      chamadas.push({ metodo, rota, corpo })
      return { dados: existente }
    },
  }
}

const COPLAN = { nome: 'Coplan', aliases: ['COPLAN - Modelo Nacional'] }

test('provedor existente: alias diferente do catálogo é corrigido; igual não gera escrita', async (contexto) => {
  contexto.mock.method(console, 'log', () => {})
  const diferente = apiComProvedor(['COPLAN antigo'])
  await garantirProvedor(diferente, COPLAN)
  const escritas = diferente.chamadas.filter(({ metodo }) => metodo !== 'GET')
  assert.deepEqual(escritas, [
    {
      metodo: 'PUT',
      rota: '/interno/provedores/16',
      corpo: { nome: 'Coplan', categoria: 'Proprio', aliases: ['COPLAN - Modelo Nacional'] },
    },
  ])

  const igual = apiComProvedor(['COPLAN - Modelo Nacional'])
  await garantirProvedor(igual, COPLAN)
  assert.equal(igual.chamadas.filter(({ metodo }) => metodo !== 'GET').length, 0)
})
