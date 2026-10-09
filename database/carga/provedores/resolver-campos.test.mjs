// Rodar com: node --test database/carga/provedores/
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { resolverCampos } from './resolver-campos.mjs'

const XSD_INVOICY = [
  'Envio/RPS',
  'Envio/RPS/Prestador/CNPJ_prest',
  'Envio/RPS/Prestador/enderPrest/cMun',
  'Envio/RPS/Servico/cMun',
  'Envio/RPS/Servico/Valores/IBSCBS/IBS/ValpIBSUF',
  'Envio/RPS/Servico/Valores/IBSCBS/TribCompraGov/ValpIBSUF',
  'Envio/RPS/Servico/Valores/ValServicos',
  'Envio/RPS/Tomador/TomaCNPJ',
  'Envio/RPS/Tomador/TomaCPF',
  'Envio/RPS/Tomador/TomaxMun',
  'Envio/RPS/RPSSubs/SubsNumero',
  'Envio/RPS/Prestador/IM',
  'Envio/RPS/Tomador/TomaIM',
  'Envio/RPS/Intermediario/IM',
].map((caminho) => ({ nome: caminho.slice(caminho.lastIndexOf('/') + 1), caminho }))

let sequencial = 0
function tag(nomeCampo, interno = false) {
  sequencial++
  return { sequencial, caminho: `X/t${sequencial}`, nomeCampo, interno }
}

function caminhosDe(tags, familia = 'abrasf') {
  return resolverCampos(tags, XSD_INVOICY, familia).tags.map((item) =>
    item.campos.map((campo) => campo.caminhoInvoicy),
  )
}

test('nome único no XSD resolve sozinho; grafia já confirmada também', () => {
  assert.deepEqual(caminhosDe([tag('ValServicos'), tag('TomaXMun')]), [
    ['Envio/RPS/Servico/Valores/ValServicos'],
    ['Envio/RPS/Tomador/TomaxMun'],
  ])
})

test('cMun segue a família: prestador no leiaute DPS, serviço no ABRASF', () => {
  assert.deepEqual(caminhosDe([tag('cMun')], 'dps'), [['Envio/RPS/Prestador/enderPrest/cMun']])
  assert.deepEqual(caminhosDe([tag('cMun')], 'abrasf'), [['Envio/RPS/Servico/cMun']])
})

test('sufixo _CompraGov vai para TribCompraGov; o nome sem sufixo, para o tributo regular', () => {
  assert.deepEqual(caminhosDe([tag('ValpIBSUF_CompraGov'), tag('ValpIBSUF')]), [
    ['Envio/RPS/Servico/Valores/IBSCBS/TribCompraGov/ValpIBSUF'],
    ['Envio/RPS/Servico/Valores/IBSCBS/IBS/ValpIBSUF'],
  ])
})

test('nome repetido no XSD sem decisão registrada interrompe em vez de adivinhar', () => {
  assert.throws(() => caminhosDe([tag('IM')]), /2 caminhos no XSD do InvoiCy/)
})

test('pendente fica de fora e é listado com o motivo', () => {
  const { tags, pendentes } = resolverCampos(
    [tag('DocChaveDFe'), tag('tpOper')],
    XSD_INVOICY,
    'dps',
  )
  assert.deepEqual(
    tags.map((item) => item.campos),
    [[], []],
  )
  assert.deepEqual([...pendentes.keys()], ['DocChaveDFe', 'tpOper'])
  assert.match(pendentes.get('DocChaveDFe'), /gReeRepRes/)
})

test('campo em região do XSD ainda não confirmada vira pendente, mesmo com nome único', () => {
  const { tags, pendentes } = resolverCampos([tag('SubsNumero')], XSD_INVOICY, 'abrasf')
  assert.deepEqual(tags[0].campos, [])
  assert.match(pendentes.get('SubsNumero'), /RPSSubs é vazio/)
})

test('campo interno sem caminho no XML de entrada é listado; externo sem caminho é erro', () => {
  const { tags, internos } = resolverCampos([tag('EmpSenhaAutent', true)], XSD_INVOICY, 'abrasf')
  assert.deepEqual(tags[0].campos, [])
  assert.deepEqual([...internos], ['EmpSenhaAutent'])
  assert.throws(() => caminhosDe([tag('CampoInventado')]), /CampoInventado não existe/)
})

test('unificado, composição e alternativa', () => {
  const { tags } = resolverCampos(
    [tag('CNPJ_prest (CPF)'), tag('RPSNumero e RPSSerie'), tag('TomaCNPJ e TomaCPF')],
    XSD_INVOICY,
    'proprio',
  )
  assert.deepEqual(tags[0].campos, [
    { nomeCampo: 'CNPJ_prest', caminhoInvoicy: 'Envio/RPS/Prestador/CNPJ_prest' },
  ])
  assert.deepEqual(tags[1].campos, [])
  assert.deepEqual(tags[1].compoeDe, ['RPSNumero', 'RPSSerie'])
  assert.deepEqual(
    tags[2].campos.map((campo) => [campo.caminhoInvoicy, campo.alternativa]),
    [
      ['Envio/RPS/Tomador/TomaCNPJ', true],
      ['Envio/RPS/Tomador/TomaCPF', true],
    ],
  )
})

test('a raiz do RPS mapeada como grupo não vira campo', () => {
  assert.deepEqual(caminhosDe([tag('RPS', true)]), [[]])
})
