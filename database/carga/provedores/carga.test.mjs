// Rodar com: node --test database/carga/provedores/
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { lerVocabularioInvoicy } from '../vocabulario-invoicy.mjs'
import { TITULO_CAMPOS_PENDENTES, avisoDeCamposPendentes } from './avisos.mjs'
import { PROVEDORES } from './catalogo.mjs'
import { definicoesDeLeiauteTranscrito } from './leiaute-transcrito.mjs'
import { TIPO_NAO_DESCRITO } from './montar-campos.mjs'
import { camposCitados, montarErros } from './montar-erros.mjs'
import { prepararCarga } from './preparar.mjs'

function tag(sequencial, caminho, campos = []) {
  return {
    sequencial,
    caminho,
    nomeTag: caminho.slice(caminho.lastIndexOf('/') + 1),
    campos: campos.map((caminhoInvoicy) => ({ caminhoInvoicy })),
  }
}

const TAGS = [
  tag(1, 'nfse/nf/IBSCBS', ['Envio/RPS/Grupo']),
  tag(2, 'nfse/IBSCBS/valores/vBC', ['Envio/RPS/ValBC']),
  tag(3, 'nfse/pis_cofins/base_calculo', ['Envio/RPS/ValBCPIS']),
  tag(4, 'nfse/pis_cofins/aliquota_cofins', ['Envio/RPS/ValAliqCOFINS']),
  tag(5, 'nfse/parcelas/parcela/valor', ['Envio/RPS/PrcValor']),
  tag(6, 'nfse/IBSCBS/fed/pCBS', ['Envio/RPS/ValpCBS']),
  tag(7, 'nfse/IBSCBS/gTribCompraGov/pCBS', ['Envio/RPS/TribCompraGov/ValpCBS']),
]

test('erro: liga o campo citado entre parênteses, depois de "tag", em snake_case ou pelo caminho', () => {
  assert.deepEqual(camposCitados('Valor da base de cálculo (vBC) inválido.', TAGS), [
    'Envio/RPS/ValBC',
  ])
  assert.deepEqual(camposCitados('O valor da tag base_calculo deve ser maior que zero.', TAGS), [
    'Envio/RPS/ValBCPIS',
  ])
  assert.deepEqual(camposCitados('A tag aliquota_pis e/ou aliquota_cofins foi informada.', TAGS), [
    'Envio/RPS/ValAliqCOFINS',
  ])
  assert.deepEqual(camposCitados('Ao informar a tag “/nfse/nf/IBSCBS”, preencha.', TAGS), [
    'Envio/RPS/Grupo',
  ])
})

test('erro: palavra comum e nome repetido em dois grupos não viram vínculo', () => {
  // "valor" é a tag da parcela, mas aqui é só uma palavra da frase.
  assert.deepEqual(camposCitados('O valor do Tributo Federal deve ser menor.', TAGS), [])
  assert.deepEqual(camposCitados('A alíquota do CBS (pCBS) não foi preenchida.', TAGS), [])
})

test('erro: a mensagem é a variante; causa e solução só quando o material traz', () => {
  const [simples, detalhado] = montarErros(
    [
      {
        codigo: '00338',
        mensagem: 'Valor da base de cálculo (vBC) não está preenchido corretamente.',
      },
      {
        codigo: 'E0037',
        mensagem: 'Município inexistente.',
        causaProvavel: 'C',
        solucaoSugerida: 'S',
      },
    ],
    TAGS,
  )
  assert.deepEqual(simples, {
    codigo: '00338',
    origem: 'Prefeitura',
    titulo: 'Valor da base de cálculo (vBC) não está preenchido corretamente.',
    causaProvavel: null,
    solucaoSugerida: null,
    variantes: ['Valor da base de cálculo (vBC) não está preenchido corretamente.'],
    camposRelacionados: ['Envio/RPS/ValBC'],
  })
  assert.deepEqual([detalhado.causaProvavel, detalhado.solucaoSugerida], ['C', 'S'])
})

test('erro: código repetido na transcrição interrompe a carga', () => {
  const repetido = { codigo: '1', mensagem: 'x' }
  assert.throws(() => montarErros([repetido, repetido], TAGS), /repetido/)
})

test('aviso de pendentes lista os campos em ordem e sem o motivo interno; sem pendentes, nada', () => {
  assert.equal(avisoDeCamposPendentes(new Map()), null)
  const aviso = avisoDeCamposPendentes(
    new Map([
      ['indTotTrib', 'Não existe no layout de entrada.'],
      ['DocCPF', 'Bloco gReeRepRes (1.01) sem equivalente confiável no layout v8.'],
      ['DocCNPJ', 'Bloco gReeRepRes (1.01) sem equivalente confiável no layout v8.'],
    ]),
  )
  assert.equal(aviso.titulo, TITULO_CAMPOS_PENDENTES)
  assert.match(aviso.corpo, /desta versão: DocCNPJ, DocCPF, indTotTrib\.\n\nFora desta lista/)
  assert.doesNotMatch(aviso.corpo, /gReeRepRes|layout v8|XSD|planilha|a confirmar/)
})

test('leiaute transcrito: domínio vira tipo, "Agrup." vira grupo, célula vazia fica vazia', () => {
  const tags = [tag(1, 'nfse/parcelas'), tag(2, 'nfse/valor_total'), tag(3, 'nfse/observacao')]
  const definicoes = definicoesDeLeiauteTranscrito(
    {
      tags: [
        {
          sequencial: 1,
          tag: 'parcelas',
          dominio: 'Agrup. Parcela',
          tamanho: null,
          obrigatoriedade: '0-1',
          encontradaNoPdf: true,
        },
        {
          sequencial: 2,
          tag: 'valor_total',
          dominio: 'Real',
          tamanho: '15',
          obrigatoriedade: '1-1',
          encontradaNoPdf: true,
        },
        {
          sequencial: 3,
          tag: 'observacao',
          dominio: null,
          tamanho: null,
          obrigatoriedade: null,
          encontradaNoPdf: false,
        },
      ],
    },
    tags,
  )
  assert.equal(definicoes.get('nfse/parcelas').ehGrupo, true)
  assert.deepEqual(
    [definicoes.get('nfse/valor_total').tipoBase, definicoes.get('nfse/valor_total').tamanhoMaximo],
    ['decimal', '15'],
  )
  assert.equal(definicoes.get('nfse/valor_total').minimo, '1')
  assert.equal(definicoes.has('nfse/observacao'), false)
})

test('leiaute transcrito: tag trocada entre a transcrição e o mapeamento é erro', () => {
  assert.throws(
    () =>
      definicoesDeLeiauteTranscrito(
        { tags: [{ sequencial: 1, tag: 'outra', encontradaNoPdf: true }] },
        [tag(1, 'nfse/valor_total')],
      ),
    /o mapeamento diz valor_total/,
  )
})

test('material real: todos os provedores preparam sem erro e com os números conferidos', async () => {
  const { elementos } = await lerVocabularioInvoicy()
  const numeros = {}
  const cargas = {}
  for (const id of Object.keys(PROVEDORES)) {
    const carga = await prepararCarga(id, elementos)
    cargas[id] = carga
    const naoDescritos = carga.campos.filter(
      (campo) => campo.campoVersao.tipo === TIPO_NAO_DESCRITO,
    )
    numeros[id] = [
      carga.campos.length,
      carga.pendentes.size,
      carga.erros.length,
      naoDescritos.length,
    ]
  }
  // [campos, pendentes, erros, campos sem tipo no leiaute do provedor]
  assert.deepEqual(numeros, {
    'e-l': [191, 30, 0, 15],
    elotech: [113, 4, 0, 1],
    giss: [102, 14, 0, 0],
    ipm: [109, 2, 58, 41],
    issnet: [203, 30, 0, 27],
    betha: [194, 32, 0, 15],
    coplan: [181, 32, 0, 5],
    dsfnet: [190, 32, 0, 14],
    dueto: [190, 32, 0, 15],
    fiorilli: [165, 15, 0, 11],
    gespam: [188, 32, 0, 14],
    'joao-pessoa': [190, 33, 0, 14],
    rlz: [189, 32, 0, 14],
    sigcorp: [196, 32, 0, 14],
    smarapdisil: [185, 17, 0, 15],
  })

  // ABRASF: o cMun é o município do serviço; no leiaute DPS, o do prestador.
  const cMun = (id) => cargas[id].campos.find((campo) => campo.nomeCampo === 'cMun').caminhoInvoicy
  assert.equal(cMun('elotech'), 'Envio/RPS/Servico/cMun')
  assert.equal(cMun('e-l'), 'Envio/RPS/Prestador/enderPrest/cMun')
})

test('leiaute nacional: o estado vem do XSD, não da ocorrência 0-1 da configuração', async () => {
  const { elementos } = await lerVocabularioInvoicy()
  const obrigatorios = async (id) =>
    (await prepararCarga(id, elementos)).campos
      .filter(({ campoVersao }) => campoVersao.estadoPreenchimento === 'Obrigatorio')
      .map(({ nomeCampo }) => nomeCampo)
      .sort()

  // dEmis e RPSNumero vêm 0-1 na configuração do Betha; o XSD nacional exige os dois.
  const betha = await obrigatorios('betha')
  assert.deepEqual(betha, await obrigatorios('e-l'))
  assert.ok(betha.includes('dEmis') && betha.includes('RPSNumero'))
})

test('leiaute nacional: SMARAPDISIL parte da NFS-e e o número vai na NFS-e e na DPS', async () => {
  const { elementos } = await lerVocabularioInvoicy()
  const carga = await prepararCarga('smarapdisil', elementos)
  const numero = carga.campos.find(({ nomeCampo }) => nomeCampo === 'RPSNumero')
  assert.equal(
    numero.campoVersao.destinoXml,
    'NFSe/infNFSe/nNFSe | NFSe/infNFSe/nDFSe | NFSe/infNFSe/DPS/infDPS/nDPS',
  )
  assert.equal(numero.campoVersao.estadoPreenchimento, 'Obrigatorio')
  assert.equal(carga.pendentes.get('EmpnMun'), 'EmpnMun não existe no layout de entrada.')
})

const PROVEDORES_COM_FONTE = ['e-l', 'elotech']
const PROVEDORES_SEM_FONTE = ['giss', 'ipm', 'issnet']

let cargasEmCache = null

/** Prepara os provedores uma vez só para os testes de exemplo. */
function prepararParaExemplos() {
  cargasEmCache ??= (async () => {
    const { elementos } = await lerVocabularioInvoicy()
    const cargas = {}
    for (const id of Object.keys(PROVEDORES)) cargas[id] = await prepararCarga(id, elementos)
    return cargas
  })()
  return cargasEmCache
}

const campoDa = (carga, nomeCampo) => carga.campos.find((campo) => campo.nomeCampo === nomeCampo)

test('exemplo: o catálogo declara a fonte só dos provedores que a têm', () => {
  assert.equal(PROVEDORES['e-l'].fonteDeExemplo, 'dps-nacional')
  assert.equal(PROVEDORES.elotech.fonteDeExemplo, 'elotech')
  for (const id of PROVEDORES_SEM_FONTE) assert.ok(!('fonteDeExemplo' in PROVEDORES[id]), id)
})

test('exemplo: provedor com fonte gerencia todo campo; sem fonte, nenhum', async () => {
  const cargas = await prepararParaExemplos()

  for (const id of PROVEDORES_COM_FONTE) {
    assert.ok(cargas[id].fonteLida, id)
    assert.ok(cargas[id].campos.length > 0, id)
    assert.ok(
      cargas[id].campos.every(({ campoVersao }) => 'exemplo' in campoVersao),
      id,
    )
  }
  for (const id of PROVEDORES_SEM_FONTE) {
    assert.equal(cargas[id].fonteLida, null, id)
    assert.ok(cargas[id].campos.length > 0, id)
    assert.ok(
      cargas[id].campos.every(({ campoVersao }) => !('exemplo' in campoVersao)),
      id,
    )
    // O corpo que vai à API não carrega a chave: quem reenvia o valor existente é a sincronização.
    assert.ok(
      cargas[id].campos.every(
        ({ campoVersao }) => !('exemplo' in JSON.parse(JSON.stringify(campoVersao))),
      ),
      id,
    )
  }
})

test('exemplo: campo sem valor publicável de provedor com fonte vai com exemplo null', async () => {
  const cargas = await prepararParaExemplos()
  for (const id of PROVEDORES_COM_FONTE) {
    const semValor = cargas[id].campos.filter(({ campoVersao }) => campoVersao.exemplo === null)
    assert.ok(semValor.length > 0, id)
    for (const { campoVersao } of semValor) {
      const corpo = JSON.parse(JSON.stringify(campoVersao))
      assert.ok(Object.hasOwn(corpo, 'exemplo'), id)
      assert.equal(corpo.exemplo, null, id)
    }
  }
})

test('exemplo E&L: valor da DPS nacional, identificação e teste omitidos', async () => {
  const { 'e-l': carga } = await prepararParaExemplos()
  const exemplo = (nome) => campoDa(carga, nome).campoVersao.exemplo
  const motivo = (nome) => campoDa(carga, nome).decisaoDeExemplo.motivo

  assert.equal(exemplo('ValServicos'), '29155.75')
  assert.equal(exemplo('dEmis'), '2026-06-02T08:06:28-03:00')
  assert.equal(exemplo('cMun'), '3301207')

  for (const nome of ['CNPJ_prest', 'IM', 'TomaCPF', 'TomaRazaoSocial', 'TomaEndereco']) {
    assert.equal(exemplo(nome), null, nome)
    assert.equal(motivo(nome), 'IDENTIFICACAO', nome)
    assert.ok(!campoDa(carga, nome).decisaoDeExemplo.detalhe, nome)
  }
  assert.equal(exemplo('Discriminacao'), null)
  assert.equal(motivo('Discriminacao'), 'MARCADOR_TESTE')
  assert.equal(motivo('ListaNFSeRef'), 'GRUPO')
})

test('exemplo E&L: a mesma folha CST em contextos diferentes recebe o valor do seu contexto', async () => {
  const { 'e-l': carga } = await prepararParaExemplos()
  assert.equal(campoDa(carga, 'CST').campoVersao.exemplo, '01')
  assert.equal(campoDa(carga, 'CSTIBSCBS').campoVersao.exemplo, '000')
})

test('exemplo Elotech: fonte assíncrona alimenta o catálogo síncrono pelo trecho do RPS', async () => {
  const { elotech: carga } = await prepararParaExemplos()
  const exemplo = (nome) => campoDa(carga, nome).campoVersao.exemplo
  const decisao = (nome) => campoDa(carga, nome).decisaoDeExemplo

  // Numero é o número do RPS aqui, e o do endereço do tomador logo abaixo.
  assert.equal(exemplo('RPSNumero'), '579')
  assert.equal(exemplo('TomaNumero'), null)
  assert.equal(decisao('TomaNumero').motivo, 'IDENTIFICACAO')

  assert.equal(exemplo('ValServicos'), '100.00')
  assert.equal(exemplo('cMun'), '4119905')
  assert.equal(exemplo('TomaUF'), 'SC')

  assert.equal(decisao('DocChaveNFSe').motivo, 'IDENTIFICACAO')
  assert.equal(decisao('Discriminacao').motivo, 'MARCADOR_TESTE')
  assert.equal(decisao('DestEndereco').motivo, 'GRUPO')
  assert.equal(decisao('tpAmb').motivo, 'ENVELOPE')
})

test('exemplo Elotech: grafia diferente do catálogo não casa e vira dica', async () => {
  const { elotech: carga } = await prepararParaExemplos()
  const destino = campoDa(carga, 'DestNome')

  assert.equal(destino.campoVersao.exemplo, null)
  assert.equal(destino.decisaoDeExemplo.motivo, 'NAO_ESTA_NA_FONTE')
  assert.match(destino.decisaoDeExemplo.detalhe, /InfIbsCbs\/dest\/xNome/)
})

test('exemplo Elotech: os cinco valores de preenchimento de teste do IBS/CBS são descartados', async () => {
  const { elotech: carga } = await prepararParaExemplos()
  const descartados = ['CSTIBSCBS', 'cClassTrib', 'cCredPres', 'CSTReg', 'cClassTribReg']

  for (const nome of descartados) {
    assert.equal(campoDa(carga, nome).campoVersao.exemplo, null, nome)
    assert.equal(campoDa(carga, nome).decisaoDeExemplo.motivo, 'DESCARTE_EDITORIAL', nome)
  }
  // Percentuais do mesmo grupo são válidos e ficam.
  assert.equal(campoDa(carga, 'ValpDifUF').campoVersao.exemplo, '1.6')
})

test('exemplo: fonte declarada e desconhecida interrompe a preparação, sem carga pela metade', async () => {
  const { elementos } = await lerVocabularioInvoicy()
  const fonteOriginal = PROVEDORES['e-l'].fonteDeExemplo
  PROVEDORES['e-l'].fonteDeExemplo = 'fonte-inexistente'
  try {
    await assert.rejects(prepararCarga('e-l', elementos), /Fonte de exemplo desconhecida/)
  } finally {
    PROVEDORES['e-l'].fonteDeExemplo = fonteOriginal
  }
})
