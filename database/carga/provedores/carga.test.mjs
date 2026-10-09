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

test('aviso de pendentes agrupa os campos pelo motivo; sem pendentes, não há aviso', () => {
  assert.equal(avisoDeCamposPendentes(new Map()), null)
  const aviso = avisoDeCamposPendentes(
    new Map([
      ['DocCNPJ', 'Bloco gReeRepRes.'],
      ['indTotTrib', 'Não existe.'],
      ['DocCPF', 'Bloco gReeRepRes.'],
    ]),
  )
  assert.equal(aviso.titulo, TITULO_CAMPOS_PENDENTES)
  assert.match(aviso.corpo, /\n- DocCNPJ, DocCPF: Bloco gReeRepRes\.\n- indTotTrib: Não existe\.$/)
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

test('material real: os cinco provedores preparam sem erro e com os números conferidos', async () => {
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
  })

  // ABRASF: o cMun é o município do serviço; no leiaute DPS, o do prestador.
  const cMun = (id) => cargas[id].campos.find((campo) => campo.nomeCampo === 'cMun').caminhoInvoicy
  assert.equal(cMun('elotech'), 'Envio/RPS/Servico/cMun')
  assert.equal(cMun('e-l'), 'Envio/RPS/Prestador/enderPrest/cMun')
})
