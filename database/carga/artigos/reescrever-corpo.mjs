/**
 * Prepara o Markdown de um artigo da Documentação para o Codex. Só funções puras.
 *
 * O título sai da primeira linha. Link para outro arquivo da Documentação vira a rota pública do
 * Codex quando o destino existe lá (artigo carregado: /artigos/<slug>; provedor: /provedores/<slug>)
 * e vira texto quando não existe. Imagem sai, porque o Codex não hospeda os prints. Marca de fonte
 * entre colchetes sai. Link externo fica.
 */

import { posix } from 'node:path'

import { MARCA_PENDENTE, removerMarcasDeFonte, textoSimples } from '../erros/documentacao.mjs'

const LINK = /(?<!!)\[([^\]]+)\]\(([^)\s]+)\)/g
const IMAGEM = /!\[[^\]]*\]\(([^)\s]+)\)/g
const LINHA_SO_DE_IMAGEM = /^\s*!\[[^\]]*\]\([^)\s]+\)\s*$/
const ALVO_DE_IMAGEM = /(^|\/)assets\/|\.(png|jpe?g|gif|svg|webp)$/i
const SEM_IMAGEM = '\u0000'
const SEM_LINK = '\u0001'
/** Texto de link que, sem o link, não diz nada ("Ver artigo."): a referência sai inteira. */
const TEXTO_GENERICO = /^(artigo|lista|aqui)$/i
const CELULA_SO_COM_IMAGEM = 'Só em imagem, não incluída.'
const CAMINHO_DE_ORIGEM_ENTRE_PARENTESES = /\s*\(`_origem\/[^`]*`\)/g

/** Âncora que o portal gera para h2 e h3 (secoesDoArtigo.ts do frontend). */
export function ancoraDoPortal(titulo) {
  const normalizado = textoSimples(titulo)
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
  return `secao-${normalizado}`
}

/** Só letras e números, sem acento: compara âncora, nome de provedor e título. */
export function soLetrasENumeros(texto) {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
}

/** Títulos de seção (h2 e h3), os únicos que ganham âncora no portal. */
export function titulosDeSecao(markdown) {
  return [...markdown.matchAll(/^#{2,3}\s+(.+)$/gm)].map((achado) => achado[1].trim())
}

/**
 * A âncora da Documentação segue o GitHub e foi escrita antes do saneamento; a comparação só por
 * letras e números casa as duas grafias.
 */
function ancoraEquivalente(fragmento, titulos) {
  const procurado = soLetrasENumeros(decodeURIComponent(fragmento))
  const titulo = titulos.find(
    (candidato) => soLetrasENumeros(textoSimples(candidato)) === procurado,
  )
  return titulo ? ancoraDoPortal(titulo) : null
}

/** Separa a primeira linha (`# Título`) do corpo. */
export function separarTitulo(markdown) {
  const achado = /^#\s+(.+)\r?\n+/.exec(markdown)
  if (!achado) throw new Error('O artigo não começa com o título (# Título).')
  return { titulo: achado[1].trim(), corpo: markdown.slice(achado[0].length) }
}

function rotuloSemArquivo(texto, rotulos) {
  if (!/\.md$/.test(texto)) return texto
  return rotulos[texto] ?? texto.replace(/\.md$/, '').replace(/-/g, ' ')
}

function resolverAlvo(alvo, arquivo) {
  const [caminho, fragmento = null] = alvo.split('#')
  if (!caminho) return { destino: arquivo, fragmento }
  return { destino: posix.normalize(posix.join(posix.dirname(arquivo), caminho)), fragmento }
}

function rotaDoDestino(destino, fragmento, contexto) {
  const artigo = contexto.artigos.get(destino)
  if (artigo) {
    const ancora = fragmento ? ancoraEquivalente(fragmento, artigo.titulos) : null
    // Âncora para o próprio artigo que não existe mais seria um link para a mesma página.
    if (destino === contexto.arquivo && !ancora) return null
    return `/artigos/${artigo.slug}${ancora ? `#${ancora}` : ''}`
  }

  const provedor = contexto.provedorPorRejeicao.get(destino)
  if (provedor) return `/provedores/${provedor}`

  if (destino === '02-observacoes/observacoes-provedores.md' && fragmento) {
    const daAncora = contexto.provedorPorAncoraDeObservacao.get(soLetrasENumeros(fragmento))
    if (daAncora) return `/provedores/${daAncora}`
  }
  return null
}

/**
 * Remove o resto de um trecho que só tinha imagem: separadores que sobraram e, na tabela, a célula
 * vazia ganha um aviso curto em vez de ficar em branco.
 */
function limparLinhaSemImagem(linha) {
  if (!linha.includes(SEM_IMAGEM)) return linha

  if (/^\s*\|.*\|\s*$/.test(linha)) {
    const celulas = linha.trim().slice(1, -1).split('|')
    const limpas = celulas.map((celula) => {
      if (!celula.includes(SEM_IMAGEM)) return celula
      const texto = celula
        .replaceAll(SEM_IMAGEM, '')
        .replace(/^[\s.·]*(e\s+)?/, '')
        .replace(/\s*(\be|·)?\s*$/, '')
      return ` ${texto || CELULA_SO_COM_IMAGEM} `
    })
    return `|${limpas.join('|')}|`
  }

  const texto = linha
    .replaceAll(SEM_IMAGEM, '')
    .replace(/\s*·\s*(?=·|\.|$)/g, '')
    .replace(/\s{2,}/g, ' ')
  return /^[^:]*:\s*[.·\s]*$/.test(texto) ? null : texto.replace(/\s+\./g, '.')
}

/** Problemas que impedem gravar o corpo: resto de caminho da Documentação, imagem ou pendência. */
export function problemasDoCorpo(corpo) {
  const problemas = []
  if (/\]\([^)]*\.md([#)])/.test(corpo)) problemas.push('link para arquivo .md')
  if (
    /\]\([^)]*(01-layouts|02-observacoes|03-rejeicoes|04-base-conhecimento|_origem|assets\/)/.test(
      corpo,
    )
  )
    problemas.push('link para caminho da Documentação')
  if (/_origem\/|pendencias-revisao/.test(corpo)) problemas.push('caminho da Documentação no texto')
  if (/!\[/.test(corpo)) problemas.push('imagem')
  if (corpo.includes(MARCA_PENDENTE)) problemas.push('marca de pendência')
  if (corpo.includes(SEM_IMAGEM) || corpo.includes(SEM_LINK))
    problemas.push('resto de link removido')
  return problemas
}

/**
 * `contexto`: `arquivo` (caminho na Documentação), `slug` do próprio artigo, `artigos` (caminho ->
 * { slug, titulos }), `provedorPorRejeicao` (caminho -> slug do provedor),
 * `provedorPorAncoraDeObservacao` (âncora só com letras e números -> slug) e `rotulos` (nome de
 * arquivo -> texto). Devolve o título, o corpo e o registro do que mudou.
 */
export function prepararArtigo(markdown, contexto) {
  const { titulo, corpo: semTitulo } = separarTitulo(markdown)
  const registro = { linksReescritos: [], linksRemovidos: [], imagensRemovidas: [] }
  const proprios = { ...contexto, artigos: new Map(contexto.artigos) }
  proprios.artigos.set(contexto.arquivo, {
    slug: contexto.slug,
    titulos: titulosDeSecao(semTitulo),
  })

  const semLinhasDeImagem = semTitulo
    .split('\n')
    .filter((linha) => {
      if (!LINHA_SO_DE_IMAGEM.test(linha)) return true
      registro.imagensRemovidas.push(linha.trim().match(/\(([^)\s]+)\)/)[1])
      return false
    })
    .join('\n')

  const semImagensNoTexto = semLinhasDeImagem.replace(IMAGEM, (_, alvo) => {
    registro.imagensRemovidas.push(alvo)
    return SEM_IMAGEM
  })

  const comLinks = semImagensNoTexto.replace(LINK, (original, texto, alvo) => {
    if (/^(https?:|mailto:)/.test(alvo)) return original
    if (ALVO_DE_IMAGEM.test(alvo)) {
      registro.imagensRemovidas.push(alvo)
      return SEM_IMAGEM
    }

    const { destino, fragmento } = resolverAlvo(alvo, contexto.arquivo)
    const rota = rotaDoDestino(destino, fragmento, proprios)
    const rotulo = rotuloSemArquivo(texto, contexto.rotulos)
    if (rota) {
      registro.linksReescritos.push({ de: alvo, para: rota })
      return `[${rotulo}](${rota})`
    }
    registro.linksRemovidos.push({ de: alvo, texto: rotulo })
    return TEXTO_GENERICO.test(rotulo) ? SEM_LINK : rotulo
  })

  const corpo = removerMarcasDeFonte(comLinks)
    .replace(new RegExp(`\\s*Ver\\s+${SEM_LINK}\\.?`, 'g'), '')
    .replaceAll(SEM_LINK, '')
    .replace(CAMINHO_DE_ORIGEM_ENTRE_PARENTESES, '')
    .split('\n')
    .map(limparLinhaSemImagem)
    .filter((linha) => linha !== null)
    .join('\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim()

  const problemas = problemasDoCorpo(corpo)
  if (problemas.length) {
    throw new Error(`O artigo "${titulo}" ainda tem: ${problemas.join(', ')}.`)
  }
  return { titulo, corpo: `${corpo}\n`, ...registro }
}
