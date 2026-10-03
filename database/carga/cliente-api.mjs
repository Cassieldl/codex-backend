/**
 * Cliente mínimo da API interna para os scripts de carga. Toda escrita passa pela API — nunca por
 * INSERT direto — para que slug, publicação em cascata e índice de busca sigam as regras do produto.
 */

import { criarProvedorDeToken } from './autenticacao-dev.mjs'

const API = process.env.CODEX_API_URL ?? 'http://localhost:5000/api/v1'

export async function criarClienteApi() {
  const obterToken = await criarProvedorDeToken()

  async function chamar(metodo, rota, corpo, etag) {
    const cabecalhos = {
      Authorization: `Bearer ${await obterToken()}`,
      'Content-Type': 'application/json',
    }
    if (etag) cabecalhos['If-Match'] = etag

    const resposta = await fetch(`${API}${rota}`, {
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

  return { chamar, obterEtag, publicar, despublicar, remover, listarTudo }
}
