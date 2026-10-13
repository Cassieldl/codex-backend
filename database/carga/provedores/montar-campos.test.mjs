// Rodar com: node --test database/carga/provedores/
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { lerFonteDeExemplo } from '../exemplos-xml.mjs'
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

const FONTE_DO_LOTE = { id: 'lote', raiz: 'Lote', prefixoDoEnvelope: 'Lote/Decl/' }

function lerFonteDoLote(xml) {
  return lerFonteDeExemplo([xml], FONTE_DO_LOTE)
}

const XML_DO_LOTE = `<Lote>
  <Cnpj>identificador do lote</Cnpj>
  <Decl>
    <Valor>150.00</Valor>
    <Retido>2</Retido>
    <Tipo>1</Tipo>
    <Emissao>2026-02-03</Emissao>
    <Prestador><Cnpj>identificador do prestador</Cnpj></Prestador>
    <Tomador><Documento>documento do tomador</Documento></Tomador>
    <Referencias><Chave>chave da nota</Chave></Referencias>
    <Numero>579</Numero>
  </Decl>
</Lote>`

function montarComFonte(xml) {
  return montarCampos(comCampos(lerMapeamento(MAPEAMENTO)), definicoes, {
    prefixoDestino: 'Lote/Decl/',
    fonteLida: lerFonteDoLote(xml),
  })
}

const camposComFonte = montarComFonte(XML_DO_LOTE)
const itemComFonte = (nome, lista = camposComFonte) => lista.find((item) => item.nomeCampo === nome)

/** Tag já resolvida, para os casos em que o mapeamento em tabela atrapalha mais do que ajuda. */
function tagResolvida(sequencial, caminho, nomeCampo, elemento = 'E') {
  return {
    sequencial,
    caminho,
    nomeTag: caminho.slice(caminho.lastIndexOf('/') + 1),
    elemento,
    ocorrencia: '0-1',
    campos: [{ nomeCampo, caminhoInvoicy: `Envio/RPS/${nomeCampo}` }],
  }
}

test('sem fonte de exemplo a chave exemplo não existe: o campo não é gerenciado pela carga', () => {
  assert.equal(campos.length, 9)
  assert.ok(campos.every((item) => !('exemplo' in item.campoVersao)))
  assert.ok(campos.every((item) => !('decisaoDeExemplo' in item)))
})

test('com fonte, todo campo tem a chave exemplo: o valor da fonte ou null', () => {
  assert.equal(camposComFonte.length, campos.length)
  assert.ok(camposComFonte.every((item) => 'exemplo' in item.campoVersao))

  const exemplos = Object.fromEntries(
    camposComFonte.map((item) => [item.nomeCampo, item.campoVersao.exemplo]),
  )
  assert.deepEqual(exemplos, {
    CNPJ_prest: null,
    ValServicos: '150.00',
    ISSRetido: '2',
    RPSTipo: '1',
    dEmis: '2026-02-03',
    TomaCNPJ: null,
    TomaCPF: null,
    DocChaveNFSe: 'chave da nota',
    RPSNumero: '579',
  })
})

test('a decisão do exemplo fica no item, fora do corpo que a API recebe', () => {
  const item = itemComFonte('ValServicos')
  assert.equal(item.decisaoDeExemplo.exemplo, '150.00')
  assert.ok(!('decisaoDeExemplo' in item.campoVersao))
  assert.ok(!('decisaoDeExemplo' in JSON.parse(JSON.stringify(item.campoVersao))))
})

test('campo sem valor publicável serializa exemplo null, não chave ausente', () => {
  const corpo = JSON.parse(JSON.stringify(itemComFonte('CNPJ_prest').campoVersao))
  assert.ok(Object.hasOwn(corpo, 'exemplo'))
  assert.equal(corpo.exemplo, null)
})

test('o exemplo é conferido contra o tipo e o domínio do próprio campo', () => {
  const foraDaRegra = montarComFonte(
    XML_DO_LOTE.replace('150.00', 'cento e cinquenta').replace('<Retido>2', '<Retido>3'),
  )
  const valor = itemComFonte('ValServicos', foraDaRegra)
  assert.equal(valor.campoVersao.exemplo, null)
  assert.equal(valor.decisaoDeExemplo.motivo, 'FORA_DO_TIPO')

  const retido = itemComFonte('ISSRetido', foraDaRegra)
  assert.equal(retido.campoVersao.exemplo, null)
  assert.equal(retido.decisaoDeExemplo.motivo, 'FORA_DO_DOMINIO')
})

test('casa pelo caminho completo da tag, nunca pelo destino exibido, mesmo truncado', () => {
  const bloco = (numero) => `Bloco${numero}${'Longo'.repeat(20)}`
  const blocos = [bloco(1), bloco(2), bloco(3)]
  const tags = blocos.map((nome, indice) =>
    tagResolvida(indice + 1, `Envelope/${nome}/Valor`, 'ValTotal'),
  )
  const xml = `<Envelope>${blocos.map((nome) => `<${nome}><Valor>900.00</Valor></${nome}>`).join('')}</Envelope>`
  const fonteLida = lerFonteDeExemplo([xml], {
    id: 'envelope',
    raiz: 'Envelope',
    prefixoDoEnvelope: 'Envelope/',
  })

  const [item] = montarCampos(tags, new Map(), { prefixoDestino: 'Envelope/', fonteLida })

  assert.ok(item.campoVersao.destinoXml.endsWith(' | …'))
  assert.equal(item.campoVersao.exemplo, '900.00')
})

test('destino do envelope não rouba nem empresta valor', () => {
  const cnpj = itemComFonte('CNPJ_prest')
  assert.equal(cnpj.campoVersao.exemplo, null)
  assert.deepEqual(
    cnpj.decisaoDeExemplo.destinos.map(({ caminho, motivo }) => [caminho, motivo]),
    [
      ['Lote/Cnpj', 'ENVELOPE'],
      ['Lote/Decl/Prestador/Cnpj', 'IDENTIFICACAO'],
    ],
  )

  const tags = [
    tagResolvida(1, 'Lote/Total', 'ValTotal'),
    tagResolvida(2, 'Lote/Decl/Total', 'ValTotal'),
    tagResolvida(3, 'Lote/Quantidade', 'QtdTotal'),
  ]
  const fonteLida = lerFonteDoLote(
    '<Lote><Total>1.00</Total><Decl><Total>900.00</Total><Quantidade>7</Quantidade></Decl></Lote>',
  )
  const [total, quantidade] = montarCampos(tags, new Map(), {
    prefixoDestino: 'Lote/Decl/',
    fonteLida,
  })

  assert.equal(total.campoVersao.exemplo, '900.00')
  assert.equal(quantidade.campoVersao.exemplo, null)
  assert.equal(quantidade.decisaoDeExemplo.motivo, 'ENVELOPE')
})

test('tag que recebe um campo ou outro: os dois ficam sem exemplo, mesmo com valor na fonte', () => {
  for (const nome of ['TomaCNPJ', 'TomaCPF']) {
    const item = itemComFonte(nome)
    assert.equal(item.campoVersao.exemplo, null)
    assert.equal(item.decisaoDeExemplo.motivo, 'ALTERNATIVA')
  }
})

test('campo de dois destinos: valores iguais viram o exemplo, valores diferentes não', () => {
  const tags = [
    tagResolvida(1, 'Lote/Decl/Soma', 'ValTotal'),
    tagResolvida(2, 'Lote/Decl/Subtotal', 'ValTotal'),
  ]
  const montarTotal = (xml) =>
    montarCampos(tags, new Map(), {
      prefixoDestino: 'Lote/Decl/',
      fonteLida: lerFonteDoLote(xml),
    })[0]

  const iguais = montarTotal(
    '<Lote><Decl><Soma>900.00</Soma><Subtotal>900.00</Subtotal></Decl></Lote>',
  )
  assert.equal(iguais.campoVersao.exemplo, '900.00')

  const diferentes = montarTotal(
    '<Lote><Decl><Soma>900.00</Soma><Subtotal>800.00</Subtotal></Decl></Lote>',
  )
  assert.equal(diferentes.campoVersao.exemplo, null)
  assert.equal(diferentes.decisaoDeExemplo.motivo, 'DESTINOS_DIVERGEM')
})

test('a tag de composição nunca é destino de exemplo', () => {
  const composicao = camposComFonte.filter((item) => item.nomeCampo === 'RPSNumero e RPSSerie')
  assert.deepEqual(composicao, [])
  assert.equal(itemComFonte('RPSNumero').decisaoDeExemplo.destinos.length, 1)
})

test('tag declarada como atributo ou grupo não vira exemplo, mesmo com valor na fonte', () => {
  const tags = [
    tagResolvida(1, 'Lote/Decl/Versao', 'VersaoLeiaute', 'A'),
    tagResolvida(2, 'Lote/Decl/Itens', 'ListaItens', 'G'),
    tagResolvida(3, 'Lote/Decl/Observacao', 'Observacao'),
  ]
  const fonteLida = lerFonteDoLote(
    '<Lote><Decl><Versao>dois ponto zero</Versao><Itens>varios itens</Itens><Observacao>sem pendencia</Observacao></Decl></Lote>',
  )

  const [versao, itens, observacao] = montarCampos(tags, new Map(), {
    prefixoDestino: 'Lote/Decl/',
    fonteLida,
  })

  assert.equal(observacao.campoVersao.exemplo, 'sem pendencia')
  assert.deepEqual([versao.campoVersao.exemplo, versao.decisaoDeExemplo.motivo], [null, 'ATRIBUTO'])
  assert.deepEqual([itens.campoVersao.exemplo, itens.decisaoDeExemplo.motivo], [null, 'GRUPO'])
})
