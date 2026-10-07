/** Regras puras da publicação do Emissor Nacional (testadas em publicacao.test.mjs). */

const DATA_ISO = /^\d{4}-\d{2}-\d{2}$/

/** Lê `--verificado-em AAAA-MM-DD`; qualquer outra forma interrompe antes de tocar a API. */
export function lerDataDeVerificacao(argumentos) {
  const posicao = argumentos.indexOf('--verificado-em')
  const valor = posicao >= 0 ? argumentos[posicao + 1] : undefined
  // A volta completa recusa dia que não existe: `Date` aceitaria 2026-02-30 e viraria 2 de março.
  const dataReal = valor && DATA_ISO.test(valor) ? new Date(`${valor}T00:00:00Z`) : null
  if (
    !dataReal ||
    Number.isNaN(dataReal.getTime()) ||
    dataReal.toISOString().slice(0, 10) !== valor
  ) {
    throw new Error('Informe a data da verificação: --verificado-em AAAA-MM-DD.')
  }
  return valor
}

/** Só o que ainda não está publicado: rodar de novo não escreve nada. */
export function pendentesDePublicacao(itens) {
  return itens.filter((item) => !item.publicado)
}
