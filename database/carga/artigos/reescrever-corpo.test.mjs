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

test('imagem de assets/imagens fica com o caminho do portal; Betha legado e o resto saem', () => {
  const resultado = preparar(
    [
      '1. Abra a tela.',
      '',
      '   ![Tela de emissão](../../assets/imagens/emissao/tela.png "Menu Emissão")',
      '',
      '![Natureza ABRASF](../../assets/imagens/betha/natureza-da-operacao-abrasf.jpg)',
      '',
      '2. Salve.',
      '',
      '| Padrão | Tabela |',
      '| :--- | :--- |',
      '| CITTA | [imagem](../../assets/imagens/citta.png) |',
      '| Betha | ![Regime](../../assets/imagens/betha/regime-especial-de-tributacao-abrasf.jpg) |',
      '',
      'Prints: [trocar](../../assets/a.png) · ![externa](https://exemplo.com/a.png).',
    ].join('\n'),
  )

  assert.equal(
    resultado.corpo,
    [
      '1. Abra a tela.',
      '',
      '   ![Tela de emissão](/imagens/emissao/tela.png "Menu Emissão")',
      '',
      '2. Salve.',
      '',
      '| Padrão | Tabela |',
      '| :--- | :--- |',
      '| CITTA | Só em imagem, não incluída. |',
      '| Betha | Só em imagem, não incluída. |',
      '',
    ].join('\n'),
  )
  assert.deepEqual(resultado.imagensMantidas, [
    { de: '../../assets/imagens/emissao/tela.png', para: '/imagens/emissao/tela.png' },
  ])
  assert.equal(resultado.imagensRemovidas.length, 5)
})

test('marcadores de bloco e fluxo passam pela carga sem mudança', () => {
  const blocos = [
    '> [!RESUMO]',
    '> O RPS é validado antes do envio.',
    '',
    '> [!DETALHES] Quando o lote volta',
    '> Confira o protocolo.',
    '',
    '```fluxo',
    'Envio: o InvoiCy monta o lote',
    'Retorno: a prefeitura responde',
    '```',
  ].join('\n')

  assert.equal(preparar(blocos).corpo, `${blocos}\n`)
})

test('corpo só aceita imagem com caminho do portal e recusa link para imagem', () => {
  assert.deepEqual(problemasDoCorpo('![Tela](/imagens/emissao/tela.png "Legenda")'), [])
  assert.deepEqual(problemasDoCorpo('![x](https://exemplo.com/a.png)'), [
    'imagem fora de /imagens/',
  ])
  assert.deepEqual(problemasDoCorpo('![x](data:image/png;base64,AAAA)'), [
    'imagem fora de /imagens/',
  ])
  assert.deepEqual(problemasDoCorpo('![x](/imagens/../segredo.png)'), ['imagem fora de /imagens/'])
  assert.deepEqual(problemasDoCorpo('Ver [print](/imagens/emissao/tela.png).'), [
    'link para imagem',
  ])
  assert.deepEqual(problemasDoCorpo('![x](../../assets/imagens/a.png)'), [
    'link para caminho da Documentação',
    'imagem fora de /imagens/',
  ])
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
