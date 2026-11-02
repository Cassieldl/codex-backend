/**
 * Leitura do material da pasta Documentação (FAQ NFS-e): saneamento na cópia para o repositório,
 * marca de pendência, tabelas Markdown e texto simples. Só funções puras, mais a cópia.
 *
 * Na Documentação, o ponto em aberto é marcado com o emoji de alerta. Na cópia ele vira o texto
 * MARCA_PENDENTE, porque emoji não entra no repositório e a marca precisa sobreviver para a carga
 * deixar o trecho de fora.
 */

import { readFile, writeFile } from 'node:fs/promises'
import { join } from 'node:path'

export const MARCA_PENDENTE = '[pendente]'

// Por código, e não o caractere: nenhum arquivo do repositório leva emoji nem travessão.
const SINAL_DE_ALERTA = String.fromCodePoint(0x26a0)
const ALERTA = new RegExp(`${SINAL_DE_ALERTA}${String.fromCodePoint(0xfe0f)}?`, 'g')
const EMOJI = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}\u{2B50}\u{2B55}]/gu
const TRAVESSAO = String.fromCodePoint(0x2014)
const MARCAS_DE_FONTE = /\s*(\*\*)?\[(obs|portal antigo|portal|validado)\](\*\*)?/g

/**
 * Troca o alerta pela marca textual e o travessão por dois-pontos (no título) ou hífen. Qualquer
 * outro emoji interrompe: cada um precisa de uma decisão antes de entrar no repositório.
 */
export function sanitizarDaDocumentacao(texto) {
  const comMarca = texto.replace(ALERTA, MARCA_PENDENTE)
  const restantes = comMarca.match(EMOJI)
  if (restantes) throw new Error(`Emoji sem tratamento: ${[...new Set(restantes)].join(' ')}`)

  return comMarca
    .replace(new RegExp(`^(#{1,6} [^\\n]*?) ${TRAVESSAO} `, 'gm'), '$1: ')
    .replace(new RegExp(` ${TRAVESSAO} `, 'g'), ' - ')
    .replaceAll(TRAVESSAO, '-')
}

export function temMarcaDePendencia(texto) {
  return texto.includes(MARCA_PENDENTE) || texto.includes(SINAL_DE_ALERTA)
}

/** Remove as marcas de fonte entre colchetes ([obs], [portal], [portal antigo], [validado]). */
export function removerMarcasDeFonte(texto) {
  return texto.replace(MARCAS_DE_FONTE, '')
}

/** Comparação tolerante a acento, caixa e pontuação: "RPS já informado." e "RPS ja informado" são a mesma mensagem. */
export function normalizarComparacao(texto) {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9%$]+/g, ' ')
    .trim()
}

/**
 * Markdown de uma célula para texto simples, porque causa, solução e mensagem do erro são
 * exibidas sem interpretar Markdown. Link externo guarda o endereço entre parênteses; link
 * interno fica só com o texto.
 */
export function textoSimples(markdown, { manterEnderecos = true } = {}) {
  return removerMarcasDeFonte(markdown)
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
    .replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, (_, texto, url) =>
      manterEnderecos && texto !== url ? `${texto} (${url})` : texto,
    )
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/<(https?:\/\/[^>\s]+)>/g, '$1')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/(^|[^*\w])\*([^*\n]+)\*(?!\w)/g, '$1$2')
    .replace(/`([^`]*)`/g, '$1')
    .replace(/\\([<>"|*_`\\[\]])/g, '$1')
    .replace(/\s+/g, ' ')
    .trim()
}

/** Identificadores entre crases (`regApTribSN`), candidatos a nome de campo. */
export function identificadoresEntreCrases(markdown) {
  return [...markdown.matchAll(/`([A-Za-z_][A-Za-z0-9_]*)`/g)].map((achado) => achado[1])
}

function ehLinhaDeTabela(linha) {
  return /^\s*\|.*\|\s*$/.test(linha)
}

function ehSeparadorDeTabela(linha) {
  return /^\s*\|(\s*:?-{3,}:?\s*\|)+\s*$/.test(linha)
}

function celulasDe(linha) {
  return linha
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split(/(?<!\\)\|/)
    .map((celula) => celula.trim().replace(/\\\|/g, '|'))
}

/**
 * Tabelas do Markdown, cada uma com o título da seção onde está. A seção é pendente quando um
 * texto dela, fora das tabelas, traz a marca: o aviso no topo de uma lista vale para a lista
 * inteira. A linha de tabela com a marca é pendente sozinha.
 */
export function lerTabelas(markdown) {
  const linhas = markdown.split(/\r?\n/)
  const tabelas = []
  let secao = { titulo: '', pendente: false }

  for (let indice = 0; indice < linhas.length; indice++) {
    const linha = linhas[indice]
    const titulo = /^#{1,6}\s+(.*)$/.exec(linha)
    if (titulo) {
      secao = { titulo: titulo[1].trim(), pendente: false }
      continue
    }

    const iniciaTabela = ehLinhaDeTabela(linha) && ehSeparadorDeTabela(linhas[indice + 1] ?? '')
    if (!iniciaTabela) {
      if (temMarcaDePendencia(linha)) secao.pendente = true
      continue
    }

    const tabela = { secao, cabecalho: celulasDe(linha), linhas: [] }
    for (indice += 2; indice < linhas.length && ehLinhaDeTabela(linhas[indice]); indice++) {
      tabela.linhas.push({
        numero: indice + 1,
        texto: linhas[indice],
        celulas: celulasDe(linhas[indice]),
      })
    }
    indice--
    tabelas.push(tabela)
  }

  return tabelas.map(({ secao: daSecao, ...resto }) => ({
    secao: daSecao.titulo,
    secaoPendente: daSecao.pendente,
    ...resto,
  }))
}

/** Copia arquivos da Documentação para uma pasta do repositório, já saneados. */
export async function copiarDaDocumentacao(
  pastaDocumentacao,
  arquivos,
  pastaDestino,
  corrigir = (_destino, texto) => texto,
) {
  for (const { origem, destino } of arquivos) {
    const texto = await readFile(join(pastaDocumentacao, origem), 'utf8')
    await writeFile(join(pastaDestino, destino), corrigir(destino, sanitizarDaDocumentacao(texto)))
    console.log(`copiado: ${origem} -> ${destino}`)
  }
}
