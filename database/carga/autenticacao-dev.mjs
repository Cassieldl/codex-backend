/**
 * Token para scripts de carga contra a API local.
 *
 * O realm de desenvolvimento não habilita login direto por senha, então o token sai do mesmo
 * fluxo do portal: authorization code + PKCE no client público `codex-front`, preenchendo o
 * formulário de login do Keycloak. Renova pelo refresh token antes de expirar, porque uma carga
 * grande dura mais que a vida do access token.
 *
 * Fora do ambiente local, passe um token pronto em CODEX_TOKEN (sem renovação).
 */

import { createHash, randomBytes } from 'node:crypto'

const KEYCLOAK = process.env.CODEX_KEYCLOAK_URL ?? 'http://localhost:8080/realms/codex'
const CLIENT_ID = 'codex-front'
const REDIRECT_URI = 'http://localhost:5173/'
const MARGEM_RENOVACAO_SEGUNDOS = 30

function base64Url(buffer) {
  return buffer.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function juntarCookies(cookiesAtuais, resposta) {
  for (const linha of resposta.headers.getSetCookie()) {
    const [par] = linha.split(';')
    const [nome] = par.split('=')
    cookiesAtuais.set(nome, par)
  }
  return cookiesAtuais
}

function decodificarCorpoHtml(texto) {
  return texto.replace(/&amp;/g, '&')
}

async function trocarCodigoPorToken(codigo, verificador) {
  const resposta = await fetch(`${KEYCLOAK}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      client_id: CLIENT_ID,
      redirect_uri: REDIRECT_URI,
      code: codigo,
      code_verifier: verificador,
    }),
  })
  if (!resposta.ok)
    throw new Error(`Troca de código falhou: ${resposta.status} ${await resposta.text()}`)
  return resposta.json()
}

async function loginPorFormulario(usuario, senha) {
  const verificador = base64Url(randomBytes(32))
  const desafio = base64Url(createHash('sha256').update(verificador).digest())
  const parametros = new URLSearchParams({
    client_id: CLIENT_ID,
    redirect_uri: REDIRECT_URI,
    response_type: 'code',
    scope: 'openid',
    code_challenge: desafio,
    code_challenge_method: 'S256',
  })

  const cookies = new Map()
  const paginaLogin = await fetch(`${KEYCLOAK}/protocol/openid-connect/auth?${parametros}`, {
    redirect: 'manual',
  })
  juntarCookies(cookies, paginaLogin)

  const html = await paginaLogin.text()
  const acao = html.match(/action="([^"]+)"/)?.[1]
  if (!acao) throw new Error('Formulário de login do Keycloak não encontrado.')

  const envio = await fetch(decodificarCorpoHtml(acao), {
    method: 'POST',
    redirect: 'manual',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
      Cookie: [...cookies.values()].join('; '),
    },
    body: new URLSearchParams({ username: usuario, password: senha }),
  })

  const destino = envio.headers.get('location')
  const codigo = destino ? new URL(destino).searchParams.get('code') : null
  if (!codigo) throw new Error(`Login recusado pelo Keycloak (status ${envio.status}).`)

  return trocarCodigoPorToken(codigo, verificador)
}

async function renovar(refreshToken) {
  const resposta = await fetch(`${KEYCLOAK}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'refresh_token',
      client_id: CLIENT_ID,
      refresh_token: refreshToken,
    }),
  })
  if (!resposta.ok) throw new Error(`Renovação de token falhou: ${resposta.status}`)
  return resposta.json()
}

/** Devolve uma função que entrega sempre um access token válido. */
export async function criarProvedorDeToken() {
  if (process.env.CODEX_TOKEN) {
    const fixo = process.env.CODEX_TOKEN
    return async () => fixo
  }

  const usuario = process.env.CODEX_DEV_USUARIO
  const senha = process.env.CODEX_DEV_SENHA
  if (!usuario || !senha) {
    throw new Error(
      'Defina CODEX_TOKEN, ou CODEX_DEV_USUARIO e CODEX_DEV_SENHA para login no Keycloak local.',
    )
  }

  let sessao = await loginPorFormulario(usuario, senha)
  let expiraEm = Date.now() / 1000 + sessao.expires_in

  return async () => {
    if (Date.now() / 1000 >= expiraEm - MARGEM_RENOVACAO_SEGUNDOS) {
      sessao = await renovar(sessao.refresh_token)
      expiraEm = Date.now() / 1000 + sessao.expires_in
    }
    return sessao.access_token
  }
}
