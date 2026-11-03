/**
 * Sincroniza um artigo da Documentação com o que está salvo no Codex, pela API interna. Artigo
 * publicado só é atualizado com `atualizarPublicados`; a atualização troca corpo, tipo e
 * vínculos e não mexe na publicação nem na data de verificação.
 */

import { chaveDeSlug } from '../emissor-nacional/transformar-layout.mjs'

function mesmoConjunto(a, b) {
  const ordenado = (ids) => [...ids].sort((x, y) => x - y).join(',')
  return ordenado(a) === ordenado(b)
}

export async function sincronizarArtigoDaDocumentacao(
  api,
  desejado,
  existentePorTitulo,
  slugsOcupados,
  { atualizarPublicados = false } = {},
) {
  const existente = existentePorTitulo.get(desejado.titulo)
  if (!existente) {
    const dono = slugsOcupados.get(chaveDeSlug(desejado.titulo))
    if (dono) return { situacao: 'colisão', detalhe: `slug já é do artigo "${dono}"` }
    await api.chamar('POST', '/interno/artigos', desejado)
    return { situacao: 'criado' }
  }

  const { dados: atual } = await api.chamar('GET', `/interno/artigos/${existente.id}`)
  const mudou =
    atual.tipo !== desejado.tipo ||
    atual.corpo !== desejado.corpo ||
    !mesmoConjunto(atual.campoLayoutIds, desejado.campoLayoutIds) ||
    !mesmoConjunto(atual.erroIds, desejado.erroIds)
  if (!mudou) return { situacao: 'sem mudança' }
  if (atual.publicado && !atualizarPublicados) {
    return { situacao: 'só relatório', detalhe: 'artigo já publicado' }
  }

  await api.chamar(
    'PUT',
    `/interno/artigos/${atual.id}`,
    desejado,
    String(atual.versaoConcorrencia),
  )
  return atual.publicado
    ? { situacao: 'atualizado', detalhe: 'publicado, continua publicado' }
    : { situacao: 'atualizado' }
}
