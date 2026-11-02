// Rodar com: node --test database/carga/relatorio-exemplos.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { FONTES_DE_EXEMPLO, MOTIVOS, escolherExemplo, lerFonteDeExemplo } from './exemplos-xml.mjs'
import {
  ErroDeUso,
  ORDEM_DOS_MOTIVOS,
  contagemDeExemplos,
  executarRelatorio,
  formatarRelatorio,
  lerArgumentos,
  montarProvedorDoRelatorio,
  montarRelatorioDeExemplos,
} from './relatorio-exemplos.mjs'

function lerFonte(xml) {
  return lerFonteDeExemplo([xml], FONTES_DE_EXEMPLO['dps-nacional'])
}

/** Campo como a carga o monta, com a decisão do `escolherExemplo` de verdade. */
function campoDaCarga({ nomeCampo, caminhoInvoicy, caminho, ordem, fonteLida }) {
  const campoVersao = { tipo: 'Texto', dominio: null, ordem }
  const destinos = [{ caminho, elemento: 'E', alternativa: false }]
  return {
    nomeCampo,
    caminhoInvoicy,
    campoVersao,
    decisaoDeExemplo: escolherExemplo({ destinos, fonteLida, campoVersao }),
  }
}

function provedorDeTeste(campos, fonteLida) {
  return montarProvedorDoRelatorio({
    id: 'provedor-de-teste',
    nome: 'Provedor de Teste 1.0',
    fonteLida,
    campos,
  })
}

function relatorioDe(provedores, naoGerenciados = []) {
  return { provedores, naoGerenciados }
}

function provedorMontado({ aceitos = [], semExemplo = [] }) {
  return {
    id: 'provedor-montado',
    nome: 'Provedor Montado 1.0',
    fonteId: 'dps-nacional',
    arquivos: 2,
    folhasLidas: 90,
    caminhosDistintos: 48,
    foraDoEnvelope: 0,
    total: aceitos.length + semExemplo.length,
    aceitos,
    semExemplo,
  }
}

function descartes(motivo, quantidade) {
  return Array.from({ length: quantidade }, (_, indice) => ({
    campo: `Campo ${motivo} ${indice + 1}`,
    tag: `Raiz/Grupo/${motivo}${indice + 1}`,
    motivo,
    detalhe: null,
  }))
}

function trechoDaSecao(texto, titulo) {
  const linhas = texto.split('\n')
  const inicio = linhas.indexOf(titulo)
  assert.notEqual(inicio, -1, `seção "${titulo}" ausente`)
  const fim = linhas.indexOf('', inicio)
  return linhas.slice(inicio + 1, fim)
}

const XML_DE_TRES_CAMPOS =
  '<DPS><infDPS><tpAmb>Homologacao</tpAmb><serie>Serie principal</serie>' +
  '<nDPS>Documento seiscentos</nDPS></infDPS></DPS>'

test('os aceitos saem na ordem do campo, nunca na de inserção nem na inversa', () => {
  const fonteLida = lerFonte(XML_DE_TRES_CAMPOS)
  const campos = [
    campoDaCarga({
      nomeCampo: 'Ambiente',
      caminhoInvoicy: 'Envio/RPS/Ambiente',
      caminho: 'DPS/infDPS/tpAmb',
      ordem: 30,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'Serie',
      caminhoInvoicy: 'Envio/RPS/Serie',
      caminho: 'DPS/infDPS/serie',
      ordem: 10,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'Numero',
      caminhoInvoicy: 'Envio/RPS/Numero',
      caminho: 'DPS/infDPS/nDPS',
      ordem: 20,
      fonteLida,
    }),
  ]

  const { aceitos } = provedorDeTeste(campos, fonteLida)

  assert.deepEqual(
    aceitos.map(({ campo, tag, valor, ordem }) => [campo, tag, valor, ordem]),
    [
      ['Serie', 'DPS/infDPS/serie', 'Serie principal', 10],
      ['Numero', 'DPS/infDPS/nDPS', 'Documento seiscentos', 20],
      ['Ambiente', 'DPS/infDPS/tpAmb', 'Homologacao', 30],
    ],
  )
})

test('na mesma ordem, o caminho do campo desempata', () => {
  const fonteLida = lerFonte(XML_DE_TRES_CAMPOS)
  const campos = [
    campoDaCarga({
      nomeCampo: 'Servico',
      caminhoInvoicy: 'Envio/RPS/Servico',
      caminho: 'DPS/infDPS/tpAmb',
      ordem: 5,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'Tomador',
      caminhoInvoicy: 'Envio/RPS/Tomador',
      caminho: 'DPS/infDPS/serie',
      ordem: 5,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'Prestador',
      caminhoInvoicy: 'Envio/RPS/Prestador',
      caminho: 'DPS/infDPS/nDPS',
      ordem: 5,
      fonteLida,
    }),
  ]

  const { aceitos } = provedorDeTeste(campos, fonteLida)

  assert.deepEqual(
    aceitos.map(({ campo }) => campo),
    ['Prestador', 'Servico', 'Tomador'],
  )
})

test('o provedor montado traz as contagens da fonte, o total e o que ficou sem exemplo', () => {
  const fonteLida = lerFonte(XML_DE_TRES_CAMPOS.replace('</infDPS>', '<teste>x</teste></infDPS>'))
  const campos = [
    campoDaCarga({
      nomeCampo: 'Serie',
      caminhoInvoicy: 'Envio/RPS/Serie',
      caminho: 'DPS/infDPS/serie',
      ordem: 1,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'Cidade',
      caminhoInvoicy: 'Envio/RPS/Cidade',
      caminho: 'DPS/infDPS/cLocEmi',
      ordem: 2,
      fonteLida,
    }),
  ]

  const provedor = provedorDeTeste(campos, fonteLida)

  assert.deepEqual(
    {
      id: provedor.id,
      nome: provedor.nome,
      fonteId: provedor.fonteId,
      arquivos: provedor.arquivos,
      folhasLidas: provedor.folhasLidas,
      caminhosDistintos: provedor.caminhosDistintos,
      foraDoEnvelope: provedor.foraDoEnvelope,
      total: provedor.total,
    },
    {
      id: 'provedor-de-teste',
      nome: 'Provedor de Teste 1.0',
      fonteId: 'dps-nacional',
      arquivos: 2,
      folhasLidas: 4,
      caminhosDistintos: 4,
      foraDoEnvelope: 0,
      total: 2,
    },
  )
  assert.deepEqual(provedor.semExemplo, [
    {
      campo: 'Cidade',
      tag: 'DPS/infDPS/cLocEmi',
      motivo: 'NAO_ESTA_NA_FONTE',
      detalhe: null,
    },
  ])
})

test('a tag exibida perde o prefixo do envelope do lote', () => {
  const fonteLida = lerFonteDeExemplo(
    [
      '<EnviarLoteRpsEnvio><LoteRps><ListaRps><Rps><Servico><Discriminacao>Consultoria mensal</Discriminacao></Servico></Rps></ListaRps></LoteRps></EnviarLoteRpsEnvio>',
    ],
    FONTES_DE_EXEMPLO.elotech,
  )
  const prefixoDestino = 'EnviarLoteRpsSincronoEnvio/LoteRps/ListaRps/Rps/'
  const campoVersao = { tipo: 'Texto', dominio: null, ordem: 1 }
  const destinos = [
    { caminho: `${prefixoDestino}Servico/Discriminacao`, elemento: 'E', alternativa: false },
    { caminho: 'EnviarLoteRpsSincronoEnvio/IdentificacaoRequerente/Homologa', elemento: 'E' },
  ]

  const provedor = montarProvedorDoRelatorio({
    id: 'elotech',
    nome: 'Elotech 2.03',
    fonteLida,
    prefixoDestino,
    campos: [
      {
        nomeCampo: 'Discriminacao',
        caminhoInvoicy: 'Envio/RPS/Servico/Discriminacao',
        campoVersao,
        decisaoDeExemplo: escolherExemplo({
          destinos: [destinos[0]],
          fonteLida,
          campoVersao,
          prefixoDestino,
        }),
      },
      {
        nomeCampo: 'Homologa',
        caminhoInvoicy: 'Envio/Homologa',
        campoVersao: { ...campoVersao, ordem: 2 },
        decisaoDeExemplo: escolherExemplo({
          destinos: [destinos[1]],
          fonteLida,
          campoVersao,
          prefixoDestino,
        }),
      },
    ],
  })

  assert.equal(provedor.aceitos[0].tag, 'Servico/Discriminacao')
  assert.equal(
    provedor.semExemplo[0].tag,
    'EnviarLoteRpsSincronoEnvio/IdentificacaoRequerente/Homologa',
  )
})

test('campo montado sem fonte de exemplo interrompe o relatório', () => {
  const fonteLida = lerFonte(XML_DE_TRES_CAMPOS)
  const campoSemDecisao = {
    nomeCampo: 'Serie',
    caminhoInvoicy: 'Envio/RPS/Serie',
    campoVersao: { ordem: 1 },
  }

  assert.throws(
    () => provedorDeTeste([campoSemDecisao], fonteLida),
    /O campo Serie não traz decisão de exemplo/,
  )
})

const XML_COM_IDENTIFICACAO =
  '<DPS><infDPS><prest><CNPJ>12345678000199</CNPJ></prest>' +
  '<toma><xNome>ACME LTDA</xNome></toma>' +
  '<serv><cServ><cNBS>115021000</cNBS></cServ></serv></infDPS></DPS>'

test('o relatório nunca contém o valor de um dado de identificação', () => {
  const fonteLida = lerFonte(XML_COM_IDENTIFICACAO)
  const campos = [
    campoDaCarga({
      nomeCampo: 'CNPJ_prest',
      caminhoInvoicy: 'Envio/RPS/Prestador/CNPJ',
      caminho: 'DPS/infDPS/prest/CNPJ',
      ordem: 1,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'TomaRazaoSocial',
      caminhoInvoicy: 'Envio/RPS/Tomador/RazaoSocial',
      caminho: 'DPS/infDPS/toma/xNome',
      ordem: 2,
      fonteLida,
    }),
    campoDaCarga({
      nomeCampo: 'CodigoNBS',
      caminhoInvoicy: 'Envio/RPS/Servico/CodigoNBS',
      caminho: 'DPS/infDPS/serv/cServ/cNBS',
      ordem: 3,
      fonteLida,
    }),
  ]

  for (const completo of [false, true]) {
    const texto = formatarRelatorio(relatorioDe([provedorDeTeste(campos, fonteLida)]), { completo })

    assert.match(texto, /CodigoNBS · DPS\/infDPS\/serv\/cServ\/cNBS · 115021000/)
    assert.match(texto, /CNPJ_prest · DPS\/infDPS\/prest\/CNPJ$/m)
    assert.match(texto, /TomaRazaoSocial · DPS\/infDPS\/toma\/xNome$/m)
    assert.equal(texto.includes('12345678000199'), false)
    assert.equal(texto.includes('ACME'), false)
  }
})

test('o detalhe de identificação não sai, nem sozinho nem repetido no grupo', () => {
  const identificacoes = [
    {
      campo: 'CNPJ_prest',
      tag: 'DPS/infDPS/prest/CNPJ',
      motivo: 'IDENTIFICACAO',
      detalhe: 'valor "12345678000199"',
    },
    {
      campo: 'TomaRazaoSocial',
      tag: 'DPS/infDPS/toma/xNome',
      motivo: 'IDENTIFICACAO',
      detalhe: 'valor "12345678000199"',
    },
  ]

  for (const semExemplo of [identificacoes, identificacoes.slice(0, 1)]) {
    const texto = formatarRelatorio(relatorioDe([provedorMontado({ semExemplo })]))

    assert.match(texto, /^ {4}CNPJ_prest · DPS\/infDPS\/prest\/CNPJ$/m)
    assert.equal(texto.includes('12345678000199'), false)
  }
})

test('cada motivo aparece com o texto exato e a contagem certa', () => {
  const contagens = [
    ['IDENTIFICACAO', 3],
    ['MARCADOR_TESTE', 2],
    ['MARCADOR_LETRA_REPETIDA', 1],
    ['DESCARTE_EDITORIAL', 2],
    ['AMBIGUO', 1],
    ['DESTINOS_DIVERGEM', 3],
    ['FORA_DO_TIPO', 1],
    ['FORA_DO_DOMINIO', 2],
    ['ACIMA_DO_TETO', 1],
    ['FONTE_COM_VALOR_VAZIO', 2],
    ['ALTERNATIVA', 1],
    ['ATRIBUTO', 3],
    ['GRUPO', 2],
    ['ENVELOPE', 1],
    ['NAO_ESTA_NA_FONTE', 4],
  ]
  const semExemplo = contagens.flatMap(([motivo, quantidade]) => descartes(motivo, quantidade))

  const texto = formatarRelatorio(relatorioDe([provedorMontado({ semExemplo })]))

  assert.deepEqual(trechoDaSecao(texto, 'sem exemplo, por motivo:'), [
    '  dado de identificação, omitido (U1): 3',
    '  valor de teste (U2): 2',
    '  valor de preenchimento com letra repetida (U2): 1',
    '  valor descartado por decisão editorial: 2',
    '  a fonte traz mais de um valor diferente nesse caminho: 1',
    '  os destinos do campo trazem valores diferentes: 3',
    '  o valor não é do tipo do campo: 1',
    '  o valor não está entre os valores aceitos: 2',
    '  o valor passa de 500 caracteres: 1',
    '  a fonte traz a tag sem valor: 2',
    '  a tag recebe um campo OU outro (ex.: CPF ou CNPJ): 1',
    '  a tag é atributo XML: 3',
    '  a tag é um grupo (tem filhos): 2',
    '  a tag fica no envelope do lote, fora do trecho que a fonte exemplifica: 1',
    '  o caminho não aparece na fonte: 4',
  ])
})

test('a ordem dos motivos cobre todos os motivos conhecidos, sem repetir', () => {
  assert.deepEqual([...ORDEM_DOS_MOTIVOS].sort(), Object.keys(MOTIVOS).sort())
})

test('o detalhamento agrupa por motivo, na ordem de leitura, com o detalhe de cada campo', () => {
  const semExemplo = [
    { campo: 'Cidade', tag: 'Raiz/Cidade', motivo: 'GRUPO', detalhe: null },
    {
      campo: 'Descricao',
      tag: 'Raiz/Descricao',
      motivo: 'MARCADOR_TESTE',
      detalhe: 'valor "teste item 1"',
    },
    { campo: 'Documento', tag: 'Raiz/Documento', motivo: 'IDENTIFICACAO', detalhe: null },
    {
      campo: 'Observacao',
      tag: 'Raiz/Observacao',
      motivo: 'MARCADOR_TESTE',
      detalhe: 'valor "Teste de emissao"',
    },
    { campo: 'Telefone', tag: 'Raiz/Telefone', motivo: 'IDENTIFICACAO', detalhe: null },
  ]

  const texto = formatarRelatorio(relatorioDe([provedorMontado({ semExemplo })]))

  assert.deepEqual(
    trechoDaSecao(
      texto,
      'descartes que merecem leitura (todos, menos "o caminho não aparece na fonte"):',
    ),
    [
      '  dado de identificação, omitido (U1) (2 campos):',
      '    Documento · Raiz/Documento',
      '    Telefone · Raiz/Telefone',
      '  valor de teste (U2) (2 campos):',
      '    Descricao · Raiz/Descricao · valor "teste item 1"',
      '    Observacao · Raiz/Observacao · valor "Teste de emissao"',
      '  a tag é um grupo (tem filhos) (1 campo):',
      '    Cidade · Raiz/Cidade',
    ],
  )
})

test('o detalhe que todos os campos do motivo repetem sai uma vez, abaixo do título', () => {
  const motivoEditorial = 'Valores de preenchimento de teste do XML: CST 123 e cClassTrib 745896.'
  const semExemplo = ['Cst', 'cClassTrib'].map((nome) => ({
    campo: nome,
    tag: `Raiz/${nome}`,
    motivo: 'DESCARTE_EDITORIAL',
    detalhe: motivoEditorial,
  }))

  const texto = formatarRelatorio(relatorioDe([provedorMontado({ semExemplo })]))

  assert.deepEqual(
    trechoDaSecao(
      texto,
      'descartes que merecem leitura (todos, menos "o caminho não aparece na fonte"):',
    ),
    [
      '  valor descartado por decisão editorial (2 campos):',
      `    Em todos: ${motivoEditorial}`,
      '    Cst · Raiz/Cst',
      '    cClassTrib · Raiz/cClassTrib',
    ],
  )
})

const CAMPO_FORA_DA_FONTE = {
  campo: 'Telefone do contato',
  tag: 'DPS/infDPS/toma/fone',
  motivo: 'NAO_ESTA_NA_FONTE',
  detalhe: 'a fonte traz "DPS/infDPS/toma/Fone" (grafia diferente)',
}

const OUTRO_CAMPO_FORA_DA_FONTE = {
  campo: 'Email do contato',
  tag: 'DPS/infDPS/toma/email',
  motivo: 'NAO_ESTA_NA_FONTE',
  detalhe: null,
}

test('--completo acrescenta os campos que não aparecem na fonte; sem ele, só a contagem', () => {
  const relatorio = relatorioDe([
    provedorMontado({
      semExemplo: [...descartes('GRUPO', 1), CAMPO_FORA_DA_FONTE, OUTRO_CAMPO_FORA_DA_FONTE],
    }),
  ])

  const resumido = formatarRelatorio(relatorio)
  const completo = formatarRelatorio(relatorio, { completo: true })

  assert.match(resumido, /^ {2}o caminho não aparece na fonte: 2$/m)
  assert.equal(resumido.includes('Telefone do contato'), false)
  assert.equal(resumido.includes('Email do contato'), false)
  assert.match(completo, /^ {2}o caminho não aparece na fonte: 2$/m)
  assert.match(completo, /^ {2}o caminho não aparece na fonte \(2 campos\):$/m)
  assert.match(
    completo,
    /^ {4}Telefone do contato · DPS\/infDPS\/toma\/fone · a fonte traz "DPS\/infDPS\/toma\/Fone" \(grafia diferente\)$/m,
  )
  assert.match(completo, /^ {4}Email do contato · DPS\/infDPS\/toma\/email$/m)
})

test('o bloco do provedor tem o formato combinado', () => {
  const provedor = {
    ...provedorMontado({
      aceitos: [{ campo: 'Serie', tag: 'DPS/infDPS/serie', valor: 'Serie principal', ordem: 1 }],
      semExemplo: [
        {
          campo: 'CNPJ_prest',
          tag: 'DPS/infDPS/prest/CNPJ',
          motivo: 'IDENTIFICACAO',
          detalhe: null,
        },
      ],
    }),
    foraDoEnvelope: 7,
    arquivos: 1,
  }

  const texto = formatarRelatorio(relatorioDe([provedor], ['giss']))

  assert.deepEqual(texto.split('\n'), [
    '== Provedor Montado 1.0 (fonte: dps-nacional) ==',
    'fonte: 1 arquivo · 90 folhas lidas (48 caminhos distintos) · 7 fora do envelope',
    'campos: 2 · com exemplo: 1 · sem exemplo: 1',
    '',
    'exemplos atribuídos (ordem do campo):',
    '  Serie · DPS/infDPS/serie · Serie principal',
    '',
    'sem exemplo, por motivo:',
    '  dado de identificação, omitido (U1): 1',
    '',
    'descartes que merecem leitura (todos, menos "o caminho não aparece na fonte"):',
    '  dado de identificação, omitido (U1) (1 campo):',
    '    CNPJ_prest · DPS/infDPS/prest/CNPJ',
    '',
    'não gerenciados (sem fonte de exemplo): giss',
    'CONTAGEM_APROVADA_M2 provedor-montado=1',
  ])
})

test('o texto termina com a contagem por provedor e a lista de não gerenciados', () => {
  const comAceitos = (id, quantidade) => ({
    ...provedorMontado({
      aceitos: Array.from({ length: quantidade }, (_, indice) => ({
        campo: `Campo ${indice + 1}`,
        tag: `Raiz/Campo${indice + 1}`,
        valor: `Valor ${indice + 1}`,
        ordem: indice + 1,
      })),
    }),
    id,
  })
  const relatorio = relatorioDe(
    [comAceitos('emissor-nacional', 3), comAceitos('e-l', 2), comAceitos('elotech', 4)],
    ['giss', 'ipm', 'issnet'],
  )

  const linhas = formatarRelatorio(relatorio).split('\n')

  assert.deepEqual(linhas.slice(-2), [
    'não gerenciados (sem fonte de exemplo): giss, ipm, issnet',
    'CONTAGEM_APROVADA_M2 emissor-nacional=3 e-l=2 elotech=4',
  ])
  assert.deepEqual(contagemDeExemplos(relatorio), { 'emissor-nacional': 3, 'e-l': 2, elotech: 4 })
})

test('provedor sem exemplo e sem descarte mostra (nenhum) em vez de seção vazia', () => {
  const texto = formatarRelatorio(relatorioDe([provedorMontado({})]))

  assert.deepEqual(trechoDaSecao(texto, 'exemplos atribuídos (ordem do campo):'), ['  (nenhum)'])
  assert.deepEqual(trechoDaSecao(texto, 'sem exemplo, por motivo:'), ['  (nenhum)'])
  assert.match(texto, /^não gerenciados \(sem fonte de exemplo\): \(nenhum\)$/m)
})

test('argumentos: --completo, --saida com arquivo, e recusa o que não conhece', () => {
  assert.deepEqual(lerArgumentos([]), { completo: false, saida: null })
  assert.deepEqual(lerArgumentos(['--completo', '--saida', 'relatorio.txt']), {
    completo: true,
    saida: 'relatorio.txt',
  })
  assert.throws(() => lerArgumentos(['--saida']), ErroDeUso)
  assert.throws(() => lerArgumentos(['--saida', '--completo']), ErroDeUso)
  assert.throws(() => lerArgumentos(['--tudo']), /Argumento desconhecido: --tudo/)
})

test('a execução imprime o texto e só grava arquivo com --saida, em UTF-8', async () => {
  const relatorio = relatorioDe([provedorMontado({})], ['giss'])
  const impressos = []
  const gravados = []
  const executar = (argumentos) =>
    executarRelatorio({
      argumentos,
      montar: async () => relatorio,
      imprimir: (texto) => impressos.push(texto),
      gravar: async (arquivo, texto, codificacao) => gravados.push([arquivo, texto, codificacao]),
    })

  const texto = await executar([])
  assert.equal(impressos.length, 1)
  assert.equal(impressos[0], texto)
  assert.deepEqual(gravados, [])

  await executar(['--saida', 'relatorio.txt'])
  assert.deepEqual(gravados, [['relatorio.txt', `${texto}\n`, 'utf8']])
})

const relatorioReal = await montarRelatorioDeExemplos()

// Contagem aprovada pelo usuário sobre o relatório real: mudar uma regra de exemplo derruba este caso.
test('a contagem de exemplos é a aprovada', () => {
  assert.deepEqual(contagemDeExemplos(relatorioReal), {
    'emissor-nacional': 35,
    'e-l': 36,
    elotech: 38,
  })
})

test('o relatório real cobre os três provedores com fonte e aponta os que não têm', () => {
  assert.deepEqual(
    relatorioReal.provedores.map(({ id, nome, total }) => [id, nome, total]),
    [
      ['emissor-nacional', 'Emissor Nacional 0.20', 195],
      ['e-l', 'E&L 1.01', 191],
      ['elotech', 'Elotech 2.03', 113],
    ],
  )
  assert.deepEqual(relatorioReal.naoGerenciados, [
    'giss',
    'ipm',
    'issnet',
    'betha',
    'coplan',
    'dsfnet',
    'dueto',
    'fiorilli',
    'gespam',
    'joao-pessoa',
    'rlz',
    'sigcorp',
    'smarapdisil',
  ])
})

test('no relatório real, cada campo ou recebe exemplo ou aparece com um motivo', () => {
  for (const provedor of relatorioReal.provedores) {
    assert.equal(provedor.aceitos.length + provedor.semExemplo.length, provedor.total, provedor.id)
    for (const descarte of provedor.semExemplo) {
      assert.equal(descarte.motivo in MOTIVOS, true, `${provedor.id}: ${descarte.campo}`)
    }
  }
})

test('no relatório real, os exemplos conhecidos aparecem com o valor da fonte', () => {
  const aceitosDe = (id) => relatorioReal.provedores.find((provedor) => provedor.id === id).aceitos
  const valorDaTag = (id, tag) => aceitosDe(id).find((aceito) => aceito.tag === tag)?.valor

  assert.equal(valorDaTag('emissor-nacional', 'DPS/infDPS/valores/vServPrest/vServ'), '29155.75')
  assert.equal(
    valorDaTag('elotech', 'InfDeclaracaoPrestacaoServico/Rps/IdentificacaoRps/Numero'),
    '579',
  )
})

test('no relatório real, a identificação sai só com o campo e a tag', () => {
  const texto = formatarRelatorio(relatorioReal, { completo: true })

  for (const provedor of relatorioReal.provedores) {
    const identificacoes = provedor.semExemplo.filter(({ motivo }) => motivo === 'IDENTIFICACAO')
    assert.equal(identificacoes.length >= 8, true, provedor.id)
    for (const { campo, tag } of identificacoes) {
      assert.match(texto, new RegExp(`^ {4}${campo} · ${tag.replace(/\//g, '\\/')}$`, 'm'))
    }
  }
})

test('o texto do relatório real termina com a contagem de cada provedor', () => {
  const texto = formatarRelatorio(relatorioReal)
  const [contagem] = texto.split('\n').slice(-1)
  const { 'emissor-nacional': nacional, 'e-l': eL, elotech } = contagemDeExemplos(relatorioReal)

  assert.equal(
    contagem,
    `CONTAGEM_APROVADA_M2 emissor-nacional=${nacional} e-l=${eL} elotech=${elotech}`,
  )
  assert.equal(/undefined|\[object|NaN/.test(texto), false)
})
