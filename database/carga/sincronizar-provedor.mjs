/**
 * Etapas de carga comuns a todo provedor: provedor, versão, avisos e erros (o layout InvoiCy é de
 * layout-invoicy.mjs). Fala com a API interna e sincroniza em vez de só criar: cria o que falta,
 * corrige o que mudou e remove o que deixou de valer para a versão. Nada aqui publica.
 */

import { planejarAtualizacaoDeErro } from './emissor-nacional/transformar.mjs'

function itensDe(dados) {
  return dados.items ?? dados
}

function mesmaLista(atual, desejada) {
  return [...atual].sort().join('\n') === [...desejada].sort().join('\n')
}

/** Aliases do catálogo valem para o provedor existente; nome e slug continuam como estão. */
async function alinharAliases(api, provedorId, provedor) {
  const { dados: atual } = await api.chamar('GET', `/interno/provedores/${provedorId}`)
  const desejados = provedor.aliases ?? []
  if (mesmaLista(atual.aliases ?? [], desejados)) return

  await api.chamar(
    'PUT',
    `/interno/provedores/${provedorId}`,
    { nome: atual.nome, categoria: atual.categoria, aliases: desejados },
    String(atual.versaoConcorrencia),
  )
  console.log(`aliases de ${atual.nome} alinhados: ${desejados.join(', ') || 'nenhum'}`)
}

export async function garantirProvedor(api, provedor) {
  const provedores = await api.listarTudo(
    `/interno/provedores?q=${encodeURIComponent(provedor.nome)}`,
  )
  const existente = provedores.find((item) => item.nome === provedor.nome)
  if (existente) {
    await alinharAliases(api, existente.id, provedor)
    return existente
  }

  const { dados: criado } = await api.chamar('POST', '/interno/provedores', provedor)
  console.log(`provedor ${provedor.nome} criado (id ${criado.id})`)
  return criado
}

/**
 * Alinha introdução e layout de origem da versão existente. Trocar o layout só é aceito pela API
 * enquanto a versão não tem campos — por isso esta etapa roda antes da carga dos campos.
 */
async function sincronizarVersao(api, versaoId, versaoLayoutId, introducao) {
  const { dados: versao } = await api.chamar('GET', `/interno/versoes/${versaoId}`)
  if (versao.introducao === introducao && versao.versaoLayoutId === versaoLayoutId) return

  await api.chamar(
    'PUT',
    `/interno/versoes/${versaoId}`,
    { rotulo: versao.rotulo, introducao, versaoLayoutId },
    String(versao.versaoConcorrencia),
  )
  console.log('versão alinhada: introdução e layout de origem')
}

export async function garantirVersao(api, provedorId, versaoLayoutId, { rotulo, introducao }) {
  const { dados } = await api.chamar('GET', `/interno/provedores/${provedorId}/versoes`)
  const existente = itensDe(dados).find((versao) => versao.rotulo === rotulo)
  if (existente) {
    await sincronizarVersao(api, existente.id, versaoLayoutId, introducao)
    return existente.id
  }

  const { dados: criada } = await api.chamar('POST', `/interno/provedores/${provedorId}/versoes`, {
    versaoLayoutId,
    rotulo,
    introducao,
  })
  console.log(`versão ${rotulo} criada (id ${criada.id})`)
  return criada.id
}

function avisoMudou(existente, desejado) {
  return (
    existente.severidade !== desejado.severidade ||
    existente.corpo !== desejado.corpo ||
    (desejado.vigenciaInicio !== undefined && existente.vigenciaInicio !== desejado.vigenciaInicio)
  )
}

/** O título é a identidade do aviso para a sincronização: título novo = aviso novo. */
export async function sincronizarAvisos(api, versaoId, avisos) {
  const { dados: versao } = await api.chamar('GET', `/interno/versoes/${versaoId}`)
  const existentes = new Map((versao.avisos ?? []).map((aviso) => [aviso.titulo, aviso]))
  const titulosDesejados = new Set(avisos.map((aviso) => aviso.titulo))
  const contagem = { criados: 0, atualizados: 0, removidos: 0 }

  for (const aviso of avisos) {
    const existente = existentes.get(aviso.titulo)
    if (!existente) {
      await api.chamar('POST', `/interno/versoes/${versaoId}/avisos`, {
        ...aviso,
        campoVersaoIds: [],
      })
      contagem.criados++
    } else if (avisoMudou(existente, aviso)) {
      await api.chamar(
        'PUT',
        `/interno/avisos/${existente.id}`,
        aviso,
        String(existente.versaoConcorrencia),
      )
      contagem.atualizados++
    }
  }

  for (const [titulo, existente] of existentes) {
    if (titulosDesejados.has(titulo)) continue
    await api.chamar(
      'DELETE',
      `/interno/avisos/${existente.id}`,
      undefined,
      String(existente.versaoConcorrencia),
    )
    contagem.removidos++
  }

  console.log(
    `avisos: ${contagem.criados} criados, ${contagem.atualizados} atualizados, ${contagem.removidos} removidos`,
  )
}

/**
 * Só considera os erros já vinculados a este provedor: código de erro não é único entre
 * provedores, e o mesmo código registrado para outro provedor não pode impedir a criação deste.
 * `idPorCaminho` liga o caminho completo do campo no XSD do InvoiCy ao id do campo canônico.
 *
 * `removerAusentes` só vale para quem é dono da lista de erros da versão: sem ele, a carga cria e
 * atualiza, mas nunca apaga um erro gravado por outra carga (e o slug apagado continua reservado).
 */
export async function sincronizarErros(
  api,
  provedorSlug,
  versaoId,
  erros,
  idPorCaminho,
  { removerAusentes = false } = {},
) {
  const existentes = await api.listarTudo(
    `/interno/erros?provedor=${encodeURIComponent(provedorSlug)}`,
  )
  const idPorCodigo = new Map(existentes.map((erro) => [erro.codigo, erro.id]))
  const codigosExistentes = new Set(idPorCodigo.keys())
  const codigosDesejados = new Set(erros.map((erro) => erro.codigo))

  const idsDosCampos = (erro) =>
    erro.camposRelacionados.map((caminho) => {
      const id = idPorCaminho.get(caminho)
      if (!id)
        throw new Error(`${erro.codigo} aponta para ${caminho}, que não está no vocabulário.`)
      return id
    })

  const novos = erros.filter((erro) => !codigosExistentes.has(erro.codigo))
  for (const [indice, erro] of novos.entries()) {
    await api.chamar('POST', '/interno/erros', {
      codigo: erro.codigo,
      origem: erro.origem,
      titulo: erro.titulo,
      causaProvavel: erro.causaProvavel,
      solucaoSugerida: erro.solucaoSugerida,
      variantes: erro.variantes,
      versoesProvedorIds: [versaoId],
      camposLayoutIds: idsDosCampos(erro),
    })
    if ((indice + 1) % 50 === 0) console.log(`  erros: ${indice + 1}/${novos.length}`)
  }

  let atualizados = 0
  const causasPreservadas = []
  for (const erro of erros.filter((desejado) => codigosExistentes.has(desejado.codigo))) {
    const { dados: detalhe } = await api.chamar(
      'GET',
      `/interno/erros/${idPorCodigo.get(erro.codigo)}`,
    )
    const { corpo, causaPreservada } = planejarAtualizacaoDeErro(detalhe, erro, idsDosCampos(erro))
    if (causaPreservada && detalhe.causaProvavel !== erro.causaProvavel)
      causasPreservadas.push(erro.codigo)
    if (!corpo) continue

    await api.chamar(
      'PUT',
      `/interno/erros/${detalhe.id}`,
      corpo,
      String(detalhe.versaoConcorrencia),
    )
    atualizados++
  }
  if (causasPreservadas.length)
    console.warn(`  causa editada na área interna, mantida: ${causasPreservadas.join(', ')}`)

  let removidos = 0
  const ausentes = removerAusentes
    ? existentes.filter((existente) => !codigosDesejados.has(existente.codigo))
    : []
  for (const erro of ausentes) {
    const { dados: detalhe } = await api.chamar('GET', `/interno/erros/${erro.id}`)
    const somenteDestaVersao =
      detalhe.versoesProvedorIds.length === 1 && detalhe.versoesProvedorIds[0] === versaoId

    // Erro que também vale para outra versão não é removido: tirar só este vínculo é decisão
    // editorial sobre o outro provedor, feita na área interna, não efeito de rodar a carga.
    if (!somenteDestaVersao) {
      console.warn(`  ${erro.codigo} também vale para outra versão — desvincule manualmente.`)
      continue
    }

    await api.chamar(
      'DELETE',
      `/interno/erros/${erro.id}`,
      undefined,
      String(detalhe.versaoConcorrencia),
    )
    removidos++
  }

  console.log(
    `erros: ${novos.length} criados, ${atualizados} atualizados, ${removidos} removidos, ${codigosExistentes.size - removidos} mantidos`,
  )
}
