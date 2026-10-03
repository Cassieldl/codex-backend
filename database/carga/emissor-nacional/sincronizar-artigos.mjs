/**
 * Etapa de artigos da carga. O corpo vive num .md versionado ao lado deste arquivo; campos e erros
 * relacionados são declarados pelo caminho no XML de entrada do InvoiCy e pelo código do erro —
 * identificadores estáveis — e só viram ids na hora de falar com a API.
 */

/**
 * Traduz as referências de um artigo para ids. Referência que não existe interrompe a carga: um
 * artigo criado sem o vínculo some da página do campo e do erro sem nenhum aviso.
 */
export function resolverReferencias(artigo, idPorCaminho, idErroPorCodigo) {
  const campoLayoutIds = artigo.campos.map((caminho) => {
    const id = idPorCaminho.get(caminho)
    if (!id)
      throw new Error(
        `O artigo "${artigo.titulo}" cita o campo ${caminho}, que não está no vocabulário.`,
      )
    return id
  })
  const erroIds = artigo.erros.map((codigo) => {
    const id = idErroPorCodigo.get(codigo)
    if (!id)
      throw new Error(
        `O artigo "${artigo.titulo}" cita o erro ${codigo}, que não está no provedor.`,
      )
    return id
  })
  return { campoLayoutIds, erroIds }
}

function mesmoConjunto(a, b) {
  const ordenado = (ids) => [...ids].sort((x, y) => x - y).join(',')
  return ordenado(a) === ordenado(b)
}

/** Cria o artigo pelo título, ou atualiza corpo, tipo e vínculos quando o que está salvo difere. */
export async function sincronizarArtigo(api, artigo, corpo, referencias) {
  const existentes = await api.listarTudo('/interno/artigos')
  const existente = existentes.find((item) => item.titulo === artigo.titulo)
  const desejado = { tipo: artigo.tipo, titulo: artigo.titulo, corpo, ...referencias }

  if (!existente) {
    await api.chamar('POST', '/interno/artigos', desejado)
    console.log(`artigo criado: ${artigo.titulo}`)
    return
  }

  const { dados: atual } = await api.chamar('GET', `/interno/artigos/${existente.id}`)
  const mudou =
    atual.tipo !== artigo.tipo ||
    atual.corpo !== corpo ||
    !mesmoConjunto(atual.campoLayoutIds, referencias.campoLayoutIds) ||
    !mesmoConjunto(atual.erroIds, referencias.erroIds)
  if (!mudou) {
    console.log(`artigo sem mudança: ${artigo.titulo}`)
    return
  }

  await api.chamar(
    'PUT',
    `/interno/artigos/${existente.id}`,
    desejado,
    String(atual.versaoConcorrencia),
  )
  console.log(`artigo atualizado: ${artigo.titulo}`)
}
