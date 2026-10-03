/**
 * Liga cada campo InvoiCy do mapeamento (código e nome, como aparecem na configuração do provedor)
 * ao caminho completo do XSD de entrada. Só funções puras.
 *
 * Nome único no XSD resolve sozinho — salvo nos blocos em que o nome casa com um grupo de outro
 * significado, que ficam em PENDENTES. O resto é decisão registrada aqui, com o motivo, e qualquer
 * campo que não caia em nenhum dos três casos interrompe a montagem em vez de ser adivinhado.
 * Relatório completo em .dev-team/demandas/008-campos-emissor-nacional/relatorio-completude.md.
 */

/** Grafia diferente entre a configuração do provedor e o XSD; confirmado pelo usuário em 2026-10-03. */
export const RESOLVIDOS_POR_DECISAO = new Map([
  [241, 'Envio/RPS/Tomador/TomaxMun'],
  [833, 'Envio/RPS/Servico/IBSCBS/tpRetPisCofins'],
  [763, 'Envio/RPS/ListaNFSeReferenciadas'],
  // Dois cMun no XSD; cLocEmi é o município emissor, e quem emite é o prestador.
  [188, 'Envio/RPS/Prestador/enderPrest/cMun'],
])

/** O mesmo campo do InvoiCy aparece com dois códigos na configuração: a planilha diz "CNPJ ou CPF". */
export const UNIFICADOS = new Map([[491, 89]])

/** Não é campo: o Id da DPS é composto pelo InvoiCy a partir de número e série. */
export const COMPOSICOES = new Map([[305, [2, 3]]])

const MOTIVO_TPOPER =
  'No XSD e na planilha v8, tpOper (valor de 1 a 5) é um grupo com os campos de substituição, e RPSSubs é vazio.'

/** Esperando resposta do time do InvoiCy — ver seção 3 do relatório de completude. */
export const PENDENTES = new Map([
  ...[746, 747, 748, 749, 750, 751, 752, 753, 754, 755, 756, 757, 758, 759, 760, 761, 765].map(
    (codigo) => [codigo, 'Bloco gReeRepRes (1.01) sem equivalente confiável no layout v8.'],
  ),
  ...[635, 636, 637, 638, 639, 640, 641, 642].map((codigo) => [
    codigo,
    'Campo "…Ex" que não existe no layout de entrada.',
  ]),
  [652, 'indTotTrib não existe no layout de entrada.'],
  ...[669, 555, 556, 557].map((codigo) => [codigo, MOTIVO_TPOPER]),
])

/**
 * Regiões do XSD cujo caminho não é confiável: ficam fora do vocabulário até o InvoiCy confirmar,
 * porque o caminho do campo canônico não muda depois de criado.
 */
export const CAMINHOS_EM_DUVIDA = ['Envio/RPS/tpOper', 'Envio/RPS/RPSSubs']

/**
 * Devolve as tags do mapeamento com `caminhoInvoicy` preenchido nas que alimentam um campo
 * resolvido, e a lista do que ficou de fora. Tag pendente ou de composição sai com `codigoCampo` 0,
 * o que a tira da tabela da versão.
 */
export function resolverMapeamento(tags, elementosXsd) {
  const porNome = new Map()
  for (const elemento of elementosXsd) {
    const lista = porNome.get(elemento.nome) ?? []
    lista.push(elemento.caminho)
    porNome.set(elemento.nome, lista)
  }

  const pendentes = new Map()
  const resolvidas = tags.map((tag) => {
    if (!tag.codigoCampo) return tag

    if (PENDENTES.has(tag.codigoCampo)) {
      pendentes.set(tag.codigoCampo, {
        nomeCampo: tag.nomeCampo,
        motivo: PENDENTES.get(tag.codigoCampo),
      })
      return { ...tag, codigoCampo: 0 }
    }
    if (COMPOSICOES.has(tag.codigoCampo))
      return { ...tag, codigoCampo: 0, compoeDe: COMPOSICOES.get(tag.codigoCampo) }

    const codigoCampo = UNIFICADOS.get(tag.codigoCampo) ?? tag.codigoCampo
    const nomeCampo =
      codigoCampo === tag.codigoCampo
        ? tag.nomeCampo
        : tags.find((outra) => outra.codigoCampo === codigoCampo).nomeCampo

    const caminhoInvoicy =
      RESOLVIDOS_POR_DECISAO.get(codigoCampo) ?? caminhoUnico(nomeCampo, porNome)
    return { ...tag, codigoCampo, nomeCampo, caminhoInvoicy }
  })

  return { tags: resolvidas, pendentes }
}

function caminhoUnico(nomeCampo, porNome) {
  const candidatos = porNome.get(nomeCampo) ?? []
  if (candidatos.length !== 1) {
    throw new Error(
      `O campo ${nomeCampo} tem ${candidatos.length} caminhos no XSD e não está nas decisões registradas.`,
    )
  }
  return candidatos[0]
}

/** Nota para os campos que também compõem outra tag da DPS (o Id é número + série). */
export function notasDeComposicao(tags) {
  const notas = new Map()
  for (const tag of tags.filter((item) => item.compoeDe)) {
    for (const codigo of tag.compoeDe) {
      notas.set(
        codigo,
        `Também compõe o ${tag.nomeTag} da DPS, montado pelo InvoiCy a partir do número e da série.`,
      )
    }
  }
  return notas
}
