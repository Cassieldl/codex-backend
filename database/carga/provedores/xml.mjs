/**
 * Leitor de XML mínimo para os esquemas dos provedores: elementos, atributos e texto. Só função
 * pura. Ignora declaração, comentários e qualquer texto antes da raiz — o XSD da Elotech foi salvo
 * pelo navegador com uma frase solta no topo.
 */

const TOKEN =
  /<!--[\s\S]*?-->|<\?[\s\S]*?\?>|<!\[CDATA\[([\s\S]*?)\]\]>|<(\/?)([\w.:-]+)([^>]*?)(\/?)>|([^<]+)/g
const ATRIBUTO = /([\w.:-]+)\s*=\s*("([^"]*)"|'([^']*)')/g

function decodificar(texto) {
  return texto
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&')
}

/** Nome sem o prefixo de namespace: `xsd:element` e `xs:element` são o mesmo `element`. */
export function nomeLocal(nome) {
  return nome?.slice(nome.indexOf(':') + 1)
}

function lerAtributos(texto) {
  return Object.fromEntries(
    [...texto.matchAll(ATRIBUTO)].map(([, nome, , duplas, simples]) => [
      nome,
      decodificar(duplas ?? simples),
    ]),
  )
}

/** Devolve a raiz como `{ nome, atributos, filhos, texto }`, com `nome` já sem prefixo. */
export function lerXml(texto) {
  const topo = { nome: null, atributos: {}, filhos: [], texto: '' }
  const pilha = [topo]

  for (const [, cdata, fechamento, nome, atributos, autoFechado, conteudo] of texto.matchAll(
    TOKEN,
  )) {
    const atual = pilha.at(-1)
    if (conteudo !== undefined || cdata !== undefined) {
      if (atual !== topo) atual.texto += decodificar(conteudo ?? cdata)
      continue
    }
    // Comentário e declaração casam o token sem nome de elemento.
    if (!nome) continue

    if (fechamento) {
      if (nomeLocal(nome) !== atual.nome)
        throw new Error(`XML malformado: </${nome}> fecha <${atual.nome}>.`)
      pilha.pop()
      continue
    }

    const no = { nome: nomeLocal(nome), atributos: lerAtributos(atributos), filhos: [], texto: '' }
    atual.filhos.push(no)
    if (!autoFechado) pilha.push(no)
  }

  if (pilha.length !== 1) throw new Error(`XML malformado: <${pilha.at(-1).nome}> não fechado.`)
  const [raiz] = topo.filhos
  if (!raiz) throw new Error('XML sem elemento raiz.')
  return raiz
}
