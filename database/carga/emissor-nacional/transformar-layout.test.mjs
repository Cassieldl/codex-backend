// Rodar com: node --test database/carga/emissor-nacional/transformar-layout.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import {
  caminhoCanonico,
  grupoDe,
  lerXsd,
  montarSignificado,
  montarVocabulario,
} from './transformar-layout.mjs'

const XSD = `<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Envio">
    <xs:annotation><xs:documentation>Envio</xs:documentation></xs:annotation>
    <xs:complexType><xs:sequence>
      <xs:element name="ModeloDocumento">
        <xs:annotation><xs:documentation>Modelo &amp; tipo</xs:documentation></xs:annotation>
      </xs:element>
      <xs:element name="RPS">
        <xs:complexType><xs:sequence>
          <xs:element name="RPSSubs" minOccurs="0" />
          <xs:element name="Tomador">
            <xs:complexType><xs:sequence>
              <xs:element name="TomaCNPJ">
                <xs:annotation><xs:documentation>CNPJ do tomador</xs:documentation></xs:annotation>
              </xs:element>
              <xs:element name="TomaCPF" />
            </xs:sequence></xs:complexType>
          </xs:element>
        </xs:sequence></xs:complexType>
      </xs:element>
    </xs:sequence></xs:complexType>
  </xs:element>
</xs:schema>`

const SEM_PLANILHA = { tags: [] }

test('o caminho de cada elemento sai do aninhamento, inclusive depois de elemento autofechado', () => {
  const elementos = lerXsd(XSD)
  assert.deepEqual(
    elementos.map((elemento) => elemento.caminho),
    [
      'Envio',
      'Envio/ModeloDocumento',
      'Envio/RPS',
      'Envio/RPS/RPSSubs',
      'Envio/RPS/Tomador',
      'Envio/RPS/Tomador/TomaCNPJ',
      'Envio/RPS/Tomador/TomaCPF',
    ],
  )
})

test('grupo é quem tem filhos; a documentação é a do próprio elemento, decodificada', () => {
  const porCaminho = new Map(lerXsd(XSD).map((elemento) => [elemento.caminho, elemento]))
  assert.equal(porCaminho.get('Envio/RPS/Tomador').ehGrupo, true)
  assert.equal(porCaminho.get('Envio/RPS/RPSSubs').ehGrupo, false)
  assert.equal(porCaminho.get('Envio/ModeloDocumento').documentacao, 'Modelo & tipo')
  assert.equal(porCaminho.get('Envio/RPS/Tomador').documentacao, null)
})

test('caminho canônico parte do RPS, ou do Envio para o que está fora dele', () => {
  assert.equal(caminhoCanonico('Envio/RPS/Tomador/TomaCNPJ'), '//Tomador/TomaCNPJ')
  assert.equal(caminhoCanonico('Envio/ModeloDocumento'), '//ModeloDocumento')
  assert.equal(grupoDe('Envio/RPS/Tomador/TomaCNPJ'), 'Tomador')
  assert.equal(grupoDe('Envio/RPS/natOp'), 'RPS')
  assert.equal(grupoDe('Envio/ModeloDocumento'), 'Envio')
})

test('o grupo é o caminho inteiro do pai, então cada grupo do XSD vira um cabeçalho', () => {
  assert.equal(
    grupoDe('Envio/RPS/Servico/Valores/IBSCBS/IBS/ValpIBSUF'),
    'Servico/Valores/IBSCBS/IBS',
  )
  // Os dois IBSCBS do Servico são grupos diferentes e não podem cair no mesmo cabeçalho.
  assert.notEqual(
    grupoDe('Envio/RPS/Servico/Valores/IBSCBS/CSTIBSCBS'),
    grupoDe('Envio/RPS/Servico/IBSCBS/CSTIBSCBS'),
  )
})

test('grupo usado como campo abre o próprio grupo, junto dos filhos', () => {
  assert.equal(grupoDe('Envio/RPS/ListaDed/Ded', true), 'ListaDed/Ded')
  assert.equal(grupoDe('Envio/RPS/ListaDed/Ded/DedValor'), 'ListaDed/Ded')
})

test('o vocabulário tem as folhas e só os grupos que algum provedor usa como campo', () => {
  const elementos = lerXsd(XSD)
  assert.deepEqual(
    montarVocabulario(elementos, SEM_PLANILHA).map((campo) => campo.caminhoXml),
    ['//ModeloDocumento', '//RPSSubs', '//Tomador/TomaCNPJ', '//Tomador/TomaCPF'],
  )
  assert.ok(
    montarVocabulario(elementos, SEM_PLANILHA, new Set(['Envio/RPS/Tomador']))
      .map((campo) => campo.caminhoXml)
      .includes('//Tomador'),
  )
})

test('no vocabulário, o grupo usado como campo abre o próprio grupo, e as folhas ficam no do pai', () => {
  const porCaminho = new Map(
    montarVocabulario(lerXsd(XSD), SEM_PLANILHA, new Set(['Envio/RPS/Tomador'])).map((campo) => [
      campo.caminhoXml,
      campo.grupo,
    ]),
  )
  assert.equal(porCaminho.get('//Tomador'), 'Tomador')
  assert.equal(porCaminho.get('//Tomador/TomaCNPJ'), 'Tomador')
  assert.equal(porCaminho.get('//RPSSubs'), 'RPS')
  assert.equal(porCaminho.get('//ModeloDocumento'), 'Envio')
})

test('região em dúvida fica inteira fora do vocabulário', () => {
  const caminhos = montarVocabulario(lerXsd(XSD), SEM_PLANILHA, new Set(), [
    'Envio/RPS/Tomador',
  ]).map((campo) => campo.caminhoXml)
  assert.deepEqual(caminhos, ['//ModeloDocumento', '//RPSSubs'])
})

test('a descrição da planilha vence a do XSD, e observação repetida não é duplicada', () => {
  const planilha = {
    tags: [
      {
        caminho: 'Envio/RPS/Tomador/TomaCNPJ',
        descricao: 'CNPJ do tomador',
        observacao: 'CNPJ do tomador',
      },
      {
        caminho: 'Envio/ModeloDocumento',
        descricao: 'Modelo do documento',
        observacao: 'Informar: NFSE',
      },
    ],
  }
  const porCaminho = new Map(
    montarVocabulario(lerXsd(XSD), planilha).map((campo) => [campo.caminhoXml, campo]),
  )
  assert.equal(porCaminho.get('//Tomador/TomaCNPJ').significado, 'CNPJ do tomador')
  assert.equal(
    porCaminho.get('//ModeloDocumento').significado,
    'Modelo do documento. Informar: NFSE',
  )
  assert.equal(porCaminho.get('//Tomador/TomaCPF').significado, 'TomaCPF')
})

test('caminhos que gerariam o mesmo slug interrompem a montagem', () => {
  const colidindo = [
    { nome: 'TomaCEP', caminho: 'Envio/RPS/Tomador/TomaCEP', documentacao: null, ehGrupo: false },
    { nome: 'TomaCep', caminho: 'Envio/RPS/Tomador/TomaCep', documentacao: null, ehGrupo: false },
  ]
  assert.throws(() => montarVocabulario(colidindo, SEM_PLANILHA), /geram o mesmo slug/)
})

test('significado longo perde a lista de valores e a frase que a apresenta, nunca o meio do texto', () => {
  const lista = Array.from(
    { length: 60 },
    (_, i) => `${i} - Valor de número ${i} com descrição;`,
  ).join('\n')
  const significado = montarSignificado(
    'Mecanismo de apoio',
    `Usado no comércio exterior. Os valores aceitos são:\n${lista}`,
  )
  assert.equal(
    significado,
    'Mecanismo de apoio. Usado no comércio exterior. Os valores aceitos estão no domínio da versão de cada provedor.',
  )
})

test('significado curto mantém a observação inteira, lista incluída', () => {
  assert.equal(
    montarSignificado('Situação', 'Valores aceitos:\n1 - Sim;\n2 - Não'),
    'Situação. Valores aceitos: 1 - Sim; 2 - Não',
  )
})
