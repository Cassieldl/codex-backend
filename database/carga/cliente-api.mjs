/**
 * Cliente mínimo da API interna para os scripts de carga. Toda escrita passa pela API — nunca por
 * INSERT direto — para que slug, publicação em cascata e índice de busca sigam as regras do produto.
 *
 * Com `CODEX_CARGA_SIMULAR=1` o cliente só lê: o GET vai à API, o PUT é registrado sem ser enviado e
 * o POST e o DELETE interrompem a carga (`ErroDeSimulacao`), porque a simulação não tem como
 * continuar sem o id que a API devolveria.
 */

import { criarProvedorDeToken } from './autenticacao-dev.mjs'

const API = process.env.CODEX_API_URL ?? 'http://localhost:5000/api/v1'

export class ErroDeSimulacao extends Error {
  name = 'ErroDeSimulacao'
}

export function simulacaoLigada(ambiente = process.env) {
  return ambiente.CODEX_CARGA_SIMULAR === '1'
}

function rotaSemIdsNemQuery(rota) {
  return rota.split('?')[0].replace(/\/\d+(?=\/|$)/g, '/{id}')
}

/** Faz a chamada HTTP (`enviar`), ou só a registra quando `simular`. */
export function criarChamada({ obterToken, enviar = fetch, simular = false, urlBase = API }) {
  const leituras = []
  const escritasRegistradas = []

  function imprimirResumoDaSimulacao() {
    if (!simular) return

    const escritasPorRota = new Map()
    for (const { metodo, rota } of escritasRegistradas) {
      const chave = `${metodo} ${rotaSemIdsNemQuery(rota)}`
      escritasPorRota.set(chave, (escritasPorRota.get(chave) ?? 0) + 1)
    }

    console.log('\n== SIMULAÇÃO: nenhuma escrita foi enviada ==')
    console.log(`leituras reais (GET): ${leituras.length}`)
    console.log(`escritas que a carga faria: ${escritasRegistradas.length}`)
    for (const [chave, quantidade] of escritasPorRota) console.log(`  ${chave}: ${quantidade}`)
  }

  async function chamar(metodo, rota, corpo, etag) {
    if (simular && metodo !== 'GET') {
      escritasRegistradas.push({ metodo, rota, corpo })
      if (metodo === 'PUT') return { dados: corpo ?? null, etag: null }

      imprimirResumoDaSimulacao()
      const acao = metodo === 'POST' ? 'criaria' : 'removeria'
      throw new ErroDeSimulacao(
        `Simulação interrompida: a carga ${acao} ${rota}. ` +
          'O esperado nesta demanda é só PUT de exemplo. Nada foi gravado.',
      )
    }

    const cabecalhos = {
      Authorization: `Bearer ${await obterToken()}`,
      'Content-Type': 'application/json',
    }
    if (etag) cabecalhos['If-Match'] = etag

    if (simular) leituras.push(rota)
    const resposta = await enviar(`${urlBase}${rota}`, {
      method: metodo,
      headers: cabecalhos,
      body: corpo ? JSON.stringify(corpo) : undefined,
    })

    const texto = await resposta.text()
    if (!resposta.ok) {
      throw new Error(`${metodo} ${rota} -> ${resposta.status}: ${texto.slice(0, 400)}`)
    }

    const conteudo = texto ? JSON.parse(texto) : null
    return { dados: conteudo?.data ?? conteudo, etag: resposta.headers.get('etag') }
  }

  return { chamar, simulando: simular, imprimirResumoDaSimulacao }
}

/**
 * Roda as etapas da carga. A interrupção da simulação é desfecho esperado, não falha do script:
 * mostra a mensagem e sai com código 1, sem pilha de chamadas.
 */
export async function executarCarga(etapas) {
  try {
    await etapas()
  } catch (erro) {
    if (!(erro instanceof ErroDeSimulacao)) throw erro
    console.error(`\n${erro.message}`)
    process.exitCode = 1
  }
}

export async function criarClienteApi({ simular = simulacaoLigada() } = {}) {
  const obterToken = await criarProvedorDeToken()
  const { chamar, simulando, imprimirResumoDaSimulacao } = criarChamada({ obterToken, simular })

  /** Lê o recurso só para obter a ETag atual — escritas exigem If-Match. */
  async function obterEtag(rota) {
    return (await chamar('GET', rota)).etag
  }

  async function publicar(rotaBase, id, etag) {
    return chamar('PUT', `${rotaBase}/${id}/publicacao`, { publicado: true }, etag)
  }

  async function despublicar(rotaBase, id, etag) {
    return chamar('PUT', `${rotaBase}/${id}/publicacao`, { publicado: false }, etag)
  }

  async function remover(rotaRecurso) {
    const etag = await obterEtag(rotaRecurso)
    return chamar('DELETE', rotaRecurso, undefined, etag)
  }

  /** Percorre uma listagem paginada interna até o fim. */
  async function listarTudo(rota) {
    const itens = []
    for (let pagina = 1; ; pagina++) {
      const separador = rota.includes('?') ? '&' : '?'
      const { dados } = await chamar('GET', `${rota}${separador}pagina=${pagina}&tamanhoPagina=100`)
      const lote = dados.items ?? dados
      itens.push(...lote)
      if (!dados.totalPages || pagina >= dados.totalPages) break
    }
    return itens
  }

  return {
    chamar,
    obterEtag,
    publicar,
    despublicar,
    remover,
    listarTudo,
    simulando,
    imprimirResumoDaSimulacao,
  }
}
