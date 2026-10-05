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

function comportamentoMudou(existente, desejado) {
  return CAMPOS_DE_COMPORTAMENTO.some(
    (chave) => (existente[chave] ?? null) !== (desejado[chave] ?? null),
  )
}

/**
 * Um campo da versão por campo canônico (a API não aceita o mesmo duas vezes). Cria o que falta e
 * atualiza o comportamento do que mudou; não remove — campo a mais na versão é decisão editorial,
 * não efeito de rodar a carga de novo.
 */
export async function sincronizarCamposDaVersao(api, versaoId, campos, idPorCaminho) {
  const { dados } = await api.chamar('GET', `/interno/versoes/${versaoId}/campos`)
  const existentes = new Map(dados.campos.map((campo) => [campo.campoLayoutId, campo]))
  const contagem = { criados: 0, atualizados: 0 }

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
    } else if (comportamentoMudou(existente, campo.campoVersao)) {
      await api.chamar(
        'PUT',
        `/interno/campos-versao/${existente.id}`,
        campo.campoVersao,
        String(existente.versaoConcorrencia),
      )
      contagem.atualizados++
    }
  }

  console.log(`campos da versão: ${contagem.criados} criados, ${contagem.atualizados} atualizados`)
}
