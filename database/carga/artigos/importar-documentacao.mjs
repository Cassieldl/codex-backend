/**
 * Carga dos artigos da base de conhecimento da Documentação (`documentacao/`), sempre como
 * rascunho. Cria pelo título ou atualiza o rascunho quando o corpo, o tipo ou os vínculos mudaram;
 * artigo já publicado não é tocado, fica no relatório. Nada aqui publica.
 *
 *   CODEX_DEV_USUARIO=... CODEX_DEV_SENHA=... node database/carga/artigos/importar-documentacao.mjs \
 *     [--relatorio <arquivo.md>]
 *
 * Rodar depois da carga de erros (erros/importar.mjs): os vínculos com erros só nascem para erros
 * que já existem.
 */

import { readFile, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { criarClienteApi, executarCarga } from '../cliente-api.mjs'
import { chaveDeSlug } from '../emissor-nacional/transformar-layout.mjs'
import { temMarcaDePendencia } from '../erros/documentacao.mjs'
import {
  ARTIGOS_DA_DOCUMENTACAO,
  ARTIGOS_FORA,
  REJEICOES_DO_PROVEDOR,
  ROTULO_DO_ARQUIVO,
} from './documentacao.mjs'
import {
  prepararArtigo,
  separarTitulo,
  soLetrasENumeros,
  titulosDeSecao,
} from './reescrever-corpo.mjs'

const PASTA = join(dirname(fileURLToPath(import.meta.url)), 'documentacao')

function valorDoArgumento(nome) {
  const indice = process.argv.indexOf(nome)
  return indice >= 0 ? process.argv[indice + 1] : null
}

function mesmoConjunto(a, b) {
  const ordenado = (ids) => [...ids].sort((x, y) => x - y).join(',')
  return ordenado(a) === ordenado(b)
}

async function lerArtigos() {
  const lidos = []
  const fora = ARTIGOS_FORA.map((artigo) => ({ ...artigo }))
  for (const artigo of ARTIGOS_DA_DOCUMENTACAO) {
    const markdown = await readFile(join(PASTA, artigo.arquivo), 'utf8')
    if (temMarcaDePendencia(markdown)) {
      fora.push({ origem: artigo.origem, motivo: 'tem marca de pendência' })
      continue
    }
    const { titulo, corpo } = separarTitulo(markdown)
    lidos.push({
      ...artigo,
      markdown,
      titulo,
      slug: chaveDeSlug(titulo),
      titulos: titulosDeSecao(corpo),
    })
  }
  return { lidos, fora }
}

async function lerProvedores(api) {
  const provedores = await api.listarTudo('/interno/provedores')
  const slugPorNome = new Map(provedores.map((provedor) => [provedor.nome, provedor.slug]))
  return {
    slugPorNome,
    provedorPorRejeicao: new Map(
      Object.entries(REJEICOES_DO_PROVEDOR)
        .filter(([, nome]) => slugPorNome.has(nome))
        .map(([caminho, nome]) => [caminho, slugPorNome.get(nome)]),
    ),
    provedorPorAncoraDeObservacao: new Map(
      provedores.map((provedor) => [soLetrasENumeros(provedor.nome), provedor.slug]),
    ),
  }
}

/** Erro citado vira id só quando o código aponta para um erro só daquele provedor (ou do InvoiCy). */
async function resolverErros(api, lidos, slugPorNome) {
  const idsPorCodigo = new Map()
  const consultar = async (provedor) => {
    if (idsPorCodigo.has(provedor)) return idsPorCodigo.get(provedor)
    const slug = provedor ? slugPorNome.get(provedor) : null
    const erros =
      provedor && !slug
        ? []
        : await api.listarTudo(
            provedor
              ? `/interno/erros?provedor=${encodeURIComponent(slug)}`
              : '/interno/erros?origem=InvoiCy',
          )
    const porCodigo = new Map()
    for (const erro of erros)
      porCodigo.set(erro.codigo, [...(porCodigo.get(erro.codigo) ?? []), erro.id])
    idsPorCodigo.set(provedor, porCodigo)
    return porCodigo
  }

  const resolvidos = new Map()
  const naoResolvidos = []
  for (const artigo of lidos) {
    const ids = []
    for (const { provedor, codigo } of artigo.erros) {
      const encontrados = (await consultar(provedor)).get(codigo) ?? []
      if (encontrados.length === 1) ids.push(encontrados[0])
      else
        naoResolvidos.push({
          titulo: artigo.titulo,
          erro: `${provedor ?? 'InvoiCy'} ${codigo}`,
          motivo: encontrados.length ? 'código ambíguo' : 'erro não existe no banco',
        })
    }
    resolvidos.set(artigo.arquivo, [...new Set(ids)])
  }
  return { resolvidos, naoResolvidos }
}

async function sincronizar(api, desejado, existentePorTitulo, slugsOcupados) {
  const existente = existentePorTitulo.get(desejado.titulo)
  if (!existente) {
    const dono = slugsOcupados.get(chaveDeSlug(desejado.titulo))
    if (dono) return { situacao: 'colisão', detalhe: `slug já é do artigo "${dono}"` }
    await api.chamar('POST', '/interno/artigos', desejado)
    return { situacao: 'criado' }
  }

  const { dados: atual } = await api.chamar('GET', `/interno/artigos/${existente.id}`)
  const mudou =
    atual.tipo !== desejado.tipo ||
    atual.corpo !== desejado.corpo ||
    !mesmoConjunto(atual.campoLayoutIds, desejado.campoLayoutIds) ||
    !mesmoConjunto(atual.erroIds, desejado.erroIds)
  if (!mudou) return { situacao: 'sem mudança' }
  if (atual.publicado) return { situacao: 'só relatório', detalhe: 'artigo já publicado' }

  await api.chamar(
    'PUT',
    `/interno/artigos/${atual.id}`,
    desejado,
    String(atual.versaoConcorrencia),
  )
  return { situacao: 'atualizado' }
}

function montarRelatorio({ fora, preparados, situacoes, naoResolvidos }) {
  const contagem = new Map()
  for (const { situacao } of situacoes) contagem.set(situacao, (contagem.get(situacao) ?? 0) + 1)

  const partes = ['## Artigos', '', '### Contagem', '']
  partes.push(`- Na base de conhecimento: ${preparados.length + fora.length} arquivos.`)
  for (const [situacao, quantidade] of contagem) partes.push(`- ${situacao}: ${quantidade}`)
  partes.push(`- fora: ${fora.length}`, '', '### Fora da carga', '')
  for (const item of fora) partes.push(`- ${item.origem}: ${item.motivo}`)

  partes.push(
    '',
    '### Situação de cada artigo',
    '',
    '| Arquivo | Título | Tipo | Situação |',
    '| :--- | :--- | :--- | :--- |',
  )
  for (const [indice, artigo] of preparados.entries()) {
    const { situacao, detalhe } = situacoes[indice]
    partes.push(
      `| ${artigo.arquivo} | ${artigo.titulo} | ${artigo.tipo} | ${situacao}${detalhe ? ` (${detalhe})` : ''} |`,
    )
  }

  const listar = (titulo, extrair) => {
    const linhas = preparados.flatMap((artigo) =>
      extrair(artigo).map((texto) => `- ${artigo.arquivo}: ${texto}`),
    )
    partes.push(
      '',
      `### ${titulo} (${linhas.length})`,
      '',
      ...(linhas.length ? linhas : ['Nenhum.']),
    )
  }
  listar('Links reescritos para rota do Codex', (artigo) =>
    artigo.linksReescritos.map((link) => `${link.de} -> ${link.para}`),
  )
  listar('Links removidos (texto mantido)', (artigo) =>
    artigo.linksRemovidos.map((link) => `${link.de} ("${link.texto}")`),
  )
  listar('Imagens removidas', (artigo) => artigo.imagensRemovidas)

  partes.push('', `### Erros citados sem vínculo (${naoResolvidos.length})`, '')
  for (const item of naoResolvidos) partes.push(`- ${item.titulo}: ${item.erro} (${item.motivo})`)
  return `${partes.join('\n')}\n`
}

const arquivoRelatorio = valorDoArgumento('--relatorio')
const { lidos, fora } = await lerArtigos()
const api = await criarClienteApi()

await executarCarga(async () => {
  const provedores = await lerProvedores(api)
  const artigos = new Map(
    lidos.map((artigo) => [artigo.origem, { slug: artigo.slug, titulos: artigo.titulos }]),
  )

  const preparados = lidos.map((artigo) => ({
    ...artigo,
    ...prepararArtigo(artigo.markdown, {
      arquivo: artigo.origem,
      slug: artigo.slug,
      artigos,
      provedorPorRejeicao: provedores.provedorPorRejeicao,
      provedorPorAncoraDeObservacao: provedores.provedorPorAncoraDeObservacao,
      rotulos: ROTULO_DO_ARQUIVO,
    }),
  }))

  const { resolvidos, naoResolvidos } = await resolverErros(api, preparados, provedores.slugPorNome)
  const existentes = await api.listarTudo('/interno/artigos')
  const existentePorTitulo = new Map(existentes.map((artigo) => [artigo.titulo, artigo]))
  const slugsOcupados = new Map(existentes.map((artigo) => [artigo.slug, artigo.titulo]))

  const situacoes = []
  for (const artigo of preparados) {
    const desejado = {
      tipo: artigo.tipo,
      titulo: artigo.titulo,
      corpo: artigo.corpo,
      campoLayoutIds: [],
      erroIds: resolvidos.get(artigo.arquivo),
    }
    const situacao = await sincronizar(api, desejado, existentePorTitulo, slugsOcupados)
    console.log(`${situacao.situacao}: ${artigo.titulo}`)
    situacoes.push(situacao)
  }

  if (arquivoRelatorio) {
    await writeFile(
      arquivoRelatorio,
      montarRelatorio({ fora, preparados, situacoes, naoResolvidos }),
    )
    console.log(`relatório: ${arquivoRelatorio}`)
  }
  api.imprimirResumoDaSimulacao()
  console.log('\nartigos carregados como rascunho. Nada foi publicado.')
})
