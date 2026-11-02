// Rodar com: node --test database/carga/emissor-nacional/estrutura-layout.test.mjs
import { strict as assert } from 'node:assert'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'

import { arquivoDaEstrutura, gerarEstrutura } from '../layout-invoicy.mjs'
import { montarEstrutura, valorModelo } from './estrutura-layout.mjs'

const restricao = (base, facetas) =>
  `<xs:simpleType><xs:restriction base="xs:${base}">${facetas}</xs:restriction></xs:simpleType>`

const XSD = `<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Envio"><xs:complexType><xs:sequence>
    <xs:element name="ModeloDocumento">${restricao('string', '<xs:maxLength value="10" />')}</xs:element>
    <xs:element name="RPS"><xs:complexType><xs:sequence>
      <xs:element name="ListaItens" minOccurs="0"><xs:complexType><xs:sequence>
        <xs:element name="Item" maxOccurs="999"><xs:complexType><xs:sequence>
          <xs:element name="ItemSeq">${restricao('decimal', '<xs:totalDigits value="4" /><xs:fractionDigits value="0" />')}</xs:element>
          <xs:element name="ItemvUnit">${restricao('decimal', '<xs:totalDigits value="15" /><xs:fractionDigits value="2" />')}</xs:element>
        </xs:sequence></xs:complexType></xs:element>
      </xs:sequence></xs:complexType></xs:element>
      <xs:element name="Destinatario" minOccurs="0"><xs:complexType><xs:sequence>
        <xs:choice>
          <xs:element name="DestCNPJ" minOccurs="0">${restricao('string', '<xs:maxLength value="14" />')}</xs:element>
          <xs:element name="DestCPF" minOccurs="0">${restricao('string', '<xs:maxLength value="11" />')}</xs:element>
        </xs:choice>
        <xs:element name="DestUF">${restricao('string', '<xs:maxLength value="2" />')}</xs:element>
      </xs:sequence></xs:complexType></xs:element>
    </xs:sequence></xs:complexType></xs:element>
  </xs:sequence></xs:complexType></xs:element>
</xs:schema>`

const tag = (caminho, observacao = null) => ({ caminho, observacao })

const PLANILHA = {
  tags: [
    tag('Envio'),
    tag('Envio/ModeloDocumento'),
    tag('Envio/RPS'),
    tag('Envio/RPS/ListaItens'),
    tag('Envio/RPS/ListaItens/Item', 'Não informar este grupo quando a integração for API Rest.'),
    tag('Envio/RPS/ListaItens/Item/ItemSeq'),
    tag('Envio/RPS/ListaItens/Item/ItemvUnit'),
    tag('Envio/RPS/Destinatario'),
    tag('Envio/RPS/Destinatario/DestCNPJ'),
    tag('Envio/RPS/Destinatario/DestCPF'),
    tag('Envio/RPS/Destinatario/DestUF'),
    tag('Envio/RPS/Destinatario/DestEmailRest'),
  ],
}

const VOCABULARIO = new Set(['//ListaItens', '//ListaItens/Item/ItemSeq', '//Destinatario/DestUF'])

function noPorCaminho(raiz) {
  const indice = new Map()
  const percorrer = (no) => {
    indice.set(no.caminho, no)
    no.filhos.forEach(percorrer)
  }
  percorrer(raiz)
  return indice
}

test('valor-modelo cabe nas facetas do XSD: texto curto vira X, decimal leva as casas', () => {
  const folha = (tipo, facetas) => ({ tipo, facetas })
  assert.equal(valorModelo(folha('string', { maxLength: 60 })), 'texto(60)')
  assert.equal(valorModelo(folha('string', { maxLength: 2 })), 'XX')
  assert.equal(valorModelo(folha('decimal', { totalDigits: 15, fractionDigits: 2 })), '0.00')
  assert.equal(valorModelo(folha('dateTime', {})), '2026-12-31T23:59:59')
})

test('XML: o grupo de item omitido no REST fica soXml e repete; a escolha fica marcada', () => {
  const { xml } = montarEstrutura(XSD, PLANILHA, VOCABULARIO)
  const nos = noPorCaminho(xml)

  const item = nos.get('Envio/RPS/ListaItens/Item')
  assert.equal(item.formato, 'soXml')
  assert.equal(item.repete, true)
  assert.deepEqual(
    nos.get('Envio/RPS/Destinatario').filhos.map((filho) => [filho.nome, filho.escolha]),
    [
      ['DestCNPJ', true],
      ['DestCPF', true],
      ['DestUF', false],
    ],
  )
  assert.equal(
    nos.get('Envio/RPS/ListaItens/Item/ItemSeq').caminhoCampo,
    '//ListaItens/Item/ItemSeq',
  )
})

test('JSON: raiz Documento, RPS lista, e a lista recebe os campos do item direto', () => {
  const { json } = montarEstrutura(XSD, PLANILHA, VOCABULARIO)
  const nos = noPorCaminho(json)

  assert.equal(json.nome, 'Documento')
  assert.equal(nos.get('Documento/RPS').repete, true)
  const lista = nos.get('Documento/RPS/ListaItens')
  assert.equal(lista.repete, true)
  assert.deepEqual(
    lista.filhos.map((filho) => filho.nome),
    ['ItemSeq', 'ItemvUnit'],
  )
  assert.ok(![...nos.values()].some((no) => no.formato === 'soXml' || no.nome === 'Item'))
})

test('tag da planilha sem par no XSD entra só no JSON, depois do irmão que a precede', () => {
  const { xml, json } = montarEstrutura(XSD, PLANILHA, VOCABULARIO)

  const destinatario = noPorCaminho(json).get('Documento/RPS/Destinatario')
  assert.deepEqual(
    destinatario.filhos.map((filho) => [filho.nome, filho.formato]),
    [
      ['DestCNPJ', 'ambos'],
      ['DestCPF', 'ambos'],
      ['DestUF', 'ambos'],
      ['DestEmailRest', 'soJson'],
    ],
  )
  assert.equal(noPorCaminho(xml).has('Envio/RPS/Destinatario/DestEmailRest'), false)
})

for (const rotulo of ['8', '9']) {
  test(`a estrutura versionada da versão ${rotulo} é a saída do gerador`, async () => {
    const versionada = JSON.parse(await readFile(arquivoDaEstrutura(rotulo), 'utf8'))

    assert.deepEqual(versionada, await gerarEstrutura(rotulo))
  })
}

test('tag da planilha com caminho errado não vira nó; sem a lista, a carga para', () => {
  const planilha = {
    tags: [...PLANILHA.tags, tag('Envio/RPS/Destinatario/DestErrado/DestCNPJ')],
  }

  assert.throws(() => montarEstrutura(XSD, planilha, VOCABULARIO), /DestErrado\/DestCNPJ/)
  const { json } = montarEstrutura(
    XSD,
    planilha,
    VOCABULARIO,
    new Set(['Envio/RPS/Destinatario/DestErrado/DestCNPJ']),
  )
  assert.deepEqual(json, montarEstrutura(XSD, PLANILHA, VOCABULARIO).json)
})

test('na estrutura 9, os sete grupos de item que a planilha manda omitir somem do JSON', async () => {
  const { xml, json } = await gerarEstrutura('9')
  const soXml = [...noPorCaminho(xml).values()].filter((no) => no.formato === 'soXml')

  assert.deepEqual(
    soXml.map((no) => no.nome),
    ['Item', 'Parcela', 'Material', 'Ded', 'UnidadeImobiliaria', 'Documento', 'NFSeReferenciada'],
  )
  const nomesNoJson = new Set([...noPorCaminho(json).values()].map((no) => no.caminho))
  assert.ok(!nomesNoJson.has('Documento/RPS/ListaItens/Item'))
  assert.ok(nomesNoJson.has('Documento/RPS/ListaItens/ItemSeq'))
})
