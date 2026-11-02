/** Identificação do que esta carga cria e a publicação publica: uma fonte só para os dois scripts. */

export const PROVEDOR = {
  nome: 'Emissor Nacional',
  identificadorInvoicy: 'EMISSOR_NACIONAL',
  categoria: 'Nacional',
  aliases: ['ADN', 'Sefin Nacional', 'Padrão Nacional', 'NFS-e Nacional'],
}

export const ROTULO_VERSAO = '0.20'

/**
 * Artigos do provedor. Campos pelo caminho completo no XSD de entrada; erros pelo código. A emissão
 * judicial liga os erros do teto de alíquota (E0595, E1300): é por eles que o analista chega quando a
 * nota que devia ser judicial seguiu o fluxo normal.
 */
export const ARTIGOS = [
  {
    arquivo: 'artigo-emissao-judicial.md',
    tipo: 'Procedimento',
    titulo: 'Emitir NFS-e por decisão judicial no Emissor Nacional',
    campos: [
      'Envio/RPS/nNFSeJudicial',
      'Envio/RPS/NumProcesso',
      'Envio/RPS/tpSusp',
      'Envio/RPS/RPSNumero',
      'Envio/RPS/RPSSerie',
      'Envio/RPS/IDUnico',
      'Envio/RPS/Servico/Valores/ValAliqISS',
    ],
    erros: ['E0595', 'E1300'],
  },
]
