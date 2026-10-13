/**
 * Identificação de cada provedor que esta carga cria, e de onde vem o comportamento dos campos. Uma
 * fonte só para a importação e a publicação. O material citado fica em `material/<id>/`.
 *
 * `fonteDeExemplo` (id em `exemplos-xml.mjs`) diz que a carga é dona do exemplo dos campos do
 * provedor: grava o valor da fonte ou `null`. Provedor sem a chave não tem fonte, e a carga nunca
 * decide o exemplo dele.
 *
 * O rótulo da versão é a versão do leiaute do provedor, porque o mapeamento exportado não traz o
 * rótulo que o InvoiCy usa; e o identificador InvoiCy é provisório. Os dois se ajustam na área
 * interna quando o InvoiCy confirmar.
 */

const ESQUEMA_DPS_NACIONAL = [
  'dps-nacional-1.01/DPS_v1.01.xsd',
  'dps-nacional-1.01/tiposComplexos_v1.01.xsd',
  'dps-nacional-1.01/tiposSimples_v1.01.xsd',
]

export const PROVEDORES = {
  'e-l': {
    provedor: {
      nome: 'E&L',
      identificadorInvoicy: 'EL',
      categoria: 'Proprio',
      aliases: ['E e L', 'EL', 'E&L Nacional'],
    },
    versao: {
      rotulo: '1.01',
      introducao:
        'DPS no leiaute nacional v1.01, enviada à API de Integração NFS-e Nacional da E&L. O ' +
        'município processa a DPS, gera a NFS-e municipal e a transmite ao Ambiente Nacional.',
    },
    familia: 'dps',
    fonteDeExemplo: 'dps-nacional',
    mapeamento: 'e-l/mapeamento.md',
    esquema: { arquivos: ESQUEMA_DPS_NACIONAL, raiz: 'DPS' },
    avisos: [
      {
        // O E0037 já existe como erro do Emissor Nacional, com a mesma mensagem: o slug do erro
        // nasce de código e título, então o detalhe que a E&L documenta vive aqui.
        severidade: 'Alerta',
        titulo: 'E0037 em homologação: o município não configurou a Produção Restrita',
        corpo:
          'A API da E&L devolve os erros da Plataforma Nacional. O E0037 diz que o código do ' +
          'município emissor é inexistente no cadastro de convênio, mas, segundo a E&L, na prática ' +
          'ele aparece quando o município ainda não configurou a Produção Restrita: replicar as ' +
          'configurações de produção, habilitar o módulo de Produção Restrita e concluir o ' +
          'convênio exigido pelo ambiente nacional.\n\n' +
          'O contribuinte deve pedir à prefeitura essa habilitação; depois dela, a DPS pode ser ' +
          'enviada de novo.',
      },
      {
        severidade: 'Informativo',
        titulo: 'O envio é autenticado por um token do contribuinte',
        corpo:
          'A API da E&L recebe a DPS assinada, compactada em GZip e codificada em Base64, com um ' +
          'token de integração gerado no portal do município. O token vale só para as DPS do ' +
          'contribuinte que o gerou, pode ser inativado a qualquer momento e é recusado depois de ' +
          'expirar.',
      },
    ],
  },
  elotech: {
    provedor: {
      nome: 'Elotech',
      identificadorInvoicy: 'ELOTECH',
      categoria: 'Proprio',
      aliases: ['Oxy', 'Aise'],
    },
    versao: {
      rotulo: '2.03',
      introducao:
        'Lote de RPS síncrono (EnviarLoteRpsSincronoEnvio) no padrão ABRASF 2.03 da Elotech, com o ' +
        'grupo InfIbsCbs da Reforma Tributária (nota técnica de atualização de campos, v1.1, ' +
        '31/12/2025).',
    },
    familia: 'abrasf',
    fonteDeExemplo: 'elotech',
    mapeamento: 'elotech/mapeamento.md',
    esquema: { arquivos: ['elotech/nfse_v2_03.xsd'], raiz: 'EnviarLoteRpsSincronoEnvio' },
    prefixoDestino: 'EnviarLoteRpsSincronoEnvio/LoteRps/ListaRps/Rps/',
    avisos: [
      {
        severidade: 'Alerta',
        titulo: 'Município que adotou o Emissor Nacional bloqueia a emissão na Elotech',
        corpo:
          'A Elotech tem o parâmetro "Utiliza Emissor Nacional", com uma data de início do ' +
          'bloqueio. A partir dessa data, o sistema da Elotech não emite mais NFS-e nem RPS para o ' +
          'município e mostra a mensagem configurada pela prefeitura: a emissão passa a seguir o ' +
          'padrão nacional.',
      },
    ],
  },
  giss: {
    provedor: {
      nome: 'GISS',
      identificadorInvoicy: 'GISS',
      categoria: 'Proprio',
      aliases: ['GISS Online', 'GISSOnline'],
    },
    versao: {
      rotulo: '2.04',
      introducao:
        'Lote de RPS (EnviarLoteRpsEnvio) no padrão ABRASF 2.04 do GISS, conforme o Manual ' +
        'Técnico de Serviços Prestados v1.7.',
    },
    familia: 'abrasf',
    mapeamento: 'giss/mapeamento.md',
    esquema: { arquivos: ['giss/nfse_v2-04.xsd'], raiz: 'EnviarLoteRpsEnvio' },
    prefixoDestino: 'EnviarLoteRpsEnvio/LoteRps/ListaRps/Rps/',
    avisos: [],
  },
  ipm: {
    provedor: {
      nome: 'IPM',
      identificadorInvoicy: 'IPM',
      categoria: 'Proprio',
      aliases: ['Atende.Net', 'IPM Sistemas'],
    },
    versao: {
      rotulo: 'NTE 122/2025',
      introducao:
        'XML próprio do IPM (Atende.Net) para emissão de NFS-e com IBS e CBS, conforme a Nota ' +
        'Técnica NTE 122/2025 v1.7 (12/08/2026). Tipo e tamanho vêm da tabela do item 5.1 da ' +
        'nota; o que ela não descreve aparece como não descrito.',
    },
    familia: 'proprio',
    mapeamento: 'ipm/mapeamento.md',
    leiauteTranscrito: 'ipm/layout.json',
    erros: 'ipm/erros.json',
    avisos: [
      {
        severidade: 'Informativo',
        titulo: 'Envio de teste pela tag nfse_teste',
        corpo:
          'Com <nfse_teste>1</nfse_teste> logo depois de <nfse>, o IPM faz todas as validações sem ' +
          'emitir a nota e, se o XML estiver correto, responde "NFS-e válida para emissão". Com o ' +
          'valor 0, ou sem a tag, a NFS-e é emitida normalmente.',
      },
    ],
  },
  issnet: {
    provedor: {
      nome: 'ISS.NET',
      identificadorInvoicy: 'ISSNET',
      categoria: 'Proprio',
      aliases: ['ISSNet', 'ISS Net', 'Nota Control'],
    },
    versao: {
      rotulo: '1.01',
      introducao:
        'Lote de DPS síncrono (EnviarLoteDpsSincronoEnvio) do ISS.NET, com a DPS no leiaute ' +
        'nacional v1.01 — Manual de Integração v1.01, revisado em 03/08/2026. Tipo e tamanho das ' +
        'tags da DPS vêm do XSD nacional; as do envelope do lote não estão descritas nele.',
    },
    familia: 'dps',
    mapeamento: 'issnet/mapeamento.md',
    esquema: {
      arquivos: ESQUEMA_DPS_NACIONAL,
      raiz: 'DPS',
      prefixo: 'EnviarLoteDpsSincronoEnvio/LoteDps/ListaDps/',
    },
    prefixoDestino: 'EnviarLoteDpsSincronoEnvio/LoteDps/ListaDps/',
    avisos: [],
  },
}
