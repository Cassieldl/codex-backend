// Rodar com: node --test database/carga/emissor-nacional/resolver-mapeamento.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { notasDeComposicao, resolverMapeamento } from './resolver-mapeamento.mjs'
import { indexarDefinicoes, lerMapeamento, montarCampos } from './transformar-campos.mjs'

const CABECALHO = 'codigoTag;nomeTag;elemento;tipo;codigoTagPai;codigoCampo;nomeCampo;nulos'

function tags(...linhas) {
  return lerMapeamento([CABECALHO, '1;DPS;G;C;0;0;;Sim', ...linhas].join('\n'))
}

function elemento(caminho) {
  return { nome: caminho.split('/').at(-1), caminho, documentacao: null, ehGrupo: false }
}

const XSD = [
  elemento('Envio/RPS/Tomador/TomaCNPJ'),
  elemento('Envio/RPS/Tomador/TomaCEP'),
  elemento('Envio/RPS/Prestador/CNPJ_prest'),
  elemento('Envio/RPS/Prestador/enderPrest/cMun'),
  elemento('Envio/RPS/Servico/cMun'),
  elemento('Envio/RPS/RPSNumero'),
  elemento('Envio/RPS/RPSSerie'),
]

test('nome único no XSD resolve o caminho sozinho', () => {
  const { tags: resolvidas } = resolverMapeamento(tags('2;CNPJ;E;C;1;42;TomaCNPJ;Sim'), XSD)
  assert.equal(resolvidas[1].caminhoInvoicy, 'Envio/RPS/Tomador/TomaCNPJ')
})

test('nome ambíguo usa a decisão registrada; sem decisão, interrompe', () => {
  const { tags: resolvidas } = resolverMapeamento(tags('2;cLocEmi;E;N;1;188;cMun;Sim'), XSD)
  assert.equal(resolvidas[1].caminhoInvoicy, 'Envio/RPS/Prestador/enderPrest/cMun')

  assert.throws(
    () => resolverMapeamento(tags('2;xpto;E;N;1;9999;cMun;Sim'), XSD),
    /cMun tem 2 caminhos no XSD/,
  )
})

test('nome que não existe no XSD e não tem decisão interrompe em vez de ser adivinhado', () => {
  assert.throws(
    () => resolverMapeamento(tags('2;xpto;E;C;1;9999;CampoInventado;Sim'), XSD),
    /CampoInventado tem 0 caminhos/,
  )
})

test('pendente sai da tabela da versão e entra na lista com o motivo', () => {
  const { tags: resolvidas, pendentes } = resolverMapeamento(
    tags('2;indTotTrib;E;C;1;652;indTotTrib;Sim'),
    XSD,
  )
  assert.equal(resolvidas[1].codigoCampo, 0)
  assert.deepEqual([...pendentes.keys()], [652])
  assert.match(pendentes.get(652).motivo, /não existe no layout de entrada/)
})

test('código duplicado do mesmo campo é unificado, com o nome do campo principal', () => {
  const { tags: resolvidas } = resolverMapeamento(
    tags('2;CNPJ;E;C;1;89;CNPJ_prest;Sim', '3;CPF;E;C;1;491;CNPJ_prest (CPF);Sim'),
    XSD,
  )
  assert.deepEqual(
    resolvidas.slice(1).map((tag) => [tag.codigoCampo, tag.nomeCampo, tag.caminhoInvoicy]),
    [
      [89, 'CNPJ_prest', 'Envio/RPS/Prestador/CNPJ_prest'],
      [89, 'CNPJ_prest', 'Envio/RPS/Prestador/CNPJ_prest'],
    ],
  )
})

test('o Id não vira campo: vira nota no número e na série', () => {
  const lidas = tags(
    '2;Id;A;C;1;305;RPSNumero e RPSSerie;Sim',
    '3;nDPS;E;C;1;2;RPSNumero;Sim',
    '4;serie;E;C;1;3;RPSSerie;Sim',
  )
  const { tags: resolvidas } = resolverMapeamento(lidas, XSD)
  assert.equal(resolvidas[1].codigoCampo, 0)

  const notas = notasDeComposicao(resolvidas)
  assert.deepEqual([...notas.keys()].sort(), [2, 3])
  assert.match(notas.get(2), /compõe o Id da DPS/)
})

test('dois códigos para o mesmo campo do XML de entrada viram uma linha só, com os dois destinos', () => {
  const { tags: resolvidas } = resolverMapeamento(
    tags('2;CEP;E;C;1;112;TomaCEP;Sim', '3;cEndPost;E;C;1;279;TomaCEP;Sim'),
    XSD,
  )
  const campos = montarCampos(resolvidas, indexarDefinicoes([], { campos_somente_anexo_I: [] }))

  assert.equal(campos.length, 1)
  assert.deepEqual(campos[0].codigosCampo, [112, 279])
  assert.equal(campos[0].campoVersao.destinoXml, 'DPS/CEP | DPS/cEndPost')
})
