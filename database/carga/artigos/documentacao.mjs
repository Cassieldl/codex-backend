/**
 * Artigos da base de conhecimento da Documentação (FAQ NFS-e), com o corpo copiado para
 * `documentacao/`. O título é o da primeira linha do arquivo. `erros` liga o artigo a erros
 * conhecidos pelo código: do provedor (`provedor`) ou do InvoiCy (`provedor: null`).
 */

const PASTA_DOS_ARTIGOS = '04-base-conhecimento/artigos'

const doEmissorNacional = (codigo) => ({ provedor: 'Emissor Nacional', codigo })
const doInvoicy = (codigo) => ({ provedor: null, codigo })

function artigo(nome, tipo, erros = []) {
  return { arquivo: `${nome}.md`, origem: `${PASTA_DOS_ARTIGOS}/${nome}.md`, tipo, erros }
}

export const ARTIGOS_DA_DOCUMENTACAO = [
  artigo('calculo-valor-liquido', 'Conceito'),
  artigo('cancelamento-sem-webservice', 'Procedimento', [
    { provedor: 'GESPAM', codigo: 'P0006' },
    { provedor: 'GESPAM', codigo: 'P0008' },
    { provedor: 'GESPAM', codigo: 'P0009' },
    doInvoicy('inv0320'),
    doInvoicy('inv0349'),
    doInvoicy('inv0350'),
  ]),
  artigo('checklist-producao-nfse', 'Procedimento', [doInvoicy('inv0303')]),
  artigo('configuracoes-de-emissao', 'Procedimento'),
  artigo('consultas-municipio', 'Procedimento', [
    doInvoicy('inv0305'),
    doInvoicy('inv0306'),
    doInvoicy('inv0328'),
  ]),
  artigo('controle-numeracao-rps', 'Procedimento'),
  artigo('credenciais-autenticacao-prefeitura', 'Procedimento', [
    { provedor: 'ISS.NET', codigo: 'E157' },
    doInvoicy('inv0326'),
  ]),
  artigo('e0037-producao-restrita', 'Procedimento', [doEmissorNacional('E0037')]),
  artigo('e1235-falha-esquema-xml', 'Procedimento', [doEmissorNacional('E1235')]),
  artigo('emissao-em-lote', 'Procedimento', [doInvoicy('inv0318')]),
  artigo('emissao-judicial', 'Procedimento'),
  artigo('emissao-sequencial-rps', 'Conceito', [
    doInvoicy('inv0337'),
    doInvoicy('inv0346'),
    doInvoicy('inv0354'),
  ]),
  artigo('erro-173-chave-de-comunicacao', 'Procedimento'),
  artigo('ibs-cbs-parametrizacao-prefeitura', 'Procedimento', [
    { provedor: 'Betha', codigo: 'E999' },
    { provedor: 'GESPAM', codigo: 'C0014' },
  ]),
  artigo('impressao-espelho', 'Procedimento', [doInvoicy('inv0331')]),
  artigo('migracao-padrao-nacional', 'Mudanca'),
  artigo('natureza-da-operacao', 'Conceito'),
  artigo('notas-recebidas', 'Procedimento'),
  artigo('nt007-retencoes-federais', 'Mudanca'),
  artigo('origem-da-rejeicao', 'Conceito'),
  artigo('processamento-sincrono-assincrono', 'Conceito', [
    doInvoicy('inv0318'),
    doInvoicy('inv0337'),
    doInvoicy('inv0346'),
    doInvoicy('inv0354'),
  ]),
  artigo('reforma-tributaria-visao-geral', 'Mudanca'),
  artigo('retencao-de-iss', 'Conceito'),
  artigo('solicitar-novo-municipio', 'Procedimento', [doInvoicy('inv0302')]),
  artigo('status-em-conflito', 'Procedimento', [doInvoicy('inv0304'), doInvoicy('inv0339')]),
  artigo('tomador-estrangeiro-padrao-nacional', 'Procedimento', [
    doEmissorNacional('E0330'),
    doEmissorNacional('E0333'),
  ]),
  artigo('webhook-nfse', 'Procedimento'),
  {
    arquivo: 'glossario.md',
    origem: '04-base-conhecimento/glossario.md',
    tipo: 'Conceito',
    erros: [],
  },
]

/** Arquivos da base de conhecimento que não são carregados, com o motivo. */
export const ARTIGOS_FORA = [
  {
    origem: `${PASTA_DOS_ARTIGOS}/retencoes-federais.md`,
    motivo: 'tem marcas de pendência (seção 7 de pendencias-revisao.md)',
  },
  {
    origem: '04-base-conhecimento/referencias.md',
    motivo: 'índice de fontes da Documentação, não é artigo da base',
  },
]

/** Lista de rejeição da Documentação que corresponde a um provedor do Codex. */
export const REJEICOES_DO_PROVEDOR = {
  '03-rejeicoes/betha.md': 'Betha',
  '03-rejeicoes/e-l.md': 'E&L',
  '03-rejeicoes/elotech.md': 'Elotech',
  '03-rejeicoes/gespam.md': 'GESPAM',
  '03-rejeicoes/giss.md': 'GISS',
  '03-rejeicoes/ipm.md': 'IPM',
  '03-rejeicoes/padrao-nacional.md': 'Emissor Nacional',
}

/**
 * Trechos da Documentação que a cópia troca por segurança, porque a pasta de origem não é do
 * repositório e copiar de novo traria o trecho de volta. Trecho que não aparece mais interrompe a
 * cópia: a origem mudou e a correção precisa ser revista.
 */
export const CORRECOES_DE_SEGURANCA = [
  {
    arquivo: 'erro-173-chave-de-comunicacao.md',
    motivo: 'calculadora MD5 de terceiros por HTTP levaria o leitor a colar a chave de acesso nela',
    trecho:
      'Também dá para conferir com uma calculadora MD5 externa, como <http://miraclesalad.com/>.',
    troca:
      'Para conferir fora do InvoiCy, calcule o MD5 localmente, na sua máquina. Nunca cole a chave de acesso em site de terceiros.',
  },
]

export function aplicarCorrecoesDeSeguranca(arquivo, texto) {
  return CORRECOES_DE_SEGURANCA.filter((correcao) => correcao.arquivo === arquivo).reduce(
    (corrigido, { trecho, troca }) => {
      if (!corrigido.includes(trecho)) {
        throw new Error(`O trecho a corrigir não está mais em ${arquivo}: revise a correção.`)
      }
      return corrigido.replaceAll(trecho, troca)
    },
    texto,
  )
}

/** Texto do link que era o nome do arquivo: vira o que o arquivo é. */
export const ROTULO_DO_ARQUIVO = {
  'invoicy-inv03xx.md': 'mensagens do InvoiCy (inv03xx)',
  'padrao-nacional.md': 'rejeições do Padrão Nacional',
  'ipm.md': 'rejeições do IPM',
}

/**
 * O que falta para publicar um artigo desta carga com a verificação informada. Artigo reservado
 * interrompe: publicado, ele não volta, e esta carga não é dona de nenhum.
 */
export function planejarPublicacao(artigo, verificadoEm) {
  if (artigo.reservado) {
    throw new Error(`"${artigo.titulo}" é reservado e não é publicado por esta carga.`)
  }
  return {
    registrarVerificacao: artigo.verificadoEm !== verificadoEm,
    publicar: !artigo.publicado,
  }
}
