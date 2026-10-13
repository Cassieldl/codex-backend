// Rodar com: node --test database/carga/
import { strict as assert } from 'node:assert'
import { readdir, readFile } from 'node:fs/promises'
import { test } from 'node:test'

import {
  DESCARTES_EDITORIAIS,
  ehFolhaDeIdentificacao,
  escolherExemplo,
  FONTES_DE_EXEMPLO,
  lerFonteDeExemplo,
  lerFonteDeExemploDoDisco,
  MOTIVOS,
  TETO_DO_EXEMPLO,
} from './exemplos-xml.mjs'

const PASTA_DO_MATERIAL = new URL('./provedores/material/', import.meta.url)
const PREFIXO_SINCRONO = 'EnviarLoteRpsSincronoEnvio/LoteRps/ListaRps/Rps/'
const PREFIXO_ASSINCRONO = 'EnviarLoteRpsEnvio/LoteRps/ListaRps/Rps/'
const CAMINHO_GENERICO = 'DPS/infDPS/campo'

const FONTE_DPS = { id: 'dps-nacional', raiz: 'DPS', prefixoDoEnvelope: '' }
const FONTE_LOTE = {
  id: 'elotech',
  raiz: 'EnviarLoteRpsEnvio',
  prefixoDoEnvelope: PREFIXO_ASSINCRONO,
}

function fonteCom(valoresPorCaminho, { id = 'dps-nacional', grupos = [] } = {}) {
  return {
    id,
    valoresPorCaminho: new Map(
      Object.entries(valoresPorCaminho).map(([caminho, valores]) => [caminho, [valores].flat()]),
    ),
    grupos: new Set(grupos),
    folhasLidas: 0,
    foraDoEnvelope: 0,
  }
}

function destino(caminho, atributos = {}) {
  return { caminho, elemento: 'E', alternativa: false, ...atributos }
}

function escolher({ destinos, fonteLida, campoVersao = {}, prefixoDestino }) {
  return escolherExemplo({
    destinos: destinos.map((item) => (typeof item === 'string' ? destino(item) : item)),
    fonteLida,
    campoVersao: { tipo: 'Texto', dominio: null, ...campoVersao },
    prefixoDestino,
  })
}

/** Um destino, um valor: o caso mais comum das regras sobre o valor. */
function exemploDe(valor, campoVersao = {}, caminho = CAMINHO_GENERICO) {
  return escolher({
    destinos: [caminho],
    fonteLida: fonteCom({ [caminho]: valor }),
    campoVersao,
  })
}

function lerTrechoDps(xmlInterno) {
  return lerFonteDeExemplo([`<DPS><infDPS>${xmlInterno}</infDPS></DPS>`], FONTE_DPS)
}

test('leitura: casa folha por caminho completo, com namespace padrão e prefixado', () => {
  const fonteLida = lerFonteDeExemplo(
    [
      `<DPS xmlns="http://exemplo.test/nfse" xmlns:ns2="http://exemplo.test/outro" versao="1.01">
        <infDPS><prest><CNPJ>x</CNPJ></prest><ns2:Extra>z</ns2:Extra></infDPS>
      </DPS>`,
    ],
    FONTE_DPS,
  )

  assert.deepEqual(fonteLida.valoresPorCaminho.get('DPS/infDPS/prest/CNPJ'), ['x'])
  assert.deepEqual(fonteLida.valoresPorCaminho.get('DPS/infDPS/Extra'), ['z'])
  assert.equal(fonteLida.id, 'dps-nacional')
})

test('leitura: ignora a subárvore Signature inteira', () => {
  const fonteLida = lerFonteDeExemplo(
    [
      `<DPS><infDPS><tpAmb>2</tpAmb></infDPS>
        <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
          <SignedInfo><Reference><DigestValue>abc=</DigestValue></Reference></SignedInfo>
          <SignatureValue>zzz</SignatureValue>
        </Signature>
      </DPS>`,
    ],
    FONTE_DPS,
  )

  assert.deepEqual(fonteLida.valoresPorCaminho.get('DPS/infDPS/tpAmb'), ['2'])
  const caminhos = [...fonteLida.valoresPorCaminho.keys(), ...fonteLida.grupos]
  assert.deepEqual(
    caminhos.filter((caminho) => caminho.startsWith('DPS/Signature')),
    [],
  )
  assert.equal(fonteLida.folhasLidas, 1)
})

test('leitura: raiz diferente da declarada lança e nomeia as duas raízes', () => {
  assert.throws(
    () =>
      lerFonteDeExemplo(
        ['<pedRegEvento><infPedReg><tpAmb>2</tpAmb></infPedReg></pedRegEvento>'],
        FONTE_DPS,
      ),
    (erro) =>
      erro.message.includes('"dps-nacional"') &&
      erro.message.includes('exige a raiz <DPS>') &&
      erro.message.includes('<pedRegEvento>'),
  )
})

test('leitura: atributo nunca é folha, tag vazia vira "" e grupo entra em grupos', () => {
  const fonteLida = lerFonteDeExemplo(
    [
      `<DPS versao="1.01"><infDPS Id="DPS123"><toma><NIF /><obs>   </obs><serie>1</serie></toma></infDPS></DPS>`,
    ],
    FONTE_DPS,
  )

  assert.deepEqual(
    [...fonteLida.valoresPorCaminho.entries()],
    [
      ['DPS/infDPS/toma/NIF', ['']],
      ['DPS/infDPS/toma/obs', ['']],
      ['DPS/infDPS/toma/serie', ['1']],
    ],
  )
  assert.deepEqual([...fonteLida.grupos], ['DPS', 'DPS/infDPS', 'DPS/infDPS/toma'])
})

test('leitura: o prefixo do envelope sai do caminho e o que fica fora não é guardado', () => {
  const fonteLida = lerFonteDeExemplo(
    [
      `<EnviarLoteRpsEnvio><LoteRps><NumeroLote>1</NumeroLote><ListaRps><Rps>
        <InfDeclaracaoPrestacaoServico><Competencia>2026-01-01</Competencia></InfDeclaracaoPrestacaoServico>
      </Rps></ListaRps></LoteRps></EnviarLoteRpsEnvio>`,
    ],
    FONTE_LOTE,
  )

  assert.deepEqual(
    [...fonteLida.valoresPorCaminho.entries()],
    [['InfDeclaracaoPrestacaoServico/Competencia', ['2026-01-01']]],
  )
  assert.deepEqual([...fonteLida.grupos], ['InfDeclaracaoPrestacaoServico'])
  assert.equal(fonteLida.folhasLidas, 2)
  assert.equal(fonteLida.foraDoEnvelope, 1)
})

test('leitura: junta as ocorrências do mesmo caminho de vários arquivos, em ordem', () => {
  const fonteLida = lerFonteDeExemplo(
    [
      '<DPS><infDPS><tpAmb>2</tpAmb></infDPS></DPS>',
      '<DPS><infDPS><tpAmb>2</tpAmb></infDPS></DPS>',
      '<DPS><infDPS><tpAmb>1</tpAmb></infDPS></DPS>',
    ],
    FONTE_DPS,
  )

  assert.deepEqual(fonteLida.valoresPorCaminho.get('DPS/infDPS/tpAmb'), ['2', '2', '1'])
})

test('envelope: sem alias de raiz entre o envio síncrono e o assíncrono', () => {
  const fonteLida = fonteCom(
    {
      'EnviarLoteRpsEnvio/LoteRps/NumeroLote': '50000216',
      'InfDeclaracaoPrestacaoServico/Competencia': '2026-01-01',
    },
    { id: 'elotech' },
  )

  const numeroDoLote = escolher({
    destinos: ['EnviarLoteRpsSincronoEnvio/LoteRps/NumeroLote'],
    fonteLida,
    prefixoDestino: PREFIXO_SINCRONO,
  })
  assert.equal(numeroDoLote.exemplo, null)
  assert.equal(numeroDoLote.motivo, 'ENVELOPE')

  const raizAssincronaComPrefixoSincrono = escolher({
    destinos: [`${PREFIXO_ASSINCRONO}InfDeclaracaoPrestacaoServico/Competencia`],
    fonteLida,
    prefixoDestino: PREFIXO_SINCRONO,
  })
  assert.equal(raizAssincronaComPrefixoSincrono.exemplo, null)
  assert.equal(raizAssincronaComPrefixoSincrono.motivo, 'ENVELOPE')

  const raizSincronaComPrefixoAssincrono = escolher({
    destinos: [`${PREFIXO_SINCRONO}InfDeclaracaoPrestacaoServico/Competencia`],
    fonteLida,
    prefixoDestino: PREFIXO_ASSINCRONO,
  })
  assert.equal(raizSincronaComPrefixoAssincrono.exemplo, null)
  assert.equal(raizSincronaComPrefixoAssincrono.motivo, 'ENVELOPE')
})

test('envelope: o destino síncrono casa com o trecho da fonte assíncrona', () => {
  const resultado = escolher({
    destinos: [`${PREFIXO_SINCRONO}InfDeclaracaoPrestacaoServico/Competencia`],
    fonteLida: fonteCom({ 'InfDeclaracaoPrestacaoServico/Competencia': '2026-01-01' }),
    campoVersao: { tipo: 'Data' },
    prefixoDestino: PREFIXO_SINCRONO,
  })

  assert.equal(resultado.exemplo, '2026-01-01')
})

test('casamento: nunca por sufixo nem por nome de folha', () => {
  const destinoLongo = 'InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Numero'

  const fonteMaisLonga = escolher({
    destinos: [destinoLongo],
    fonteLida: fonteCom({ [`Extra/${destinoLongo}`]: 'valor' }),
  })
  assert.equal(fonteMaisLonga.exemplo, null)
  assert.equal(fonteMaisLonga.motivo, 'NAO_ESTA_NA_FONTE')

  const fonteMaisCurta = escolher({
    destinos: [destinoLongo],
    fonteLida: fonteCom({ 'Rps/IdentificacaoRps/Numero': 'valor' }),
  })
  assert.equal(fonteMaisCurta.exemplo, null)
  assert.equal(fonteMaisCurta.motivo, 'NAO_ESTA_NA_FONTE')
})

test('casamento: a mesma folha em contextos diferentes recebe o valor do seu contexto', () => {
  const fonteLida = fonteCom({
    'DPS/infDPS/valores/trib/tribFed/piscofins/CST': '01',
    'DPS/infDPS/IBSCBS/valores/trib/gIBSCBS/CST': '000',
    'InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Serie': '1',
    'InfDeclaracaoPrestacaoServico/Outro/Serie': '2',
  })
  const exemploDo = (caminho) => escolher({ destinos: [caminho], fonteLida }).exemplo

  assert.equal(exemploDo('DPS/infDPS/valores/trib/tribFed/piscofins/CST'), '01')
  assert.equal(exemploDo('DPS/infDPS/IBSCBS/valores/trib/gIBSCBS/CST'), '000')
  assert.equal(exemploDo('InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Serie'), '1')
  assert.equal(exemploDo('InfDeclaracaoPrestacaoServico/Outro/Serie'), '2')
})

test('casamento: sensível a maiúscula, e a grafia próxima é só dica no detalhe', () => {
  const caminhoDoCatalogo = 'InfDeclaracaoPrestacaoServico/InfIbsCbs/dest/XNome'
  const caminhoDaFonte = 'InfDeclaracaoPrestacaoServico/InfIbsCbs/dest/xNome'

  const resultado = escolher({
    destinos: [caminhoDoCatalogo],
    fonteLida: fonteCom({ [caminhoDaFonte]: 'ACME' }),
  })
  assert.equal(resultado.exemplo, null)
  assert.equal(resultado.motivo, 'NAO_ESTA_NA_FONTE')
  assert.ok(resultado.detalhe.includes(caminhoDaFonte))
  assert.ok(!JSON.stringify(resultado).includes('ACME'))

  const semDica = escolher({
    destinos: ['A/Serie'],
    fonteLida: fonteCom({ 'A/serie': '7' }),
  })
  assert.equal(semDica.exemplo, null)
  assert.equal(semDica.motivo, 'NAO_ESTA_NA_FONTE')
})

test('casamento: sem caminho parecido, não há dica', () => {
  const resultado = escolher({
    destinos: ['A/Serie'],
    fonteLida: fonteCom({ 'A/Tipo': '1' }),
  })

  assert.equal(resultado.motivo, 'NAO_ESTA_NA_FONTE')
  assert.equal(resultado.detalhe, null)
})

test('ambíguo: três valores diferentes no mesmo caminho não geram exemplo', () => {
  for (const valores of [
    ['alfa', 'beta', 'gama'],
    ['gama', 'beta', 'alfa'],
    ['alfa', 'alfa', 'beta'],
  ]) {
    const resultado = escolher({
      destinos: [CAMINHO_GENERICO],
      fonteLida: fonteCom({ [CAMINHO_GENERICO]: valores }),
    })
    assert.equal(resultado.exemplo, null, valores.join())
    assert.equal(resultado.motivo, 'AMBIGUO', valores.join())
  }
})

test('ambíguo: valores iguais repetidos nas duas fontes são um valor só', () => {
  const resultado = escolher({
    destinos: [CAMINHO_GENERICO],
    fonteLida: fonteCom({ [CAMINHO_GENERICO]: ['7', '7'] }),
  })

  assert.equal(resultado.exemplo, '7')
})

test('destinos do mesmo campo: valores diferentes divergem e iguais valem', () => {
  const fonteLida = fonteCom({ 'A/um': 'alfa', 'A/dois': 'beta', 'A/tres': 'alfa' })

  const divergem = escolher({ destinos: ['A/um', 'A/dois'], fonteLida })
  assert.equal(divergem.exemplo, null)
  assert.equal(divergem.motivo, 'DESTINOS_DIVERGEM')

  const iguais = escolher({ destinos: ['A/um', 'A/tres'], fonteLida })
  assert.equal(iguais.exemplo, 'alfa')
  assert.deepEqual(
    iguais.destinos.map(({ caminho, estagio }) => [caminho, estagio]),
    [
      ['A/um', 'aceito'],
      ['A/tres', 'aceito'],
    ],
  )
})

test('destinos do mesmo campo: um destino de identificação contamina o outro, em qualquer ordem', () => {
  const fonteLida = fonteCom({ 'A/um': 'alfa', 'DPS/infDPS/prest/CNPJ': '11222333000144' })

  for (const destinos of [
    ['A/um', 'DPS/infDPS/prest/CNPJ'],
    ['DPS/infDPS/prest/CNPJ', 'A/um'],
  ]) {
    const resultado = escolher({ destinos, fonteLida })
    assert.equal(resultado.exemplo, null)
    assert.equal(resultado.motivo, 'IDENTIFICACAO')
  }
})

test('destinos do mesmo campo: o motivo é o do primeiro descartado, ou o do primeiro sem valor', () => {
  const fonteLida = fonteCom({ 'A/x': ['alfa', 'beta'], 'A/y': 'teste', 'A/z': 'ab' })

  const descartes = escolher({ destinos: ['A/z', 'A/x', 'A/y'], fonteLida })
  assert.equal(descartes.motivo, 'AMBIGUO')

  const semValor = escolher({
    destinos: [
      destino('A/z', { elemento: 'A' }),
      destino('A/ausente'),
      destino('A/z', { alternativa: true }),
    ],
    fonteLida,
  })
  assert.equal(semValor.motivo, 'ATRIBUTO')
  assert.equal(semValor.exemplo, null)
})

test('campo sem nenhum destino fica sem exemplo, sem lançar', () => {
  const resultado = escolher({ destinos: [], fonteLida: fonteCom({}) })

  assert.equal(resultado.exemplo, null)
  assert.equal(resultado.motivo, 'NAO_ESTA_NA_FONTE')
  assert.deepEqual(resultado.destinos, [])
})

test('regras do destino: atributo, grupo, grupo pela fonte e alternativa não recebem exemplo', () => {
  const fonteLida = fonteCom(
    { 'A/atributo': 'valor', 'A/grupo': 'valor', 'A/grupoCg': 'valor', 'A/alternativa': 'valor' },
    { grupos: ['A/end'] },
  )
  const motivoDe = (atributos, caminho) => {
    const resultado = escolher({ destinos: [destino(caminho, atributos)], fonteLida })
    assert.equal(resultado.exemplo, null, caminho)
    return resultado.motivo
  }

  assert.equal(motivoDe({ elemento: 'A' }, 'A/atributo'), 'ATRIBUTO')
  assert.equal(motivoDe({ elemento: 'G' }, 'A/grupo'), 'GRUPO')
  assert.equal(motivoDe({ elemento: 'CG' }, 'A/grupoCg'), 'GRUPO')
  assert.equal(motivoDe({ alternativa: true }, 'A/alternativa'), 'ALTERNATIVA')

  const grupoPelaFonte = escolher({ destinos: [destino('A/end', { elemento: 'E' })], fonteLida })
  assert.equal(grupoPelaFonte.motivo, 'GRUPO')
  assert.ok(grupoPelaFonte.detalhe.includes('filhos'))
})

test('regras do destino: tag vazia na fonte e valor vazio têm motivo próprio', () => {
  const fonteLida = lerTrechoDps('<vazia></vazia><espaco>   </espaco><cod></cod><cod>82</cod>')
  const exemploDoCaminho = (nome) => escolher({ destinos: [`DPS/infDPS/${nome}`], fonteLida })

  for (const nome of ['vazia', 'espaco']) {
    const resultado = exemploDoCaminho(nome)
    assert.equal(resultado.exemplo, null, nome)
    assert.equal(resultado.motivo, 'FONTE_COM_VALOR_VAZIO', nome)
  }
  assert.equal(exemploDoCaminho('cod').exemplo, '82')
})

test('identificação: todos os nomes da lista por nome de folha', () => {
  const nomes = [
    'CNPJ',
    'CPF',
    'CAEPF',
    'NIF',
    'IM',
    'Cnpj',
    'Cpf',
    'NifTomador',
    'InscricaoMunicipal',
    'InscricaoEstadual',
    'inscImobFisc',
    'cCIB',
    'xNome',
    'XNome',
    'RazaoSocial',
    'fone',
    'Telefone',
    'email',
    'Email',
    'xLgr',
    'nro',
    'xCpl',
    'xBairro',
    'CEP',
    'Cep',
    'cEndPost',
    'xCidade',
    'xEstProvReg',
    'chSubstda',
    'chNFSe',
    'chNFe',
    'refNFSe',
  ]

  assert.equal(nomes.length, 32)
  for (const nome of nomes) {
    assert.equal(ehFolhaDeIdentificacao(`Raiz/Grupo/${nome}`), true, nome)
  }
})

test('identificação: códigos públicos e dados de documento fiscal ficam de fora', () => {
  for (const nome of [
    'cMun',
    'CodigoMunicipio',
    'cLocEmi',
    'Uf',
    'cPais',
    'CodigoPais',
    'cNaoNIF',
    'cNaoNif',
    'Serie',
    'Tipo',
    'nDPS',
  ]) {
    assert.equal(ehFolhaDeIdentificacao(`Raiz/Grupo/${nome}`), false, nome)
  }
})

test('identificação: comparação exata, sensível a maiúscula', () => {
  assert.equal(ehFolhaDeIdentificacao('Raiz/Grupo/cnpj'), false)
  assert.equal(ehFolhaDeIdentificacao('Raiz/Grupo/xnome'), false)
  assert.equal(ehFolhaDeIdentificacao('Raiz/Grupo/Cnpj2'), false)
})

test('identificação: Numero, Endereco, Complemento e Bairro dependem do pai direto', () => {
  const lote = 'EnviarLoteRpsEnvio/LoteRps/ListaRps/Rps/InfDeclaracaoPrestacaoServico'

  assert.equal(ehFolhaDeIdentificacao(`${lote}/Tomador/Endereco/Numero`), true)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/InfIbsCbs/dest/end/Numero`), true)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/Tomador/Endereco/Endereco`), true)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/InfIbsCbs/imovel/end/Complemento`), true)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/Tomador/Endereco/Bairro`), true)

  assert.equal(ehFolhaDeIdentificacao(`${lote}/Rps/IdentificacaoRps/Numero`), false)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/Outro/Complemento`), false)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/Outro/Bairro`), false)
  assert.equal(ehFolhaDeIdentificacao(`${lote}/Tomador/Endereco`), false)
  assert.equal(ehFolhaDeIdentificacao('Numero'), false)
})

test('identificação: o número do RPS é exemplo, o número do endereço não', () => {
  const rps = 'InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Numero'
  const endereco = 'InfDeclaracaoPrestacaoServico/Tomador/Endereco/Numero'
  const fonteLida = fonteCom({ [rps]: '579', [endereco]: '47' }, { id: 'elotech' })

  const numeroDoRps = escolher({ destinos: [rps], fonteLida, campoVersao: { tipo: 'Numérico' } })
  assert.equal(numeroDoRps.exemplo, '579')

  const numeroDoEndereco = escolher({ destinos: [endereco], fonteLida })
  assert.equal(numeroDoEndereco.exemplo, null)
  assert.equal(numeroDoEndereco.motivo, 'IDENTIFICACAO')
})

test('identificação vale com o valor presente e com a cópia já esvaziada, e nunca expõe o valor', () => {
  for (const valor of ['99999999999999', '']) {
    const resultado = escolher({
      destinos: ['DPS/infDPS/prest/CNPJ'],
      fonteLida: fonteCom({ 'DPS/infDPS/prest/CNPJ': valor }),
    })

    assert.equal(resultado.exemplo, null)
    assert.equal(resultado.motivo, 'IDENTIFICACAO')
    assert.equal(resultado.detalhe, null)
    assert.ok(!JSON.stringify(resultado).includes('9999'))
  }
})

test('marcador de teste: palavra inteira, sem diferenciar maiúscula', () => {
  for (const valor of ['Teste de emissao', 'teste', 'Teste', 'TEST', 'a test']) {
    const resultado = exemploDe(valor)
    assert.equal(resultado.exemplo, null, valor)
    assert.equal(resultado.motivo, 'MARCADOR_TESTE', valor)
  }
  for (const valor of ['Testemunha', 'Contestado']) {
    assert.equal(exemploDe(valor).exemplo, valor)
  }
})

test('marcador de letra repetida: só letra, e nunca um código do domínio do campo', () => {
  for (const valor of ['a', 'aaaaaaaa']) {
    const resultado = exemploDe(valor)
    assert.equal(resultado.exemplo, null, valor)
    assert.equal(resultado.motivo, 'MARCADOR_LETRA_REPETIDA', valor)
  }

  assert.equal(exemploDe('S', { dominio: 'S, N' }).exemplo, 'S')
  assert.equal(exemploDe('N', { dominio: 'S = Sim; N = Não' }).exemplo, 'N')
  assert.equal(exemploDe('S').motivo, 'MARCADOR_LETRA_REPETIDA')
})

test('marcador: dígito repetido, texto curto e zero decimal são valores válidos', () => {
  assert.equal(exemploDe('99999999999999').exemplo, '99999999999999')
  assert.equal(exemploDe('ab').exemplo, 'ab')
  assert.equal(exemploDe('0.00').exemplo, '0.00')
})

test('descarte editorial: os cinco caminhos da fonte Elotech, e só nela', () => {
  const grupo = 'InfDeclaracaoPrestacaoServico/InfIbsCbs/valores/trib/gIBSCBS'
  const caminhos = [
    [`${grupo}/Cst`, '123'],
    [`${grupo}/cClassTrib`, '745896'],
    [`${grupo}/cCredPres`, '23'],
    [`${grupo}/gTribRegular/CstReg`, '859'],
    [`${grupo}/gTribRegular/cClassTribReg`, '123569'],
  ]

  assert.deepEqual(
    [...DESCARTES_EDITORIAIS.elotech.keys()],
    caminhos.map(([caminho]) => caminho),
  )
  for (const [caminho, valor] of caminhos) {
    const daElotech = escolher({
      destinos: [caminho],
      fonteLida: fonteCom({ [caminho]: valor }, { id: 'elotech' }),
    })
    assert.equal(daElotech.exemplo, null, caminho)
    assert.equal(daElotech.motivo, 'DESCARTE_EDITORIAL', caminho)

    const deOutraFonte = escolher({
      destinos: [caminho],
      fonteLida: fonteCom({ [caminho]: valor }, { id: 'dps-nacional' }),
    })
    assert.equal(deOutraFonte.exemplo, valor, caminho)
  }

  const percentual = escolher({
    destinos: [`${grupo}/gDif/pDifUF`],
    fonteLida: fonteCom({ [`${grupo}/gDif/pDifUF`]: '1.6' }, { id: 'elotech' }),
    campoVersao: { tipo: 'Numérico' },
  })
  assert.equal(percentual.exemplo, '1.6')
})

test('conformidade: o tipo Numérico recusa texto e vírgula decimal', () => {
  const campoNumerico = { tipo: 'Numérico' }

  for (const valor of ['te', '1,5']) {
    const resultado = exemploDe(valor, campoNumerico)
    assert.equal(resultado.exemplo, null, valor)
    assert.equal(resultado.motivo, 'FORA_DO_TIPO', valor)
  }
  assert.equal(exemploDe('2.00', campoNumerico).exemplo, '2.00')
  assert.equal(exemploDe('-3', campoNumerico).exemplo, '-3')
})

test('conformidade: Data exige AAAA-MM-DD', () => {
  const resultado = exemploDe('2026-1-1', { tipo: 'Data' })

  assert.equal(resultado.exemplo, null)
  assert.equal(resultado.motivo, 'FORA_DO_TIPO')
  assert.equal(exemploDe('2026-02-06', { tipo: 'Data' }).exemplo, '2026-02-06')
})

test('conformidade: Data e hora exige o horário', () => {
  const campo = { tipo: 'Data e hora' }

  assert.equal(exemploDe('2026-06-02T08:06:28-03:00', campo).exemplo, '2026-06-02T08:06:28-03:00')
  assert.equal(exemploDe('2026-06-02T08:06:28.123', campo).exemplo, '2026-06-02T08:06:28.123')
  const semHorario = exemploDe('2026-06-02', campo)
  assert.equal(semHorario.exemplo, null)
  assert.equal(semHorario.motivo, 'FORA_DO_TIPO')
})

test('conformidade: o valor tem de ser um dos códigos do domínio, nas duas formas de escrevê-lo', () => {
  for (const dominio of ['1 = Não retido; 2 = Retido', '1, 2']) {
    assert.equal(exemploDe('2', { dominio }).exemplo, '2', dominio)

    const foraDoDominio = exemploDe('3', { dominio })
    assert.equal(foraDoDominio.exemplo, null, dominio)
    assert.equal(foraDoDominio.motivo, 'FORA_DO_DOMINIO', dominio)

    const comZero = exemploDe('01', { dominio })
    assert.equal(comZero.exemplo, null, `01 em ${dominio}`)
    assert.equal(comZero.motivo, 'FORA_DO_DOMINIO', `01 em ${dominio}`)
  }
})

test('conformidade: o teto de 500 caracteres usa literais e nunca lança', () => {
  const noTeto = 'ab'.repeat(250)

  assert.equal(TETO_DO_EXEMPLO, 500)
  assert.equal(noTeto.length, 500)
  assert.equal(exemploDe(noTeto).exemplo, noTeto)

  const acima = exemploDe(`${noTeto}c`)
  assert.equal(acima.exemplo, null)
  assert.equal(acima.motivo, 'ACIMA_DO_TETO')
})

test('ordem das regras: identificação antes de marcador e de ambiguidade; ambiguidade antes de marcador', () => {
  const marcadorDeIdentificacao = escolher({
    destinos: ['X/Tomador/RazaoSocial'],
    fonteLida: fonteCom({ 'X/Tomador/RazaoSocial': 'teste wendy' }),
  })
  assert.equal(marcadorDeIdentificacao.motivo, 'IDENTIFICACAO')

  const ambiguoDeIdentificacao = escolher({
    destinos: ['DPS/infDPS/prest/CNPJ'],
    fonteLida: fonteCom({ 'DPS/infDPS/prest/CNPJ': ['111', '222'] }),
  })
  assert.equal(ambiguoDeIdentificacao.motivo, 'IDENTIFICACAO')

  const marcadorAmbiguo = escolher({
    destinos: [CAMINHO_GENERICO],
    fonteLida: fonteCom({ [CAMINHO_GENERICO]: ['teste a', 'teste b'] }),
  })
  assert.equal(marcadorAmbiguo.motivo, 'AMBIGUO')
})

test('o grupo da fonte vence um valor com o mesmo caminho', () => {
  const resultado = escolher({
    destinos: ['A/end'],
    fonteLida: fonteCom({ 'A/end': 'qualquer' }, { grupos: ['A/end'] }),
  })

  assert.equal(resultado.motivo, 'GRUPO')
  assert.equal(resultado.exemplo, null)
})

test('toda chave de MOTIVOS usada pelas regras tem texto', () => {
  assert.deepEqual(Object.keys(MOTIVOS).sort(), [
    'ACIMA_DO_TETO',
    'ALTERNATIVA',
    'AMBIGUO',
    'ATRIBUTO',
    'DESCARTE_EDITORIAL',
    'DESTINOS_DIVERGEM',
    'ENVELOPE',
    'FONTE_COM_VALOR_VAZIO',
    'FORA_DO_DOMINIO',
    'FORA_DO_TIPO',
    'GRUPO',
    'IDENTIFICACAO',
    'MARCADOR_LETRA_REPETIDA',
    'MARCADOR_TESTE',
    'NAO_ESTA_NA_FONTE',
  ])
  assert.equal(MOTIVOS.ACIMA_DO_TETO, 'o valor passa de 500 caracteres')
})

async function lerTextoDoMaterial(arquivo) {
  return readFile(new URL(arquivo, PASTA_DO_MATERIAL), 'utf8')
}

test('material versionado: sem assinatura, sem senha, sem certificado', async () => {
  const arquivos = Object.values(FONTES_DE_EXEMPLO).flatMap((fonte) => fonte.arquivos)

  assert.equal(arquivos.length, 3)
  for (const arquivo of arquivos) {
    const texto = await lerTextoDoMaterial(arquivo)
    assert.ok(texto.includes('<'), arquivo)
    for (const proibido of ['<Signature', '<Senha', 'X509', 'SignatureValue']) {
      assert.ok(!texto.includes(proibido), `${arquivo} contém ${proibido}`)
    }
  }
})

test('material versionado: toda folha de identificação está vazia, o resto é cópia fiel', async () => {
  const contagemDeIdentificacao = {}
  for (const fonte of Object.values(FONTES_DE_EXEMPLO)) {
    const textos = await Promise.all(fonte.arquivos.map(lerTextoDoMaterial))
    // Sem prefixo, para conferir também o que o envelope deixa fora da leitura.
    const completa = lerFonteDeExemplo(textos, { ...fonte, prefixoDoEnvelope: '' })

    let folhasDeIdentificacao = 0
    for (const [caminho, valores] of completa.valoresPorCaminho) {
      if (!ehFolhaDeIdentificacao(caminho)) continue
      folhasDeIdentificacao += valores.length
      assert.deepEqual(
        valores.filter((valor) => valor !== ''),
        [],
        `${caminho} traz valor`,
      )
    }
    contagemDeIdentificacao[fonte.id] = folhasDeIdentificacao
  }

  assert.deepEqual(contagemDeIdentificacao, { 'dps-nacional': 18, elotech: 43 })
})

test('material versionado: valores que não identificam ninguém continuam lá', async () => {
  const dps = await lerFonteDeExemploDoDisco('dps-nacional')
  assert.deepEqual(dps.valoresPorCaminho.get('DPS/infDPS/valores/vServPrest/vServ'), [
    '29155.75',
    '29155.75',
  ])
  assert.deepEqual(dps.valoresPorCaminho.get('DPS/infDPS/dhEmi'), [
    '2026-06-02T08:06:28-03:00',
    '2026-06-02T08:06:28-03:00',
  ])
  assert.deepEqual(dps.valoresPorCaminho.get('DPS/infDPS/serv/cServ/xDescServ'), ['teste', 'teste'])
  assert.deepEqual(dps.valoresPorCaminho.get('DPS/infDPS/IBSCBS/valores/trib/gIBSCBS/CST'), ['000'])

  const elotech = await lerFonteDeExemploDoDisco('elotech')
  assert.deepEqual(
    elotech.valoresPorCaminho.get('InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Numero'),
    ['579'],
  )
  assert.equal(elotech.foraDoEnvelope, 7)
})

test('material real: decisões sobre o exemplo da DPS Nacional', async () => {
  const fonteLida = await lerFonteDeExemploDoDisco('dps-nacional')
  const decidir = (caminho, campoVersao) =>
    escolher({ destinos: [caminho], fonteLida, campoVersao })

  assert.equal(
    decidir('DPS/infDPS/valores/vServPrest/vServ', { tipo: 'Numérico' }).exemplo,
    '29155.75',
  )
  assert.equal(decidir('DPS/infDPS/prest/CNPJ').motivo, 'IDENTIFICACAO')
  assert.equal(decidir('DPS/infDPS/toma/CPF').motivo, 'IDENTIFICACAO')
  assert.equal(decidir('DPS/infDPS/serv/cServ/xDescServ').motivo, 'MARCADOR_TESTE')
  assert.equal(decidir('DPS/infDPS/valores/trib/tribFed/piscofins/CST').exemplo, '01')
  assert.equal(decidir('DPS/infDPS/IBSCBS/valores/trib/gIBSCBS/CST').exemplo, '000')
})

test('material real: decisões sobre o exemplo da Elotech, com o destino do envio síncrono', async () => {
  const fonteLida = await lerFonteDeExemploDoDisco('elotech')
  const decidir = (relativo, atributos = {}) =>
    escolher({
      destinos: [destino(`${PREFIXO_SINCRONO}${relativo}`, atributos)],
      fonteLida,
      prefixoDestino: PREFIXO_SINCRONO,
    })
  const declaracao = 'InfDeclaracaoPrestacaoServico'

  assert.equal(decidir(`${declaracao}/Rps/IdentificacaoRps/Numero`).exemplo, '579')
  assert.equal(decidir(`${declaracao}/Tomador/Endereco/Numero`).motivo, 'IDENTIFICACAO')
  assert.equal(decidir(`${declaracao}/InfIbsCbs/gRefNFSe/refNFSe`).motivo, 'IDENTIFICACAO')
  assert.equal(decidir(`${declaracao}/Servico/Discriminacao`).motivo, 'MARCADOR_TESTE')
  assert.equal(
    decidir(`${declaracao}/InfIbsCbs/valores/trib/gIBSCBS/Cst`).motivo,
    'DESCARTE_EDITORIAL',
  )
  assert.equal(decidir(`${declaracao}/InfIbsCbs/dest/end`, { elemento: 'E' }).motivo, 'GRUPO')

  const grafia = decidir(`${declaracao}/InfIbsCbs/dest/XNome`)
  assert.equal(grafia.motivo, 'NAO_ESTA_NA_FONTE')
  assert.ok(grafia.detalhe.includes(`${declaracao}/InfIbsCbs/dest/xNome`))
})

test('disco: fonte desconhecida e arquivo ausente interrompem antes de qualquer outra coisa', async () => {
  await assert.rejects(() => lerFonteDeExemploDoDisco('inexistente'), /desconhecida.*inexistente/)

  FONTES_DE_EXEMPLO.ausente = {
    id: 'ausente',
    raiz: 'DPS',
    prefixoDoEnvelope: '',
    arquivos: ['dps-nacional-1.01/exemplos/nao-existe.xml'],
  }
  try {
    await assert.rejects(
      () => lerFonteDeExemploDoDisco('ausente'),
      /ausente.*dps-nacional-1\.01\/exemplos\/nao-existe\.xml/,
    )
  } finally {
    delete FONTES_DE_EXEMPLO.ausente
  }
})

test('fontes: o exemplo de evento de cancelamento nunca é fonte e tudo fica no material', async () => {
  assert.deepEqual(Object.keys(FONTES_DE_EXEMPLO), ['dps-nacional', 'elotech'])
  for (const [id, fonte] of Object.entries(FONTES_DE_EXEMPLO)) {
    assert.equal(fonte.id, id)
    for (const arquivo of fonte.arquivos) {
      assert.ok(!/evento/i.test(arquivo), arquivo)
      assert.ok(!arquivo.split('/').includes('..'), arquivo)
      assert.ok(
        new URL(arquivo, PASTA_DO_MATERIAL).href.startsWith(PASTA_DO_MATERIAL.href),
        arquivo,
      )
    }
  }

  const copiados = await readdir(new URL('dps-nacional-1.01/exemplos/', PASTA_DO_MATERIAL))
  assert.deepEqual(copiados.sort(), ['README.md', 'exemplo_dps.xml', 'exemplo_dps_ibscbs.xml'])
})
