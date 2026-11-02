/**
 * Carga dos erros conhecidos copiados da Documentação (`fonte/`). Cria como rascunho e acrescenta o
 * que falta em erro existente não publicado; nunca apaga nem publica. Pode rodar de novo a
 * qualquer momento: o que já está no banco não é gravado outra vez.
 *
 *   CODEX_DEV_USUARIO=... CODEX_DEV_SENHA=... node database/carga/erros/importar.mjs \
 *     [--relatorio <arquivo.md>] [--fontes betha.md,gespam.md] [--aguardar Betha,GESPAM]
 *
 * Provedor ausente no banco não interrompe: as linhas dele ficam como "aguardando" no relatório.
 * `--aguardar` faz o mesmo com provedor que existe mas ainda está sendo carregado: a carga de
 * provedores remove os erros da versão que não estão no material dela.
 */

import { readFile, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { criarClienteApi, executarCarga } from '../cliente-api.mjs'
import { ROTULO_LAYOUT_DOS_PROVEDORES } from '../layout-invoicy.mjs'
import { PROVEDORES } from '../provedores/catalogo.mjs'
import { FONTES } from './fontes.mjs'
import { agruparErros, lerFonte } from './montar.mjs'
import { sincronizarErros } from './sincronizar.mjs'

const PASTA_FONTE = join(dirname(fileURLToPath(import.meta.url)), 'fonte')

function valorDoArgumento(nome) {
  const indice = process.argv.indexOf(nome)
  return indice >= 0 ? process.argv[indice + 1] : null
}

function itensDe(dados) {
  return dados.items ?? dados
}

async function lerFontes(arquivos) {
  const escolhidas = arquivos ? FONTES.filter((fonte) => arquivos.includes(fonte.arquivo)) : FONTES
  const lido = { itens: [], pulados: [], tabelasFora: [] }
  for (const fonte of escolhidas) {
    const { itens, pulados, tabelasFora } = lerFonte(
      fonte,
      await readFile(join(PASTA_FONTE, fonte.arquivo), 'utf8'),
    )
    lido.itens.push(...itens)
    lido.pulados.push(...pulados)
    lido.tabelasFora.push(...tabelasFora)
  }
  return { ...lido, erros: agruparErros(lido.itens) }
}

async function lerCamposDoLayout(api) {
  const versoes = await api.listarTudo('/interno/layout/versoes')
  const versao = versoes.find((item) => item.rotulo === ROTULO_LAYOUT_DOS_PROVEDORES)
  if (!versao) return []
  const { dados: campos } = await api.chamar('GET', `/interno/layout/versoes/${versao.id}/campos`)
  return campos
}

async function lerContexto(api, erros, aguardar) {
  const provedores = await api.listarTudo('/interno/provedores')
  const provedorPorNome = new Map(provedores.map((provedor) => [provedor.nome, provedor]))

  const nomesUsados = new Set(erros.map((erro) => erro.provedor).filter(Boolean))
  const versoesPorProvedor = new Map()
  const errosPorProvedor = new Map()
  for (const nome of nomesUsados) {
    const provedor = provedorPorNome.get(nome)
    if (!provedor) continue
    const { dados } = await api.chamar('GET', `/interno/provedores/${provedor.id}/versoes`)
    versoesPorProvedor.set(
      provedor.id,
      itensDe(dados).map((versao) => versao.id),
    )
    const existentes = await api.listarTudo(
      `/interno/erros?provedor=${encodeURIComponent(provedor.slug)}`,
    )
    errosPorProvedor.set(provedor.slug, new Map(existentes.map((erro) => [erro.codigo, erro])))
  }
  const doInvoicy = await api.listarTudo('/interno/erros?origem=InvoiCy')
  errosPorProvedor.set('InvoiCy', new Map(doInvoicy.map((erro) => [erro.codigo, erro])))

  const todos = await api.listarTudo('/interno/erros')
  const slugsOcupados = new Map(
    todos.map((erro) => [erro.slug, { id: erro.id, rotulo: `${erro.codigo} (erro ${erro.id})` }]),
  )

  return {
    provedorPorNome,
    provedorPublicadoPorNome: new Map(provedores.map((p) => [p.nome, p.publicado])),
    provedoresEmCriacao: new Set(Object.values(PROVEDORES).map((item) => item.provedor.nome)),
    aguardar,
    versoesPorProvedor,
    errosPorProvedor,
    slugsOcupados,
    camposDoLayout: await lerCamposDoLayout(api),
  }
}

function contarPorFonte(lido, resultado) {
  const fontes = new Map()
  const contar = (fonte, chave, quantidade = 1) => {
    const linha = fontes.get(fonte) ?? {
      criados: 0,
      acrescentados: 0,
      vinculados: 0,
      semMudanca: 0,
      soRelatorio: 0,
      colisoes: 0,
      aguardando: 0,
      pulados: 0,
    }
    linha[chave] += quantidade
    fontes.set(fonte, linha)
  }
  for (const pulado of lido.pulados) contar(pulado.fonte, 'pulados')
  for (const tabela of lido.tabelasFora) contar(tabela.fonte, 'pulados', tabela.linhas)
  for (const [chave, lista] of Object.entries(resultado)) {
    for (const item of lista) contar(item.fonte, chave)
  }
  return fontes
}

function linhaDoErro(item, extra) {
  return `- ${item.provedor ?? 'InvoiCy'} ${item.codigo ?? '(sem código)'} (${item.fonte}): ${extra}`
}

function montarRelatorio(lido, resultado, contexto) {
  const partes = ['## Erros conhecidos', '', '### Contagem por fonte', '']
  partes.push(
    '| Fonte | Criados | Acrescentados | Versão vinculada | Sem mudança | Só relatório | Colisão de slug | Aguardando provedor | Pulados |',
    '| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |',
  )
  for (const [fonte, contagem] of contarPorFonte(lido, resultado)) {
    partes.push(
      `| ${fonte} | ${contagem.criados} | ${contagem.acrescentados} | ${contagem.vinculados} | ${contagem.semMudanca} | ` +
        `${contagem.soRelatorio} | ${contagem.colisoes} | ${contagem.aguardando} | ${contagem.pulados} |`,
    )
  }

  const publicados = [...contexto.provedorPublicadoPorNome].filter(([, publicado]) => publicado)
  partes.push(
    '',
    `Provedores publicados no banco: ${publicados.map(([nome]) => nome).join(', ') || 'nenhum'}. ` +
      'O erro tem estado de publicação próprio e nasce como rascunho: os criados não aparecem no portal.',
  )

  const secoes = [
    [
      'Colisões de slug',
      resultado.colisoes,
      (item) => `slug ${item.slug} já é de ${item.dono}; não gravado`,
    ],
    [
      'Versão vinculada a erro igual de outro provedor (mesmo slug)',
      resultado.vinculados,
      (item) => `erro de ${item.dono}: ${item.acrescimos.join(', ')}`,
    ],
    [
      'Só relatório (erro existente publicado)',
      resultado.soRelatorio,
      (item) => `acrescentaria ${item.acrescimos.join(', ')}`,
    ],
    [
      'Acrescentados a erro existente não publicado',
      resultado.acrescentados,
      (item) => item.acrescimos.join(', '),
    ],
    ['Aguardando provedor', resultado.aguardando, (item) => item.motivo],
    ['Pulados na gravação', resultado.pulados, (item) => item.motivo],
    [
      'Pulados na leitura da fonte',
      lido.pulados,
      (item) => `linha ${item.linha}, ${item.secao}: ${item.motivo}`,
    ],
  ]
  for (const [titulo, lista, descrever] of secoes) {
    partes.push('', `### ${titulo} (${lista.length})`, '')
    if (!lista.length) partes.push('Nenhum.')
    for (const item of lista) partes.push(linhaDoErro(item, descrever(item)))
  }

  partes.push('', `### Tabelas fora do recorte (${lido.tabelasFora.length})`, '')
  for (const tabela of lido.tabelasFora) {
    partes.push(`- ${tabela.fonte}, "${tabela.secao}" (${tabela.linhas} linhas): ${tabela.motivo}`)
  }

  const fundidos = lido.erros.filter((erro) => erro.linhas.length > 1)
  partes.push('', `### Código repetido na fonte, fundido num erro só (${fundidos.length})`, '')
  for (const erro of fundidos) {
    partes.push(
      linhaDoErro(
        erro,
        `linhas ${erro.linhas.join(', ')}; variantes: ${erro.variantes.map((v) => `"${v}"`).join(' | ')}`,
      ),
    )
  }

  const comCampos = [
    ...resultado.criados,
    ...resultado.acrescentados,
    ...resultado.vinculados,
  ].filter((item) => item.camposIgnorados)
  partes.push('', '### Nomes de campo citados e não vinculados', '')
  if (!comCampos.length) partes.push('Nenhum.')
  for (const item of comCampos) partes.push(linhaDoErro(item, item.camposIgnorados.join(', ')))

  return `${partes.join('\n')}\n`
}

const arquivoRelatorio = valorDoArgumento('--relatorio')
const fontesEscolhidas = valorDoArgumento('--fontes')?.split(',') ?? null
const aguardar = new Set(valorDoArgumento('--aguardar')?.split(',') ?? [])

const lido = await lerFontes(fontesEscolhidas)
const api = await criarClienteApi()

await executarCarga(async () => {
  const contexto = await lerContexto(api, lido.erros, aguardar)
  const resultado = await sincronizarErros(api, lido.erros, contexto)

  console.log(
    `erros: ${resultado.criados.length} criados, ${resultado.acrescentados.length} acrescentados, ` +
      `${resultado.semMudanca.length} sem mudança, ${resultado.soRelatorio.length} só relatório, ` +
      `${resultado.colisoes.length} colisões, ${resultado.aguardando.length} aguardando, ` +
      `${resultado.pulados.length + lido.pulados.length} pulados`,
  )
  if (arquivoRelatorio) {
    await writeFile(arquivoRelatorio, montarRelatorio(lido, resultado, contexto))
    console.log(`relatório: ${arquivoRelatorio}`)
  }
  api.imprimirResumoDaSimulacao()
  console.log('\ncarga concluída. Nada foi publicado.')
})
