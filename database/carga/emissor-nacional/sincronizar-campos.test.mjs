// Rodar com: node --test database/carga/emissor-nacional/sincronizar-campos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { sincronizarVocabulario } from './sincronizar-campos.mjs'

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
