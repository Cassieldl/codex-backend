// Rodar com: node --test database/carga/emissor-nacional/transformar.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import {
  LIMITE_TITULO_DESEJADO,
  caminhoParaLeitor,
  codigosSomenteRtc,
  derivarTitulo,
  montarErros,
  origemDoCodigo,
  rejeicoesSomenteAnexoI,
} from './transformar.mjs'

function rejeicao(sobrescrita) {
  return {
    codigo: 'E0100',
    contexto: 'DPS/NFS-e',
    xpath: 'NFSe/infNFSe/DPS/infDPS/prest/CNPJ',
    mensagem: 'CNPJ do prestador inválido.',
    regra: 'O CNPJ deve ser válido.',
    codigo_provisorio: false,
    ...sobrescrita,
  }
}

test('mensagem curta vira o título sem alteração', () => {
  assert.equal(derivarTitulo('CNPJ do prestador inválido.'), 'CNPJ do prestador inválido.')
})

test('mensagem longa usa a primeira frase quando ela cabe', () => {
  const mensagem = `A informação é exclusiva da Sefin Nacional. ${'Detalhe extenso da regra. '.repeat(10)}`
  assert.equal(derivarTitulo(mensagem), 'A informação é exclusiva da Sefin Nacional.')
})

test('mensagem longa sem frase curta corta na última palavra inteira, com reticências', () => {
  const titulo = derivarTitulo(`${'palavra '.repeat(40)}fim.`)
  assert.ok(titulo.length <= LIMITE_TITULO_DESEJADO)
  assert.ok(titulo.endsWith('palavra…'))
})

test('quebras de linha da planilha não chegam ao título', () => {
  assert.equal(derivarTitulo('Linha um\n\nlinha dois.'), 'Linha um linha dois.')
})

test('só falha de codificação e de esquema são origem Schema', () => {
  assert.equal(origemDoCodigo('E1235'), 'Schema')
  assert.equal(origemDoCodigo('E1229'), 'Schema')
  assert.equal(origemDoCodigo('E1200'), 'Prefeitura')
})

test('caminho dentro da NFS-e vira o caminho da DPS enviada; os demais ficam como estão', () => {
  assert.equal(caminhoParaLeitor('NFSe/infNFSe/DPS/infDPS/prest/CNPJ'), 'DPS/infDPS/prest/CNPJ')
  assert.equal(caminhoParaLeitor('NFSe/infNFSe/valores/vBC'), 'NFSe/infNFSe/valores/vBC')
  assert.equal(caminhoParaLeitor('evento/versao'), 'evento/versao')
  assert.equal(caminhoParaLeitor(null), null)
})

test('código provisório e código fora do padrão ficam de fora', () => {
  const erros = montarErros([
    rejeicao({ codigo: 'E0100' }),
    // Formato de código definitivo, mas marcado como provisório: só o flag o exclui.
    rejeicao({ codigo: 'E0200', codigo_provisorio: true }),
    rejeicao({ codigo: 'EXXX', codigo_provisorio: false }),
  ])
  assert.deepEqual(
    erros.map((erro) => erro.codigo),
    ['E0100'],
  )
})

test('código repetido vira um erro só, com uma variante por mensagem distinta', () => {
  const erros = montarErros([
    rejeicao({ codigo: 'E1260', mensagem: 'Versão da NFS-e expirou.', xpath: 'NFSe/versao' }),
    rejeicao({
      codigo: 'E1260',
      mensagem: 'Versão do DF-e expirou.',
      xpath: 'evento/versao',
      contexto: 'Evento do emitente',
    }),
    rejeicao({ codigo: 'E1260', mensagem: 'Versão da NFS-e expirou.', xpath: 'NFSe/versao' }),
  ])

  assert.equal(erros.length, 1)
  assert.deepEqual(erros[0].variantes, ['Versão da NFS-e expirou.', 'Versão do DF-e expirou.'])
  assert.equal(erros[0].titulo, 'Versão da NFS-e expirou.')
})

test('causa provável traz a regra, o caminho da DPS e o aviso de evento', () => {
  const [erro] = montarErros([rejeicao({ contexto: 'Evento do emitente', xpath: 'evento/versao' })])
  assert.equal(
    erro.causaProvavel,
    'Regra da Sefin: O CNPJ deve ser válido.\nOnde: evento/versao\nOcorre no envio de evento, não na emissão da DPS.',
  )
})

test('rejeição sem regra nem caminho não inventa causa', () => {
  const [erro] = montarErros([rejeicao({ regra: null, xpath: null })])
  assert.equal(erro.causaProvavel, null)
})

test('variante acima do limite do formulário interrompe a carga em vez de truncar', () => {
  assert.throws(() => montarErros([rejeicao({ mensagem: 'x'.repeat(1001) })]), /Variante de E0100/)
})

test('códigos que só o RTC define ficam fora da versão do leiaute vigente', () => {
  const codigosForaDaVersao = codigosSomenteRtc({
    codigos_erro_novos_anexo_VI: ['E0678', 'EXXX', 'Exxx', 'E0690'],
  })
  assert.deepEqual(codigosForaDaVersao, ['E0678', 'E0690'])

  const erros = montarErros(
    [rejeicao({ codigo: 'E0100' }), rejeicao({ codigo: 'E0678' }), rejeicao({ codigo: 'E0690' })],
    { codigosForaDaVersao },
  )
  assert.deepEqual(
    erros.map((erro) => erro.codigo),
    ['E0100'],
  )
})

test('rejeição só do Anexo I vira erro com a mensagem e o caminho, sem inventar regra', () => {
  const rejeicoes = rejeicoesSomenteAnexoI({
    codigos_erro_somente_anexo_I: [
      {
        codigo: 'E0438',
        mensagem: 'Dedução não permitida com regime especial.',
        xpath: 'NFSe/infNFSe/DPS/infDPS/valores/vDedRed',
      },
    ],
  })
  const [erro] = montarErros(rejeicoes)

  assert.equal(erro.codigo, 'E0438')
  assert.equal(erro.origem, 'Prefeitura')
  assert.deepEqual(erro.variantes, ['Dedução não permitida com regime especial.'])
  assert.equal(erro.causaProvavel, 'Onde: DPS/infDPS/valores/vDedRed')
})
