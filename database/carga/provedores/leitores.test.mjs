// Rodar com: node --test database/carga/provedores/
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { lerMapeamento } from './ler-mapeamento.mjs'
import { definicoesPorCaminho, indexarEsquema } from './ler-xsd.mjs'
import { lerXml } from './xml.mjs'

test('XML: tira o prefixo do namespace e ignora o que vem antes da raiz', () => {
  const raiz = lerXml(
    'Frase solta do navegador.<?xml version="1.0"?><!-- c --><xsd:schema a=\'1\'><xsd:element name="X"/></xsd:schema>',
  )
  assert.equal(raiz.nome, 'schema')
  assert.deepEqual(raiz.atributos, { a: '1' })
  assert.deepEqual(
    raiz.filhos.map((filho) => [filho.nome, filho.atributos.name]),
    [['element', 'X']],
  )
})

test('XML: fechamento trocado é erro, não árvore torta', () => {
  assert.throws(() => lerXml('<a><b></a></b>'), /XML malformado/)
})

const ESQUEMA_SIMPLES = `<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:simpleType name="tsTexto"><xs:restriction base="xs:string"><xs:minLength value="1"/><xs:maxLength value="60"/></xs:restriction></xs:simpleType>
  <xs:simpleType name="tsCurto"><xs:restriction base="tsTexto"><xs:maxLength value="10"/></xs:restriction></xs:simpleType>
  <xs:simpleType name="tsTipo"><xs:restriction base="xs:string"><xs:enumeration value="1"><xs:annotation><xs:documentation>Um</xs:documentation></xs:annotation></xs:enumeration><xs:enumeration value="2"/></xs:restriction></xs:simpleType>
  <xs:simpleType name="tsValor"><xs:restriction base="xs:decimal"><xs:totalDigits value="15"/><xs:fractionDigits value="2"/><xs:minInclusive value="0"/></xs:restriction></xs:simpleType>
  <xs:complexType name="tcDoc">
    <xs:choice><xs:element name="CNPJ" type="tsTexto"/><xs:element name="CPF" type="tsTexto"/></xs:choice>
  </xs:complexType>
  <xs:complexType name="tcInf">
    <xs:sequence>
      <xs:element name="nome" type="tsCurto"/>
      <xs:element name="tipo" type="tsTipo" minOccurs="0"/>
      <xs:element name="valor" type="tsValor"/>
      <xs:element name="doc" type="tcDoc"/>
      <xs:element ref="Assinatura" minOccurs="0"/>
    </xs:sequence>
    <xs:attribute name="Id" type="xs:ID" use="required"/>
  </xs:complexType>
  <xs:element name="Raiz"><xs:complexType><xs:sequence><xs:element name="inf" type="tcInf"/></xs:sequence></xs:complexType></xs:element>
  <xs:element name="Assinatura" type="tsTexto"/>
</xs:schema>`

const definicoes = definicoesPorCaminho(indexarEsquema([ESQUEMA_SIMPLES]), 'Raiz')

test('XSD: percorre tipos nomeados a partir da raiz, com atributo como passo do caminho', () => {
  assert.deepEqual(
    [...definicoes.keys()],
    [
      'Raiz',
      'Raiz/inf',
      'Raiz/inf/nome',
      'Raiz/inf/tipo',
      'Raiz/inf/valor',
      'Raiz/inf/doc',
      'Raiz/inf/doc/CNPJ',
      'Raiz/inf/doc/CPF',
      'Raiz/inf/Assinatura',
      'Raiz/inf/Id',
    ],
  )
  assert.equal(definicoes.get('Raiz/inf').ehGrupo, true)
  assert.equal(definicoes.get('Raiz/inf/Id').minimo, '1')
})

test('XSD: a restrição mais próxima da tag vence na cadeia de tipos', () => {
  const nome = definicoes.get('Raiz/inf/nome')
  assert.equal(nome.tipoBase, 'string')
  assert.equal(nome.tamanhoMinimo, '1')
  assert.equal(nome.tamanhoMaximo, '10')
})

test('XSD: ocorrência, escolha, valores aceitos e dígitos', () => {
  assert.equal(definicoes.get('Raiz/inf/tipo').minimo, '0')
  assert.deepEqual(definicoes.get('Raiz/inf/tipo').valoresAceitos, [
    { valor: '1', descricao: 'Um' },
    { valor: '2', descricao: null },
  ])
  assert.equal(definicoes.get('Raiz/inf/doc/CPF').emEscolha, true)
  assert.equal(definicoes.get('Raiz/inf/nome').emEscolha, false)
  assert.equal(definicoes.get('Raiz/inf/valor').totalDigitos, '15')
  assert.equal(definicoes.get('Raiz/inf/valor').casasDecimais, '2')
})

test('XSD: o valor mínimo do tipo não se confunde com a ocorrência da tag', () => {
  // tsValor tem minInclusive 0 e a tag é obrigatória: as duas coisas convivem.
  assert.equal(definicoes.get('Raiz/inf/valor').minimo, '1')
  assert.equal(definicoes.get('Raiz/inf/valor').valorMinimo, '0')
})

test('XSD: raiz que não existe no esquema é erro', () => {
  assert.throws(
    () => definicoesPorCaminho(indexarEsquema([ESQUEMA_SIMPLES]), 'Outra'),
    /raiz Outra/,
  )
})

function linha(seq, tag, elemento, pai, campo, ocorrencia, descricao, origem) {
  return `| **${seq}** | ${tag} | ${elemento} | ${pai} | ${campo} | ${ocorrencia} | ${descricao} | ${origem} |`
}

const MAPEAMENTO = [
  '# Mapeamento',
  '| Sequencial | TAG | Elemento XML | Dentro do grupo | Nome | Ocorrência | Descrição | Interno / Externo |',
  '| --- | --- | --- | --- | --- | --- | --- | --- |',
  linha(1, 'Envio', 'Grupo', '0 -', '', '1-1', '', 'Interno'),
  linha(2, 'Lote', 'Grupo', '1 - Envio', '', '1-1', '', 'Interno'),
  linha(5, 'Senha', 'Elemento', '2 - Lote', 'EmpSenhaAutent', '1-1', 'Senha', 'Interno'),
  linha(6, 'Valor', 'Elemento', '2 - Lote', 'ValCBS', '1-1', '"Total valor da CBS.', 'Externo'),
].join('\n')

test('mapeamento: monta o caminho pelo sequencial do pai, mesmo com sequenciais pulados', () => {
  const tags = lerMapeamento(MAPEAMENTO)
  assert.deepEqual(
    tags.map((tag) => [tag.sequencial, tag.caminho, tag.nomeCampo, tag.interno]),
    [
      [1, 'Envio', null, true],
      [2, 'Envio/Lote', null, true],
      [5, 'Envio/Lote/Senha', 'EmpSenhaAutent', true],
      [6, 'Envio/Lote/Valor', 'ValCBS', false],
    ],
  )
  assert.equal(tags[3].descricao, 'Total valor da CBS.')
  assert.equal(tags[3].elemento, 'E')
})

test('mapeamento: pai inexistente e sequencial repetido interrompem a leitura', () => {
  assert.throws(
    () =>
      lerMapeamento(
        `${MAPEAMENTO}\n${linha(7, 'X', 'Elemento', '9 - Nada', '', '0-1', '', 'Interno')}`,
      ),
    /pai 9/,
  )
  assert.throws(
    () =>
      lerMapeamento(
        `${MAPEAMENTO}\n${linha(6, 'X', 'Elemento', '2 - Lote', '', '0-1', '', 'Interno')}`,
      ),
    /repetido/,
  )
})
