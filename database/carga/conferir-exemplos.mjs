/**
 * Confere, pela página pública da versão, quantos campos estão com exemplo depois da carga. Só faz
 * GET público, sem token: a página só responde versão publicada.
 *
 * Uso:
 *   node database/carga/conferir-exemplos.mjs <provedorSlug> <versaoSlug> <esperado>
 *
 * Imprime `<n> campos com exemplo de <total>` e `<campo> · <exemplo>` por campo. Sai com código 1
 * quando `<n>` difere de `<esperado>` ou quando a página não pode ser lida.
 */

import { pathToFileURL } from 'node:url'

const URL_BASE_DA_API = process.env.CODEX_API_URL ?? 'http://localhost:5000/api/v1'

const USO = 'Uso: node database/carga/conferir-exemplos.mjs <provedorSlug> <versaoSlug> <esperado>'

export class ErroDeConferencia extends Error {
  name = 'ErroDeConferencia'
}

/**
 * Campos com exemplo da página pública (`{ data: { camposPorGrupo: [{ campos: [...] }] } }`). A
 * página crua, sem o envelope `data`, é recusada: lê-la como se fosse a resposta contaria zero.
 */
export function contarExemplos(resposta) {
  const pagina = resposta?.data
  if (!pagina || typeof pagina !== 'object') {
    throw new ErroDeConferencia(
      'A resposta não traz o envelope "data" da página pública da versão.',
    )
  }
  if (!Array.isArray(pagina.camposPorGrupo)) {
    throw new ErroDeConferencia('A resposta traz "data" sem a lista "camposPorGrupo".')
  }

  const campos = pagina.camposPorGrupo.flatMap((grupo) => grupo.campos)
  const comExemplo = campos
    .filter((campo) => typeof campo.exemplo === 'string' && campo.exemplo !== '')
    .map((campo) => ({ nome: campo.nome, exemplo: campo.exemplo }))
  return { total: campos.length, comExemplo }
}

function mensagemDoErro(status, provedorSlug, versaoSlug) {
  const versao = `${provedorSlug}/${versaoSlug}`
  if (status === 410) {
    return (
      `A versão ${versao} está despublicada (410): a página pública só responde versão ` +
      'publicada. Publique a versão antes de conferir.'
    )
  }
  if (status === 404) {
    return `Provedor ou versão não encontrados (404): confira os slugs "${provedorSlug}" e "${versaoSlug}".`
  }
  if (status === 429) return 'A API limitou as requisições (429). Espere um pouco e tente de novo.'
  return `A API respondeu ${status} para a versão ${versao}.`
}

/** Lê a página pública da versão. Qualquer resposta que não seja 200 vira `ErroDeConferencia`. */
export async function lerPaginaPublicaDaVersao({
  provedorSlug,
  versaoSlug,
  enviar = fetch,
  urlBase = URL_BASE_DA_API,
}) {
  const rota = `${urlBase}/provedores/${encodeURIComponent(provedorSlug)}/versoes/${encodeURIComponent(versaoSlug)}`

  let resposta
  try {
    resposta = await enviar(rota, { method: 'GET' })
  } catch (erro) {
    throw new ErroDeConferencia(`Não foi possível falar com a API em ${urlBase}: ${erro.message}`, {
      cause: erro,
    })
  }
  if (!resposta.ok) {
    throw new ErroDeConferencia(mensagemDoErro(resposta.status, provedorSlug, versaoSlug))
  }
  return resposta.json()
}

/** Linhas da conferência e se a quantidade de campos com exemplo é a esperada. */
export function conferirContagem(contagem, esperado) {
  const linhas = [
    `${contagem.comExemplo.length} campos com exemplo de ${contagem.total}`,
    ...contagem.comExemplo.map(({ nome, exemplo }) => `${nome} · ${exemplo}`),
  ]
  return { linhas, aprovado: contagem.comExemplo.length === esperado }
}

export function lerArgumentos(argumentos) {
  const [provedorSlug, versaoSlug, esperadoTexto, ...sobra] = argumentos
  const esperado = Number(esperadoTexto)
  if (!provedorSlug || !versaoSlug || sobra.length || !/^\d+$/.test(esperadoTexto ?? '')) {
    throw new ErroDeConferencia(USO)
  }
  return { provedorSlug, versaoSlug, esperado }
}

/** Devolve o código de saída: 0 quando a contagem é a esperada, 1 em qualquer outro caso. */
export async function executarConferencia({
  argumentos,
  enviar = fetch,
  urlBase = URL_BASE_DA_API,
  imprimir = console.log,
  imprimirErro = console.error,
}) {
  try {
    const { provedorSlug, versaoSlug, esperado } = lerArgumentos(argumentos)
    const pagina = await lerPaginaPublicaDaVersao({ provedorSlug, versaoSlug, enviar, urlBase })
    const { linhas, aprovado } = conferirContagem(contarExemplos(pagina), esperado)
    for (const linha of linhas) imprimir(linha)
    if (!aprovado) {
      imprimirErro(`Esperados ${esperado} campos com exemplo em ${provedorSlug}/${versaoSlug}.`)
    }
    return aprovado ? 0 : 1
  } catch (erro) {
    if (!(erro instanceof ErroDeConferencia)) throw erro
    imprimirErro(erro.message)
    return 1
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  process.exitCode = await executarConferencia({ argumentos: process.argv.slice(2) })
}
