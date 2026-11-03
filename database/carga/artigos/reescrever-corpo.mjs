/**
 * Prepara o Markdown de um artigo da Documentação para o Codex. Só funções puras.
 *
 * O título sai da primeira linha. Link para outro arquivo da Documentação vira a rota pública do
 * Codex quando o destino existe lá (artigo carregado: /artigos/<slug>; provedor: /provedores/<slug>)
 * e vira texto quando não existe. Imagem de `assets/imagens/` fica, com o caminho que o portal
 * serve (/imagens/<pasta>/<arquivo>), o mesmo texto alternativo e a mesma legenda; os prints do
 * Betha legado e qualquer outra imagem saem. Link para arquivo de imagem sai. Marca de fonte entre
 * colchetes sai. Link externo fica.
 */

import { posix } from 'node:path'

import { MARCA_PENDENTE, removerMarcasDeFonte, textoSimples } from '../erros/documentacao.mjs'

const LINK = /(?<!!)\[([^\]]+)\]\(([^)\s]+)\)/g
const IMAGEM = /!\[([^\]]*)\]\(([^)\s]+)(\s+"[^"\n]*")?\)/g
const EXTENSAO_DE_IMAGEM = /\.(png|jpe?g|gif|svg|webp)$/i
const NA_PASTA_DE_ASSETS = /(^|\/)assets\//
const NA_PASTA_DE_IMAGENS = /(?:^|\/)assets\/imagens\/(.+)$/
/** Prints do Betha ABRASF, que não vale mais: o Betha atual é leiaute nacional. */
export const IMAGENS_DESCARTADAS = new Set([
  'betha/natureza-da-operacao-abrasf.jpg',
  'betha/regime-especial-de-tributacao-abrasf.jpg',
  'betha/forma-de-pagamento-betha.jpg',
])
/** Único formato de imagem aceito no corpo: caminho do portal, sem `..`, legenda opcional. */
const IMAGEM_DO_PORTAL =
  /!\[[^\]]*\]\(\/imagens\/(?:[\w-][\w.-]*\/)*[\w-][\w.-]*\.(?:png|jpe?g|gif|svg|webp)(?:\s+"[^"\n]*")?\)/gi
const LINK_QUALQUER = /(?<!!)\[[^\]]*\]\(([^)\s]+)[^)]*\)/g
const SEM_IMAGEM = '\u0000'
const SEM_LINK = '\u0001'
/** Imagem mantida fica guardada fora do texto até o fim, para nenhuma limpeza alterá-la. */
const IMAGEM_GUARDADA = /\u0002(\d+)\u0003/g
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

/** Caminho do portal para a imagem, ou null quando ela não fica no corpo. */
function caminhoDaImagemNoPortal(alvo) {
  const achado = NA_PASTA_DE_IMAGENS.exec(alvo)
  if (!achado || IMAGENS_DESCARTADAS.has(achado[1])) return null
  return `/imagens/${achado[1]}`
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
  if (corpo.split('![').length - 1 > [...corpo.matchAll(IMAGEM_DO_PORTAL)].length)
    problemas.push('imagem fora de /imagens/')
  if ([...corpo.matchAll(LINK_QUALQUER)].some(([, alvo]) => EXTENSAO_DE_IMAGEM.test(alvo)))
    problemas.push('link para imagem')
  if (corpo.includes(MARCA_PENDENTE)) problemas.push('marca de pendência')
  if (corpo.includes(SEM_IMAGEM) || corpo.includes(SEM_LINK) || corpo.includes('\u0002'))
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
  const registro = {
    linksReescritos: [],
    linksRemovidos: [],
    imagensMantidas: [],
    imagensRemovidas: [],
  }
  const proprios = { ...contexto, artigos: new Map(contexto.artigos) }
  proprios.artigos.set(contexto.arquivo, {
    slug: contexto.slug,
    titulos: titulosDeSecao(semTitulo),
  })

  const imagensGuardadas = []
  const comImagensTratadas = semTitulo.replace(IMAGEM, (_, alt, alvo, legenda = '') => {
    const caminho = caminhoDaImagemNoPortal(alvo)
    if (!caminho) {
      registro.imagensRemovidas.push(alvo)
      return SEM_IMAGEM
    }
    registro.imagensMantidas.push({ de: alvo, para: caminho })
    imagensGuardadas.push(`![${alt}](${caminho}${legenda})`)
    return `\u0002${imagensGuardadas.length - 1}\u0003`
  })
  const semLinhasDeImagem = comImagensTratadas
    .split('\n')
    .filter((linha) => !/^[\s\u0000]*\u0000[\s\u0000]*$/.test(linha))
    .join('\n')

  const comLinks = semLinhasDeImagem.replace(LINK, (original, texto, alvo) => {
    const ehImagem = EXTENSAO_DE_IMAGEM.test(alvo.split(/[?#]/)[0])
    if (ehImagem || (!/^(https?:|mailto:)/.test(alvo) && NA_PASTA_DE_ASSETS.test(alvo))) {
      registro.imagensRemovidas.push(alvo)
      return SEM_IMAGEM
    }
    if (/^(https?:|mailto:)/.test(alvo)) return original

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
    .replace(IMAGEM_GUARDADA, (_, indice) => imagensGuardadas[Number(indice)])

  const problemas = problemasDoCorpo(corpo)
  if (problemas.length) {
    throw new Error(`O artigo "${titulo}" ainda tem: ${problemas.join(', ')}.`)
  }
  return { titulo, corpo: `${corpo}\n`, ...registro }
}
