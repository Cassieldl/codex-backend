/**
 * Etapa de campos da carga: vocabulário canônico do Layout InvoiCy e campos da versão do provedor.
 * Fala com a API interna; a montagem dos dados é das funções puras de transformar-layout.mjs,
 * resolver-mapeamento.mjs e transformar-campos.mjs.
 */

const CAMPOS_DE_COMPORTAMENTO = [
  'tipo',
  'tamanho',
  'mascara',
  'dominio',
  'estadoPreenchimento',
  'condicao',
  'notaPreenchimento',
  'destinoXml',
  'notaTransformacao',
  'ordem',
]

/** Grupo e ordem são a posição do campo na hierarquia do XSD, que é quem os decide. */
function posicaoMudou(existente, desejado) {
  return (existente.grupo ?? null) !== desejado.grupo || existente.ordem !== desejado.ordem
}

/**
 * Corpo do PUT que alinha o existente ao plano: posição (grupo e ordem, que o XSD decide) e
 * descontinuação. Nome e significado ficam como a edição os deixou. `null` quando nada muda.
 */
export function planejarAtualizacaoDeCampo(existente, desejado, idDescontinuacao) {
  const corpo = {}
  if (posicaoMudou(existente, desejado))
    Object.assign(corpo, { grupo: desejado.grupo, ordem: desejado.ordem })
  if (idDescontinuacao && existente.estado !== 'Descontinuado')
    Object.assign(corpo, { estado: 'Descontinuado', versaoLayoutDescontinuadoId: idDescontinuacao })
  return Object.keys(corpo).length ? corpo : null
}

function avisarDivergencias(existente, desejado, idOrigem) {
  // Origem e descontinuação passadas não têm caminho de volta na API; divergência é decisão editorial.
  if (existente.versaoLayoutId !== idOrigem)
    console.warn(`  ${desejado.caminhoXml}: origem na API difere da do plano (${desejado.origem}).`)
  if (!desejado.descontinuadoEm && existente.estado === 'Descontinuado')
    console.warn(
      `  ${desejado.caminhoXml}: descontinuado na API, mas presente na versão mais nova.`,
    )
}

/**
 * Sincroniza o vocabulário de todas as versões do layout a partir do plano multiversão. O campo é
 * identificado pelo caminho XML, único e imutável: existente nunca é recriado. Campo novo nasce na
 * versão de origem do plano e, se já saiu de uma versão posterior, é descontinuado nela. Devolve,
 * por rótulo, caminho completo → id dos campos ativos naquela versão.
 */
export async function sincronizarVocabulario(api, idPorRotulo, plano) {
  const existentePorCaminhoXml = new Map()
  for (const versaoLayoutId of idPorRotulo.values()) {
    const { dados } = await api.chamar('GET', `/interno/layout/versoes/${versaoLayoutId}/campos`)
    for (const campo of dados) existentePorCaminhoXml.set(campo.caminhoXml, campo)
  }

  const idPorCaminhoXml = new Map()
  const contagem = { criados: 0, atualizados: 0, descontinuados: 0 }
  for (const campo of plano.campos) {
    const idOrigem = idPorRotulo.get(campo.origem)
    const idDescontinuacao = campo.descontinuadoEm ? idPorRotulo.get(campo.descontinuadoEm) : null
    let existente = existentePorCaminhoXml.get(campo.caminhoXml)

    if (existente) {
      avisarDivergencias(existente, campo, idOrigem)
    } else {
      ;({ dados: existente } = await api.chamar(
        'POST',
        `/interno/layout/versoes/${idOrigem}/campos`,
        {
          nome: campo.nome,
          caminhoXml: campo.caminhoXml,
          significado: campo.significado,
          grupo: campo.grupo,
          ordem: campo.ordem,
        },
      ))
      contagem.criados++
      if (contagem.criados % 100 === 0) console.log(`  campos canônicos: ${contagem.criados}`)
    }
    idPorCaminhoXml.set(campo.caminhoXml, existente.id)

    const corpo = planejarAtualizacaoDeCampo(existente, campo, idDescontinuacao)
    if (!corpo) continue
    await api.chamar(
      'PUT',
      `/interno/campos-layout/${existente.id}`,
      corpo,
      String(existente.versaoConcorrencia),
    )
    contagem.atualizados++
    if (corpo.estado) contagem.descontinuados++
  }
  console.log(
    `vocabulário: ${contagem.criados} criados, ${contagem.atualizados} atualizados ` +
      `(${contagem.descontinuados} descontinuados), ${plano.campos.length - contagem.criados} já existiam`,
  )

  return new Map(
    [...plano.porVersao].map(([rotulo, campos]) => [
      rotulo,
      new Map(
        campos.map((campo) => [campo.caminhoCompleto, idPorCaminhoXml.get(campo.caminhoXml)]),
      ),
    ]),
  )
}

/**
 * O exemplo só entra na comparação quando o campo o traz (texto, ou `null` explícito): campo sem a
 * chave `exemplo` pertence a um provedor sem fonte, e o que existe na API não é da carga.
 */
function chavesComparadas(desejado) {
  return desejado.exemplo === undefined
    ? CAMPOS_DE_COMPORTAMENTO
    : [...CAMPOS_DE_COMPORTAMENTO, 'exemplo']
}

export function chavesAlteradas(existente, desejado) {
  return chavesComparadas(desejado).filter(
    (chave) => (existente[chave] ?? null) !== (desejado[chave] ?? null),
  )
}

/**
 * O PUT substitui o recurso inteiro: chave ausente no JSON vira `null` na API. Sem exemplo
 * gerenciado, o corpo reenvia o valor existente — e o `null` explícito, porque `JSON.stringify`
 * descarta `undefined`.
 */
function corpoDoPut(existente, desejado) {
  return desejado.exemplo === undefined
    ? { ...desejado, exemplo: existente.exemplo ?? null }
    : desejado
}

function classificarMudancaDeExemplo(de, para) {
  if (de === null) return 'definido'
  return para === null ? 'apagado' : 'alterado'
}

/**
 * Um campo da versão por campo canônico (a API não aceita o mesmo duas vezes). Cria o que falta e
 * atualiza o comportamento do que mudou; não remove — campo a mais na versão é decisão editorial,
 * não efeito de rodar a carga de novo. Em campo com exemplo gerenciado, a carga é dona do valor:
 * descartado vira `null` e apaga o que existia.
 */
export async function sincronizarCamposDaVersao(api, versaoId, campos, idPorCaminho) {
  const { dados } = await api.chamar('GET', `/interno/versoes/${versaoId}/campos`)
  const existentes = new Map(dados.campos.map((campo) => [campo.campoLayoutId, campo]))
  const contagem = {
    criados: 0,
    atualizados: 0,
    atualizadosSoExemplo: 0,
    exemplosDefinidos: 0,
    exemplosAlterados: 0,
    exemplosApagados: 0,
  }
  const mudancasDeExemplo = []

  for (const campo of campos) {
    const campoLayoutId = idPorCaminho.get(campo.caminhoInvoicy)
    if (!campoLayoutId) {
      throw new Error(
        `${campo.nomeCampo} aponta para ${campo.caminhoInvoicy}, que não está no vocabulário.`,
      )
    }

    const existente = existentes.get(campoLayoutId)
    if (!existente) {
      await api.chamar('POST', `/interno/versoes/${versaoId}/campos`, {
        campoLayoutId,
        ...campo.campoVersao,
      })
      contagem.criados++
      continue
    }

    const alteradas = chavesAlteradas(existente, campo.campoVersao)
    if (!alteradas.length) continue

    await api.chamar(
      'PUT',
      `/interno/campos-versao/${existente.id}`,
      corpoDoPut(existente, campo.campoVersao),
      String(existente.versaoConcorrencia),
    )
    contagem.atualizados++

    if (!alteradas.includes('exemplo')) continue
    if (alteradas.length === 1) contagem.atualizadosSoExemplo++

    const de = existente.exemplo ?? null
    const para = campo.campoVersao.exemplo
    const mudanca = classificarMudancaDeExemplo(de, para)
    if (mudanca === 'definido') contagem.exemplosDefinidos++
    if (mudanca === 'alterado') contagem.exemplosAlterados++
    if (mudanca === 'apagado') contagem.exemplosApagados++
    mudancasDeExemplo.push({ nomeCampo: campo.nomeCampo, de, para })
  }

  console.log(
    `campos da versão ${versaoId}: ${contagem.criados} criados, ${contagem.atualizados} atualizados ` +
      `(${contagem.atualizadosSoExemplo} só de exemplo, ` +
      `${contagem.atualizados - contagem.atualizadosSoExemplo} com outro atributo) · exemplos: ` +
      `${contagem.exemplosDefinidos} definidos, ${contagem.exemplosAlterados} alterados, ` +
      `${contagem.exemplosApagados} apagados`,
  )
  if (api.simulando) {
    for (const { nomeCampo, de, para } of mudancasDeExemplo)
      console.log(`  exemplo ${nomeCampo}: ${imprimirExemplo(de)} → ${imprimirExemplo(para)}`)
  }
  const apagados = mudancasDeExemplo.filter(({ para }) => para === null)
  if (apagados.length) {
    console.warn(
      `  exemplo apagado (a carga é dona do valor): ${apagados.map(({ nomeCampo }) => nomeCampo).join(', ')}`,
    )
  }

  return contagem
}

function imprimirExemplo(valor) {
  return valor === null ? '∅' : `"${valor}"`
}
