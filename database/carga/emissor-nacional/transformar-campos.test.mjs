// Rodar com: node --test database/carga/emissor-nacional/transformar-campos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import {
  acomodarDominio,
  avaliarPreenchimento,
  corrigirCaminhoDoMaterial,
  descreverDominio,
  descreverTamanho,
  indexarDefinicoes,
  lerMapeamento,
  montarCampos,
  obrigatoriosNaNotaRegular,
  significadoSemEnumeracao,
} from './transformar-campos.mjs'

const CABECALHO = 'codigoTag;nomeTag;elemento;tipo;codigoTagPai;codigoCampo;nomeCampo;nulos'

function csv(...linhas) {
  return [CABECALHO, ...linhas].join('\n')
}

const SEM_DIFERENCAS = { campos_somente_anexo_I: [] }

function indexar(campos, obrigatorios) {
  return indexarDefinicoes(campos, SEM_DIFERENCAS, obrigatorios)
}

test('o caminho de cada tag sai da cadeia de tags pai', () => {
  const tags = lerMapeamento(
    csv(
      '1;DPS;G;C;0;0;;Sim',
      '2;infDPS;G;C;1;0;;Sim',
      '3;toma;G;C;2;0;;Sim',
      '4;CNPJ;E;C;3;42;TomaCNPJ;Não',
    ),
  )
  assert.equal(tags[3].caminho, 'DPS/infDPS/toma/CNPJ')
  assert.equal(tags[3].aceitaNulo, false)
  assert.equal(tags[0].aceitaNulo, true)
})

test('tag com pai inexistente interrompe a leitura em vez de gerar caminho errado', () => {
  assert.throws(
    () => lerMapeamento(csv('1;DPS;G;C;0;0;;Sim', '2;CNPJ;E;C;99;42;TomaCNPJ;Sim')),
    /tag pai 99/,
  )
})

test('tamanho do leiaute vira texto legível', () => {
  assert.equal(descreverTamanho('1-15V2'), '1 a 15 dígitos inteiros, com 2 decimais')
  assert.equal(descreverTamanho('1-60'), '1 a 60')
  assert.equal(descreverTamanho('14'), '14')
  assert.equal(descreverTamanho(null), null)
})

test('domínio publicado traz só os valores que o XSD vigente aceita', () => {
  const definicao = {
    dominio: [
      { valor: '0', descricao: 'NFS-e regular' },
      { valor: '1', descricao: 'NFS-e de débito' },
      { valor: '2', descricao: 'NFS-e de crédito' },
    ],
    valores_xsd: ['0'],
  }
  assert.equal(descreverDominio(definicao), '0 = NFS-e regular')
  assert.equal(
    descreverDominio({ dominio: definicao.dominio }),
    '0 = NFS-e regular; 1 = NFS-e de débito; 2 = NFS-e de crédito',
  )
})

test('domínio longo fica só com os códigos, e a descrição vai para a nota quando cabe', () => {
  const medio = Array.from({ length: 12 }, (_, i) => `${i} = Descrição do valor ${i}`).join('; ')
  const { dominio, notaExtra } = acomodarDominio(medio)
  assert.equal(dominio, '0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11')
  assert.equal(notaExtra, `Significado de cada valor: ${medio}`)

  const enorme = Array.from(
    { length: 40 },
    (_, i) => `${i} = Descrição bem longa do valor ${i}`,
  ).join('; ')
  assert.match(acomodarDominio(enorme).notaExtra, /ver a tabela do campo no leiaute/)

  assert.deepEqual(acomodarDominio('1 = Sim; 2 = Não'), {
    dominio: '1 = Sim; 2 = Não',
    notaExtra: null,
  })
})

test('o significado perde a lista de valores só quando ela existe estruturada no domínio', () => {
  const descricao = 'Tipo de retenção do ISSQN:\n\n1 - Não retido;\n2 - Retido pelo tomador;'
  assert.equal(
    significadoSemEnumeracao({ descricao, dominio: [{ valor: '1', descricao: 'Não retido' }] }),
    'Tipo de retenção do ISSQN.',
  )
  assert.equal(
    significadoSemEnumeracao({ descricao }),
    'Tipo de retenção do ISSQN: 1 - Não retido; 2 - Retido pelo tomador;',
  )
})

test('grupo opcional pela RTC vira obrigatório quando a regra o exige na nota regular', () => {
  const obrigatorios = obrigatoriosNaNotaRegular([
    {
      xpath: 'NFSe/infNFSe/DPS/infDPS/valores',
      regra: 'Este campo deve ser informado obrigatoriamente (OCOR. 1-1) quando finNFSe = 0 ou ...',
    },
    {
      xpath: 'NFSe/infNFSe/IBSCBS/valores',
      regra: 'Este campo deve ser informado obrigatoriamente (OCOR. 1-1) quando finNFSe = 0',
    },
    { xpath: 'NFSe/infNFSe/DPS/infDPS/toma', regra: 'Outra regra qualquer.' },
  ])
  assert.deepEqual([...obrigatorios], ['DPS/infDPS/valores'])

  const definicoes = indexar(
    [{ xpath_no_envio: 'DPS/infDPS/valores', ocorrencia: '0-1' }],
    obrigatorios,
  )
  assert.equal(definicoes.get('DPS/infDPS/valores').ocorrencia, '1-1')
})

test('o caminho grafado errado no material é corrigido antes de indexar', () => {
  assert.equal(
    corrigirCaminhoDoMaterial('DPS/infDPS/valorestrib/totTrib/pTotTrib'),
    'DPS/infDPS/valores/trib/totTrib/pTotTrib',
  )
  const definicoes = indexar([
    { xpath_no_envio: 'DPS/infDPS/valorestrib/totTrib/pTotTrib', ele: 'CG' },
  ])
  assert.equal(definicoes.get('DPS/infDPS/valores/trib/totTrib/pTotTrib').ele, 'CG')
})

function cenarioDeTomador() {
  const tags = lerMapeamento(
    csv(
      '1;DPS;G;C;0;0;;Sim',
      '2;infDPS;G;C;1;0;;Sim',
      '3;tpAmb;E;N;2;330;tpAmb;Sim',
      '4;toma;G;C;2;0;;Sim',
      '5;CNPJ;E;C;4;42;TomaCNPJ;Sim',
      '6;CPF;E;C;4;43;TomaCPF;Sim',
      '7;xNome;E;C;4;45;TomaRazaoSocial;Sim',
      '8;fone;E;C;4;56;TomaTelefone;Sim',
      '9;vTotTribFed;E;N;2;517;ValRetFederais;Não',
    ),
  )
  const definicoes = indexar([
    { xpath_no_envio: 'DPS/infDPS/tpAmb', ocorrencia: '1-1', ele: 'E' },
    { xpath_no_envio: 'DPS/infDPS/toma', ocorrencia: '0-1', ele: 'G' },
    { xpath_no_envio: 'DPS/infDPS/toma/CNPJ', ocorrencia: '1-1', ele: 'CE' },
    { xpath_no_envio: 'DPS/infDPS/toma/CPF', ocorrencia: '1-1', ele: 'CE' },
    { xpath_no_envio: 'DPS/infDPS/toma/xNome', ocorrencia: '1-1', ele: 'E' },
    { xpath_no_envio: 'DPS/infDPS/toma/fone', ocorrencia: '0-1', ele: 'E' },
    { xpath_no_envio: 'DPS/infDPS/vTotTribFed', ocorrencia: '0-1', ele: 'E' },
  ])
  const porCaminho = new Map(tags.map((tag) => [tag.caminho, tag]))
  const avaliar = (nome) =>
    avaliarPreenchimento(
      tags.find((tag) => tag.nomeCampo === nome),
      porCaminho,
      definicoes,
    )
  return { avaliar, tags, definicoes }
}

test('obrigatoriedade efetiva considera os grupos acima e as escolhas', () => {
  const { avaliar } = cenarioDeTomador()

  assert.deepEqual(avaliar('tpAmb'), { estado: 'Obrigatorio', condicao: null })
  assert.deepEqual(avaliar('TomaTelefone'), { estado: 'Opcional', condicao: null })
  assert.deepEqual(avaliar('TomaRazaoSocial'), {
    estado: 'Condicional',
    condicao: 'Obrigatório quando o grupo toma for informado.',
  })
  assert.deepEqual(avaliar('TomaCNPJ'), {
    estado: 'Condicional',
    condicao: 'Informar apenas um entre: CNPJ, CPF.',
  })
})

test('"não nulo" no InvoiCy não torna obrigatória uma tag que a Sefin trata como opcional', () => {
  const { avaliar, tags, definicoes } = cenarioDeTomador()
  assert.deepEqual(avaliar('ValRetFederais'), { estado: 'Opcional', condicao: null })

  const campo = montarCampos(tags, definicoes).find((item) => item.nomeCampo === 'ValRetFederais')
  assert.match(campo.campoVersao.notaPreenchimento, /configurado como não nulo/)
})

test('campo InvoiCy usado em duas tags vira uma linha só, com os dois destinos', () => {
  const tags = lerMapeamento(
    csv(
      '1;DPS;G;C;0;0;;Sim',
      '2;cPaisPrestacao;E;C;1;205;PaiPreServico;Sim',
      '3;cPaisResult;E;C;1;205;PaiPreServico;Sim',
      '4;verAplic;E;C;1;0;;Sim',
    ),
  )
  const campos = montarCampos(tags, indexar([]))

  assert.equal(campos.length, 1)
  assert.equal(campos[0].campoVersao.destinoXml, 'DPS/cPaisPrestacao | DPS/cPaisResult')
  assert.match(campos[0].campoVersao.notaTransformacao, /em 2 tags da DPS/)
  assert.match(campos[0].campoVersao.notaPreenchimento, /não descreve esta tag/)
})

test('destino maior que o formulário interrompe a montagem', () => {
  const nomeLongo = 'x'.repeat(301)
  const tags = lerMapeamento(csv(`1;${nomeLongo};E;C;0;10;Campo;Sim`))
  assert.throws(() => montarCampos(tags, indexar([])), /Destino de Campo 10/)
})
