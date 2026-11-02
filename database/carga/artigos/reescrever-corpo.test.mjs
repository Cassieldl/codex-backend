// Rodar com: node --test database/carga/artigos/reescrever-corpo.test.mjs
import { strict as assert } from 'node:assert'
import { test } from 'node:test'

import { MARCA_PENDENTE } from '../erros/documentacao.mjs'
import { prepararArtigo, problemasDoCorpo } from './reescrever-corpo.mjs'

const ARQUIVO = '04-base-conhecimento/artigos/origem.md'

const CONTEXTO = {
  arquivo: ARQUIVO,
  slug: 'origem-da-rejeicao',
  artigos: new Map([
    [
      '04-base-conhecimento/artigos/emissao-sequencial-rps.md',
      { slug: 'emissao-sequencial-de-rps', titulos: ['Particularidades por padrão'] },
    ],
  ]),
  provedorPorRejeicao: new Map([['03-rejeicoes/giss.md', 'giss']]),
  provedorPorAncoraDeObservacao: new Map([['ipm', 'ipm']]),
  rotulos: { 'invoicy-inv03xx.md': 'mensagens do InvoiCy (inv03xx)' },
}

function preparar(corpo) {
  return prepararArtigo(`# Origem da rejeição\n\n${corpo}\n`, CONTEXTO)
}

test('link para artigo carregado e para provedor do Codex vira a rota pública', () => {
  const resultado = preparar(
    [
      'Ver [Emissão sequencial](emissao-sequencial-rps.md#particularidades-por-padrão).',
      'Erros do [GISS](../../03-rejeicoes/giss.md) e [observações do IPM](../../02-observacoes/observacoes-provedores.md#ipm).',
    ].join('\n'),
  )

  assert.equal(resultado.titulo, 'Origem da rejeição')
  assert.equal(
    resultado.corpo,
    [
      'Ver [Emissão sequencial](/artigos/emissao-sequencial-de-rps#secao-particularidades-por-padrao).',
      'Erros do [GISS](/provedores/giss) e [observações do IPM](/provedores/ipm).',
      '',
    ].join('\n'),
  )
})

test('link sem destino no Codex fica só com o texto, e nenhum .md ou caminho da Documentação sobra', () => {
  const resultado = preparar(
    [
      'Veja o [layout De/Para](../../01-layouts/layouts-provedores.md) e [invoicy-inv03xx.md](../../03-rejeicoes/invoicy-inv03xx.md).',
      'Lista oficial: [gov.br](https://www.gov.br/nfse). Ver [artigo](retencoes-federais.md).',
      'Na planilha (`_origem/Padrões/Layout.xlsx`) há mais. **[obs]** Confirmado [portal antigo].',
    ].join('\n'),
  )

  assert.equal(
    resultado.corpo,
    [
      'Veja o layout De/Para e mensagens do InvoiCy (inv03xx).',
      'Lista oficial: [gov.br](https://www.gov.br/nfse).',
      'Na planilha há mais. Confirmado.',
      '',
    ].join('\n'),
  )
  assert.deepEqual(problemasDoCorpo(resultado.corpo), [])
})

test('imagem sai do corpo e entra na lista; célula que só tinha imagem ganha aviso', () => {
  const resultado = preparar(
    [
      '1. Abra a tela.',
      '',
      '   ![Tela](../../assets/imagens/tela.png)',
      '',
      '2. Salve.',
      '',
      '| Padrão | Tabela |',
      '| :--- | :--- |',
      '| CITTA | [imagem](../../assets/imagens/citta.png) |',
      '',
      'Prints: [trocar](../../assets/a.png) · [herdar](../../assets/b.png).',
    ].join('\n'),
  )

  assert.equal(
    resultado.corpo,
    [
      '1. Abra a tela.',
      '',
      '2. Salve.',
      '',
      '| Padrão | Tabela |',
      '| :--- | :--- |',
      '| CITTA | Só em imagem, não incluída. |',
      '',
    ].join('\n'),
  )
  assert.equal(resultado.imagensRemovidas.length, 4)
})

test('corpo com link para .md ou marca de pendência é recusado', () => {
  assert.deepEqual(problemasDoCorpo(`Ver [x](../outro.md). ${MARCA_PENDENTE}`), [
    'link para arquivo .md',
    'marca de pendência',
  ])
  assert.throws(
    () => preparar(`Texto ${MARCA_PENDENTE} Pendente: falta confirmar.`),
    /ainda tem: marca de pendência/,
  )
})
