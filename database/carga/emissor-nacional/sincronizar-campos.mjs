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
 * Cria os campos canônicos que faltam, identificados pelo caminho XML — que é único e imutável, então
 * um campo existente nunca é recriado nem tem o caminho alterado. Do existente, só a posição (grupo
 * e ordem) acompanha o XSD; nome e significado ficam como a edição os deixou. Devolve caminho
 * completo → id.
 */
export async function sincronizarVocabulario(api, versaoLayoutId, vocabulario) {
  const { dados: existentes } = await api.chamar(
    'GET',
    `/interno/layout/versoes/${versaoLayoutId}/campos`,
  )
  const existentePorCaminhoXml = new Map(existentes.map((campo) => [campo.caminhoXml, campo]))
  const idPorCaminhoXml = new Map(existentes.map((campo) => [campo.caminhoXml, campo.id]))

  let criados = 0
  let reposicionados = 0
  for (const campo of vocabulario) {
    const existente = existentePorCaminhoXml.get(campo.caminhoXml)
    if (existente) {
      if (posicaoMudou(existente, campo)) {
        await api.chamar(
          'PUT',
          `/interno/campos-layout/${existente.id}`,
          { grupo: campo.grupo, ordem: campo.ordem },
          String(existente.versaoConcorrencia),
        )
        reposicionados++
      }
      continue
    }
    const { dados: criado } = await api.chamar(
      'POST',
      `/interno/layout/versoes/${versaoLayoutId}/campos`,
      {
        nome: campo.nome,
        caminhoXml: campo.caminhoXml,
        significado: campo.significado,
        grupo: campo.grupo,
        ordem: campo.ordem,
      },
    )
    idPorCaminhoXml.set(campo.caminhoXml, criado.id)
    criados++
    if (criados % 100 === 0) console.log(`  campos canônicos: ${criados}`)
  }
  console.log(
    `vocabulário: ${criados} criados, ${reposicionados} reposicionados, ` +
      `${vocabulario.length - criados} já existiam`,
  )

  return new Map(
    vocabulario.map((campo) => [campo.caminhoCompleto, idPorCaminhoXml.get(campo.caminhoXml)]),
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
