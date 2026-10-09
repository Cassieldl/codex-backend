// Rodar com: node --test database/carga/provedores/
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { lerMapeamento } from './ler-mapeamento.mjs'
import { comPrefixo, definicoesPorCaminho, indexarEsquema } from './ler-xsd.mjs'
import { TIPO_NAO_DESCRITO, montarCampos } from './montar-campos.mjs'

const ESQUEMA = `<xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <xsd:simpleType name="tsValor"><xsd:restriction base="xsd:decimal"><xsd:totalDigits value="15"/><xsd:fractionDigits value="2"/><xsd:minInclusive value="0"/></xsd:restriction></xsd:simpleType>
  <xsd:simpleType name="tsSimNao"><xsd:restriction base="xsd:byte"><xsd:pattern value="1|2"/></xsd:restriction></xsd:simpleType>
  <xsd:simpleType name="tsTipo"><xsd:restriction base="xsd:string">
    <xsd:enumeration value="1"><xsd:annotation><xsd:documentation>RPS</xsd:documentation></xsd:annotation></xsd:enumeration>
    <xsd:enumeration value="2"><xsd:annotation><xsd:documentation>Cupom</xsd:documentation></xsd:annotation></xsd:enumeration>
  </xsd:restriction></xsd:simpleType>
  <xsd:simpleType name="tsCnpj"><xsd:restriction base="xsd:string"><xsd:length value="14"/></xsd:restriction></xsd:simpleType>
  <xsd:complexType name="tcCpfCnpj"><xsd:choice><xsd:element name="Cpf" type="tsCnpj"/><xsd:element name="Cnpj" type="tsCnpj"/></xsd:choice></xsd:complexType>
  <xsd:complexType name="tcDecl">
    <xsd:sequence>
      <xsd:element name="Valor" type="tsValor"/>
      <xsd:element name="Retido" type="tsSimNao" minOccurs="0"/>
      <xsd:element name="Tipo" type="tsTipo"/>
      <xsd:element name="Emissao" type="xsd:date"/>
      <xsd:element name="Prestador" type="tcCpfCnpj"/>
      <xsd:element name="Tomador" minOccurs="0"><xsd:complexType><xsd:sequence>
        <xsd:element name="Documento" type="tsCnpj"/>
      </xsd:sequence></xsd:complexType></xsd:element>
      <xsd:element name="Referencias" minOccurs="0"><xsd:complexType><xsd:choice maxOccurs="99">
        <xsd:element name="Chave" type="tsCnpj"/>
      </xsd:choice></xsd:complexType></xsd:element>
    </xsd:sequence>
  </xsd:complexType>
  <xsd:element name="Decl" type="tcDecl"/>
</xsd:schema>`

function linha(seq, tag, elemento, pai, campo, ocorrencia = '0-1') {
  return `| **${seq}** | ${tag} | ${elemento} | ${pai} | ${campo} | ${ocorrencia} | | Externo |`
}

/** O lote embrulha a declaração, como no ISS.NET: o envelope não está no XSD da declaração. */
const MAPEAMENTO = [
  linha(1, 'Lote', 'Grupo', '0 -', '', '0-1'),
  linha(2, 'Cnpj', 'Elemento', '1 - Lote', 'CNPJ_prest'),
  linha(3, 'Decl', 'Grupo', '1 - Lote', '', '0-1'),
  linha(4, 'Valor', 'Elemento', '3 - Decl', 'ValServicos'),
  linha(5, 'Retido', 'Elemento', '3 - Decl', 'ISSRetido'),
  linha(6, 'Tipo', 'Elemento', '3 - Decl', 'RPSTipo'),
  linha(7, 'Emissao', 'Elemento', '3 - Decl', 'dEmis'),
  linha(8, 'Prestador', 'Grupo', '3 - Decl', ''),
  linha(9, 'Cnpj', 'Elemento', '8 - Prestador', 'CNPJ_prest'),
  linha(10, 'Tomador', 'Grupo', '3 - Decl', ''),
  linha(11, 'Documento', 'Elemento', '10 - Tomador', 'TomaCNPJ e TomaCPF'),
  linha(12, 'Referencias', 'Grupo', '3 - Decl', ''),
  linha(13, 'Chave', 'Elemento', '12 - Referencias', 'DocChaveNFSe'),
  linha(14, 'Id', 'Atributo', '3 - Decl', 'RPSNumero e RPSSerie'),
  linha(15, 'Numero', 'Elemento', '3 - Decl', 'RPSNumero'),
].join('\n')

/** O que o resolvedor devolveria: o caminho no InvoiCy é irrelevante aqui, só precisa ser único. */
function comCampos(tags) {
  return tags.map((tag) => {
    if (tag.nomeCampo === 'RPSNumero e RPSSerie')
      return { ...tag, campos: [], compoeDe: ['RPSNumero', 'RPSSerie'] }
    if (tag.nomeCampo === 'TomaCNPJ e TomaCPF') {
      return {
        ...tag,
        campos: ['TomaCNPJ', 'TomaCPF'].map((nome) => ({
          nomeCampo: nome,
          caminhoInvoicy: `Envio/RPS/${nome}`,
          alternativa: true,
        })),
      }
    }
    return {
      ...tag,
      campos: tag.nomeCampo
        ? [{ nomeCampo: tag.nomeCampo, caminhoInvoicy: `Envio/RPS/${tag.nomeCampo}` }]
        : [],
    }
  })
}

const definicoes = comPrefixo(definicoesPorCaminho(indexarEsquema([ESQUEMA]), 'Decl'), 'Lote/')
const campos = montarCampos(comCampos(lerMapeamento(MAPEAMENTO)), definicoes, {
  prefixoDestino: 'Lote/Decl/',
})
const campo = (nome) => campos.find((item) => item.nomeCampo === nome).campoVersao

test('tipo, tamanho e máscara vêm do XSD do provedor', () => {
  assert.deepEqual(
    [campo('ValServicos').tipo, campo('ValServicos').tamanho],
    ['Numérico', 'até 15 dígitos, com 2 decimais'],
  )
  assert.deepEqual([campo('dEmis').tipo, campo('dEmis').mascara], ['Data', 'AAAA-MM-DD'])
  assert.deepEqual([campo('TomaCNPJ').tipo, campo('TomaCNPJ').tamanho], ['Texto', '14'])
})

test('domínio da enumeração com descrição, e de um padrão que só lista valores', () => {
  assert.equal(campo('RPSTipo').dominio, '1 = RPS; 2 = Cupom')
  assert.equal(campo('ISSRetido').dominio, '1, 2')
})

test('obrigatória dentro do trecho descrito; o envelope do lote não a torna condicional', () => {
  // Lote e Decl são 0-1 na configuração do InvoiCy, mas estão fora do XSD da declaração.
  assert.equal(campo('ValServicos').estadoPreenchimento, 'Obrigatorio')
  assert.equal(campo('ISSRetido').estadoPreenchimento, 'Opcional')
})

test('tag de grupo opcional é condicional, com o nome do grupo', () => {
  assert.equal(campo('TomaCNPJ').estadoPreenchimento, 'Condicional')
  assert.match(campo('TomaCNPJ').condicao, /grupo Tomador for informado/)
})

test('escolha lista as alternativas do XSD, mesmo as que o mapeamento não configurou', () => {
  const cnpj = campo('CNPJ_prest')
  assert.equal(cnpj.estadoPreenchimento, 'Condicional')
  assert.equal(cnpj.condicao, 'Informar apenas um entre: Cpf, Cnpj.')
})

test('choice de uma alternativa só segue a ocorrência da tag', () => {
  const chave = campo('DocChaveNFSe')
  assert.doesNotMatch(chave.condicao ?? '', /apenas um entre/)
  assert.match(chave.condicao, /grupo Referencias for informado/)
})

test('um campo que vai para duas tags vira uma linha; o destino exibido perde só o começo comum', () => {
  assert.equal(campos.filter((item) => item.nomeCampo === 'CNPJ_prest').length, 1)
  assert.equal(campo('CNPJ_prest').destinoXml, 'Lote/Cnpj | Prestador/Cnpj')
  assert.match(campo('CNPJ_prest').notaTransformacao, /em 2 tags do XML do provedor/)
  assert.equal(campo('CNPJ_prest').ordem, 2)
})

test('tag que recebe um campo ou outro: os dois são condicionais e a nota diz qual tag', () => {
  for (const nome of ['TomaCNPJ', 'TomaCPF']) {
    assert.match(campo(nome).condicao, /^Informar TomaCNPJ ou TomaCPF, conforme o caso\./)
    assert.equal(campo(nome).notaTransformacao, 'A tag Documento recebe TomaCNPJ ou TomaCPF.')
  }
})

test('campo que compõe outra tag leva a nota de composição', () => {
  assert.match(campo('RPSNumero').notaTransformacao, /compõe a tag Id .* RPSNumero e RPSSerie/)
})

test('tag fora do leiaute: tipo explícito e ocorrência da configuração, com nota', () => {
  const semLeiaute = montarCampos(
    comCampos(lerMapeamento(linha(1, 'Senha', 'Elemento', '0 -', 'IM', '1-1'))),
    new Map(),
  )[0].campoVersao
  assert.equal(semLeiaute.tipo, TIPO_NAO_DESCRITO)
  assert.equal(semLeiaute.estadoPreenchimento, 'Obrigatorio')
  assert.match(semLeiaute.notaPreenchimento, /não descreve esta tag/)
})
