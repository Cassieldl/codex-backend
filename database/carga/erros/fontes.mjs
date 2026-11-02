/**
 * De onde vem cada erro conhecido carregado a partir da Documentação. Cada tabela que a carga lê
 * declara as colunas; cada tabela que fica de fora declara o motivo. Tabela sem declaração
 * interrompe a carga, para que conteúdo novo na fonte nunca entre nem saia sem decisão.
 *
 * Colunas: `codigo`, `mensagem` (vira variante), `causa`, `solucao` e, na tabela que mistura
 * provedores, `provedor`. `codigoNoInicio`: a coluna do código traz "CÓDIGO - texto"; o texto não é
 * a mensagem literal, então só vira variante quando o erro é criado e não há outra.
 * `camposInvoicy`: a fonte cita, entre crases, nomes de campo do Layout InvoiCy. `conferirCom`: a
 * linha só entra se o código não estiver na tabela indicada ou estiver lá com a mesma mensagem.
 */

const CODIGO_NACIONAL = /^E\d{4}$/

export const FONTES = [
  {
    arquivo: 'invoicy-inv03xx.md',
    origemNaDocumentacao: '03-rejeicoes/invoicy-inv03xx.md',
    origem: 'InvoiCy',
    provedor: null,
    tabelas: [
      {
        secao: /^Rejeições e mensagens do InvoiCy/,
        colunas: { codigo: 'Código', mensagem: 'Mensagem', causa: 'Causa', solucao: 'Ação' },
      },
    ],
    fora: [
      {
        secao: /^Outros códigos de retorno/,
        motivo:
          'fora do recorte dos 48 códigos inv03xx: códigos numéricos de retorno do Web Service, ' +
          'várias mensagens cortadas com reticências na fonte e sem causa nem ação',
      },
    ],
  },
  {
    arquivo: 'padrao-nacional.md',
    origemNaDocumentacao: '03-rejeicoes/padrao-nacional.md',
    origem: 'PorCodigo',
    provedor: 'Emissor Nacional',
    camposInvoicy: true,
    tabelas: [
      {
        secao:
          /^(Configuração e cadastro|Identificação da DPS|Serviço e tributação|Simples Nacional)/,
        colunas: { codigo: 'Mensagem', causa: 'Causa', solucao: 'Como resolver' },
        codigoNoInicio: true,
        codigoValido: CODIGO_NACIONAL,
      },
      {
        secao: /^Tomador estrangeiro/,
        colunas: { codigo: 'Código / mensagem', causa: 'Causa', solucao: 'Como resolver' },
        codigoNoInicio: true,
        codigoValido: CODIGO_NACIONAL,
      },
    ],
    fora: [],
  },
  {
    arquivo: 'giss.md',
    origemNaDocumentacao: '03-rejeicoes/giss.md',
    origem: 'Prefeitura',
    provedor: 'GISS',
    tabelas: [
      {
        secao: /^Erros mais comuns/,
        colunas: {
          codigo: 'Código',
          mensagem: 'Mensagem',
          causa: 'Causa provável',
          solucao: 'Como resolver',
        },
        // Vem das observações internas; a lista completa é a planilha do provedor.
        conferirCom: /^Lista completa/,
      },
      {
        secao: /^Lista completa/,
        colunas: { codigo: 'Código', mensagem: 'Mensagem', solucao: 'Solução' },
      },
    ],
    fora: [],
  },
  {
    arquivo: 'elotech.md',
    origemNaDocumentacao: '03-rejeicoes/elotech.md',
    origem: 'Prefeitura',
    provedor: 'Elotech',
    tabelas: [
      {
        secao: /^Lista de erros e alertas/,
        colunas: { codigo: 'Código', mensagem: 'Mensagem', solucao: 'Motivo / Solução' },
      },
    ],
    fora: [],
  },
  {
    arquivo: 'betha.md',
    origemNaDocumentacao: '03-rejeicoes/betha.md',
    origem: 'Prefeitura',
    provedor: 'Betha',
    tabelas: [
      {
        secao: /^Erros com causa e solução/,
        colunas: {
          codigo: 'Código',
          mensagem: 'Mensagem',
          causa: 'Causa provável',
          solucao: 'Como resolver',
        },
      },
      {
        secao: /^Erros da DPS/,
        colunas: { codigo: 'Código', mensagem: 'Mensagem', solucao: 'Orientação' },
      },
    ],
    fora: [],
  },
  {
    arquivo: 'gespam.md',
    origemNaDocumentacao: '03-rejeicoes/gespam.md',
    origem: 'Prefeitura',
    provedor: 'GESPAM',
    tabelas: [
      {
        secao: /^(Sistema e requisição|Contribuinte|Prefeitura|Lote) \(/,
        colunas: { codigo: 'Código', mensagem: 'Descrição' },
      },
      {
        secao: /^Problemas comuns/,
        colunas: { codigo: 'Sintoma', solucao: 'Causa e solução' },
        codigoNoInicio: true,
        codigoValido: /^[SCPL]\d{4}$/,
      },
    ],
    fora: [],
  },
  {
    arquivo: 'rejeicoes-indice.md',
    origemNaDocumentacao: '03-rejeicoes/README.md',
    origem: 'Prefeitura',
    provedor: 'PorLinha',
    camposInvoicy: true,
    tabelas: [
      {
        secao: /^Erros citados nas observações/,
        colunas: {
          provedor: 'Provedor',
          codigo: 'Código',
          mensagem: 'Mensagem',
          solucao: 'Solução',
        },
      },
    ],
    fora: [
      { secao: /^Gerais$/, motivo: 'índice de arquivos, sem erro' },
      { secao: /^Por provedor$/, motivo: 'índice de arquivos, sem erro' },
    ],
  },
]

/**
 * Nome do provedor na tabela de erros citados da Documentação e no Codex. Provedor fora deste mapa
 * não está no Codex, e a linha dele fica de fora.
 */
export const PROVEDOR_NO_CODEX = {
  Betha: 'Betha',
  Coplan: 'Coplan',
  DSFNET: 'DSFNET',
  ISSNET: 'ISS.NET',
  Sigcorp: 'Sigcorp',
}
