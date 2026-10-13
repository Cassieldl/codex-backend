// Rodar com: node --test database/carga/emissor-nacional/preparar-campos.test.mjs
import { strict as assert } from 'node:assert'
import { join } from 'node:path'
import { test } from 'node:test'

import { FONTES_DE_EXEMPLO, lerFonteDeExemplo, lerFonteDeExemploDoDisco } from '../exemplos-xml.mjs'
import { lerVocabularioInvoicy } from '../vocabulario-invoicy.mjs'
import {
  PASTA_DO_MATERIAL_SEFIN,
  lerMaterialEmissorNacional,
  prepararCamposEmissorNacional,
} from './preparar-campos.mjs'

const material = await lerMaterialEmissorNacional()
const { elementos } = await lerVocabularioInvoicy()
const fonteDpsNacional = await lerFonteDeExemploDoDisco('dps-nacional')

function campoPorCaminhoDeDestino(campos, caminho) {
  return campos.find((campo) => campo.campoVersao.destinoXml.split(' | ').includes(caminho))
}

test('o material real monta os mesmos 195 campos, 326 tags e 30 pendentes de sempre', () => {
  const { campos, tags, pendentes } = prepararCamposEmissorNacional(material, elementos)

  assert.equal(campos.length, 195)
  assert.equal(tags.length, 326)
  assert.equal(pendentes.size, 30)
})

test('sem fonte de exemplo, nenhum campo do material real traz a chave exemplo', () => {
  const { campos } = prepararCamposEmissorNacional(material, elementos)

  assert.equal(campos.length, 195)
  assert.deepEqual(
    campos.filter((campo) => 'exemplo' in campo.campoVersao),
    [],
  )
})

test('com a fonte DPS, todo campo passa a ter a chave exemplo, com texto ou null', () => {
  const { campos } = prepararCamposEmissorNacional(material, elementos, fonteDpsNacional)

  assert.equal(campos.length, 195)
  for (const campo of campos) {
    const { exemplo } = campo.campoVersao
    assert.equal('exemplo' in campo.campoVersao, true, campo.nomeCampo)
    assert.equal(exemplo === null || typeof exemplo === 'string', true, campo.nomeCampo)
    assert.equal(campo.decisaoDeExemplo.motivo === null, exemplo !== null, campo.nomeCampo)
  }
})

test('exemplos reais da DPS: valor público fica, identificação e teste ficam de fora', () => {
  const { campos } = prepararCamposEmissorNacional(material, elementos, fonteDpsNacional)
  const decisaoDe = (caminho) => campoPorCaminhoDeDestino(campos, caminho)

  assert.equal(decisaoDe('DPS/infDPS/valores/vServPrest/vServ').campoVersao.exemplo, '29155.75')
  assert.equal(decisaoDe('DPS/infDPS/dhEmi').campoVersao.exemplo, '2026-06-02T08:06:28-03:00')

  const cnpjDoPrestador = decisaoDe('DPS/infDPS/prest/CNPJ')
  assert.equal(cnpjDoPrestador.campoVersao.exemplo, null)
  assert.equal(cnpjDoPrestador.decisaoDeExemplo.motivo, 'IDENTIFICACAO')

  const cpfDoTomador = decisaoDe('DPS/infDPS/toma/CPF')
  assert.equal(cpfDoTomador.campoVersao.exemplo, null)
  assert.equal(cpfDoTomador.decisaoDeExemplo.motivo, 'IDENTIFICACAO')

  const descricaoDoServico = decisaoDe('DPS/infDPS/serv/cServ/xDescServ')
  assert.equal(descricaoDoServico.campoVersao.exemplo, null)
  assert.equal(descricaoDoServico.decisaoDeExemplo.motivo, 'MARCADOR_TESTE')
})

test('os dois CST da DPS recebem cada um o seu valor, nunca o mesmo copiado', () => {
  const { campos } = prepararCamposEmissorNacional(material, elementos, fonteDpsNacional)

  const cstDoPisCofins = campoPorCaminhoDeDestino(
    campos,
    'DPS/infDPS/valores/trib/tribFed/piscofins/CST',
  )
  const cstDoIbsCbs = campoPorCaminhoDeDestino(campos, 'DPS/infDPS/IBSCBS/valores/trib/gIBSCBS/CST')

  assert.equal(cstDoPisCofins.campoVersao.exemplo, '01')
  assert.equal(cstDoIbsCbs.campoVersao.exemplo, '000')
})

test('lê o material da pasta informada, e uma pasta sem o material interrompe', async () => {
  assert.equal(PASTA_DO_MATERIAL_SEFIN.endsWith('material-sefin'), true)
  await assert.rejects(
    lerMaterialEmissorNacional(join(PASTA_DO_MATERIAL_SEFIN, 'pasta-que-nao-existe')),
    { code: 'ENOENT' },
  )
})

const MAPEAMENTO_MINIMO = [
  'codigoTag;nomeTag;elemento;tipo;codigoTagPai;codigoCampo;nomeCampo;nulos',
  '1;DPS;G;C;0;0;;Sim',
  '2;infDPS;G;C;1;0;;Sim',
  '3;Id;A;C;2;305;RPSNumero e RPSSerie;Sim',
  '4;nDPS;E;N;2;2;RPSNumero;Sim',
  '5;serie;E;C;2;3;RPSSerie;Sim',
  '6;tag746;E;C;2;746;CampoPendente;Sim',
].join('\n')

const ELEMENTOS_MINIMOS = [
  { nome: 'RPSNumero', caminho: 'Envio/RPS/RPSNumero', ehGrupo: false },
  { nome: 'RPSSerie', caminho: 'Envio/RPS/RPSSerie', ehGrupo: false },
]

function materialMinimo(mapeamento = MAPEAMENTO_MINIMO) {
  return {
    mapeamento,
    leiaute: { campos: [] },
    diferencas: { campos_somente_anexo_I: [] },
    regras: { regras: [] },
  }
}

test('a preparação é pura: resolve, separa pendentes, anota composição e liga o exemplo', () => {
  const fonte = lerFonteDeExemplo(
    ['<DPS><infDPS><nDPS>77</nDPS><serie>5</serie></infDPS></DPS>'],
    FONTES_DE_EXEMPLO['dps-nacional'],
  )
  const { campos, pendentes } = prepararCamposEmissorNacional(
    materialMinimo(),
    ELEMENTOS_MINIMOS,
    fonte,
  )

  assert.deepEqual([...pendentes.keys()], [746])
  assert.deepEqual(
    campos.map((campo) => [campo.nomeCampo, campo.caminhoInvoicy, campo.campoVersao.exemplo]),
    [
      ['RPSNumero', 'Envio/RPS/RPSNumero', '77'],
      ['RPSSerie', 'Envio/RPS/RPSSerie', '5'],
    ],
  )
  assert.match(campos[0].campoVersao.notaTransformacao, /Também compõe o Id da DPS/)
})

test('campo sem caminho único no XSD interrompe a preparação', () => {
  assert.throws(
    () => prepararCamposEmissorNacional(materialMinimo(), []),
    /RPSNumero tem 0 caminhos no XSD/,
  )
})
