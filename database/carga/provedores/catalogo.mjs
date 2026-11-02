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

const informativo = (titulo) => ({ severidade: 'Informativo', titulo })
const alerta = (titulo) => ({ severidade: 'Alerta', titulo })

const INTRODUCAO_DPS =
  'DPS no leiaute nacional v1.01. Tipo, tamanho e obrigatoriedade das tags vêm do XSD nacional.'

/**
 * Provedor de leiaute nacional cuja configuração no InvoiCy segue a DPS v1.01. `envelope` é o grupo
 * que embrulha a DPS na mensagem do provedor, fora do XSD nacional.
 *
 * Os avisos vêm de `material/<id>/observacoes.md`, a seção do provedor nas observações de emissão,
 * copiada com o travessão trocado por hífen e sem o emoji da marca de pendência (a palavra
 * "Pendente" continua lá e tira o trecho do aviso). Cada subseção vira um aviso com o título e a
 * severidade decididos aqui.
 */
function provedorDeLeiauteNacional(
  id,
  { provedor, introducao, envelope, esquema, subsecoes, avisos = [] },
) {
  return {
    provedor: { categoria: 'Proprio', aliases: [], ...provedor },
    versao: { rotulo: '1.01', introducao: introducao ?? INTRODUCAO_DPS },
    familia: 'dps',
    mapeamento: `${id}/mapeamento.md`,
    esquema: esquema ?? {
      arquivos: ESQUEMA_DPS_NACIONAL,
      raiz: 'DPS',
      ...(envelope ? { prefixo: `${envelope}/` } : {}),
    },
    ...(envelope ? { prefixoDestino: `${envelope}/` } : {}),
    observacoes: { arquivo: `${id}/observacoes.md`, subsecoes },
    avisos,
  }
}

const PROVEDORES_DE_LEIAUTE_NACIONAL = {
  betha: provedorDeLeiauteNacional('betha', {
    provedor: { nome: 'Betha', identificadorInvoicy: 'BETHA' },
    subsecoes: {
      'Visão geral': informativo('O Betha atual usa o leiaute nacional (DPS), não o ABRASF'),
      'Autenticação e acesso': alerta(
        'Inscrição Municipal, tipo de emissão e faixa de RPS antes da primeira emissão',
      ),
      'Ambiente de homologação e produção': informativo(
        'Homologação e produção usam a mesma URL: o ambiente é escolhido no portal Betha',
      ),
      Processamento: alerta('Lote assíncrono, sem reenvio de RPS rejeitado'),
      Cancelamento: alerta('Maracás/BA não cancela pelo webservice'),
      'Regras de preenchimento': alerta(
        'Campos exigidos, discriminação e tomador do exterior no Betha',
      ),
      'Reforma Tributária (IBS/CBS)': alerta(
        'IBS/CBS depende de parametrização da prefeitura (E999)',
      ),
      Links: informativo('Exemplo de XML do Betha 2.0 (legado ABRASF)'),
    },
  }),
  coplan: provedorDeLeiauteNacional('coplan', {
    provedor: {
      nome: 'Coplan',
      identificadorInvoicy: 'COPLAN',
      aliases: ['COPLAN - Modelo Nacional'],
    },
    introducao:
      'DPS no leiaute nacional v1.01, dentro do envelope GerarNfseEnvio. Tipo, tamanho e ' +
      'obrigatoriedade das tags da DPS vêm do XSD nacional; o envelope não está descrito nele.',
    envelope: 'GerarNfseEnvio',
    avisos: [
      {
        // O E138 já existe como erro do GISS: o slug do erro nasce de código e título, então a
        // solução do Coplan vive aqui, como a do E0037 no E&L.
        severidade: 'Alerta',
        titulo: 'E138: usuário não autorizado a emitir pelo webservice em produção',
        corpo:
          'O Coplan devolve o E138 ("Usuário não autorizado a realizar o serviço") quando o ' +
          'prestador ainda não tem permissão para emitir via webservice em produção.\n\n' +
          'Peça a liberação a equipetributario@coplanmt.com.br, com os dados do prestador. Em ' +
          'Sinop/MT, a liberação é feita pela prefeitura.',
      },
    ],
    subsecoes: {
      'Visão geral': informativo('O Coplan segue as regras do Emissor Nacional'),
      'Autenticação e acesso': { ignorar: 'Vira o aviso do E138, escrito no catálogo.' },
      'Regras de preenchimento': alerta(
        'Retenções federais, intermediário e alíquota de ISS no Coplan',
      ),
    },
  }),
  dsfnet: provedorDeLeiauteNacional('dsfnet', {
    provedor: { nome: 'DSFNET', identificadorInvoicy: 'DSFNET', aliases: ['DFSNET'] },
    subsecoes: {
      'Autenticação e acesso': alerta('Certificado A1 e subusuário ativo na prefeitura'),
      Processamento: alerta('Emissão sequencial: uma rejeição trava as notas seguintes'),
      Cancelamento: informativo('RPS cancelado não reaproveita o número'),
      'Regras de preenchimento': alerta(
        'Série entre 10001 e 49999 em produção e outras regras do DSFNET',
      ),
    },
  }),
  dueto: provedorDeLeiauteNacional('dueto', {
    provedor: { nome: 'Dueto', identificadorInvoicy: 'DUETO' },
    subsecoes: {
      'Ambiente de homologação': informativo('Homologação varia por município'),
      Cancelamento: alerta('Cândido Mota/SP e Catanduva/SP não cancelam pelo webservice'),
      'Regras de preenchimento': alerta(
        'Sem quebra de linha; alíquota e responsável pela retenção só nos casos previstos',
      ),
      Links: informativo('Exemplo de XML do Dueto 2.0'),
    },
  }),
  fiorilli: provedorDeLeiauteNacional('fiorilli', {
    provedor: {
      nome: 'Fiorilli',
      identificadorInvoicy: 'FIORILLI',
      aliases: ['ISSWEB Fiorilli', 'Fiorilli - Modelo Nacional'],
    },
    introducao:
      'DPS no leiaute nacional v1.01, dentro do envelope RecepcionarDpsEnvio. Tipo, tamanho e ' +
      'obrigatoriedade das tags da DPS vêm do XSD nacional; o envelope não está descrito nele.',
    envelope: 'RecepcionarDpsEnvio',
    subsecoes: {
      'Autenticação e acesso': informativo('Usuário e senha do portal nos dados da empresa'),
      'Ambiente de homologação': alerta('Sem homologação: os testes são feitos em produção'),
      Processamento: informativo('Cada prefeitura usa uma versão diferente do webservice'),
      Disponibilidade: informativo('O endereço do serviço é um IP que muda com frequência'),
      'Regras de preenchimento': alerta('Tomador obrigatório e data não anterior à última NFS-e'),
    },
  }),
  gespam: provedorDeLeiauteNacional('gespam', {
    provedor: { nome: 'GESPAM', identificadorInvoicy: 'GESPAM' },
    subsecoes: {
      'Autenticação e acesso': alerta('ApiKey por ambiente: uma chave nova invalida a anterior'),
      Processamento: informativo('Recepção síncrona e processamento assíncrono'),
      'Limites (produção)': alerta(
        'Limites de produção: 100 requisições por minuto, 10 MB e 50 DPS por lote',
      ),
      'Regras de preenchimento': alerta(
        'Série só numérica e ApiKey de contribuinte restrita ao próprio CNPJ',
      ),
    },
  }),
  'joao-pessoa': provedorDeLeiauteNacional('joao-pessoa', {
    provedor: { nome: 'João Pessoa', identificadorInvoicy: 'JOAOPESSOA', aliases: ['PMJP'] },
    // A seção não tem subseções: o texto vem logo depois da identificação.
    subsecoes: {
      '': informativo('João Pessoa segue as regras do Emissor Nacional'),
    },
  }),
  rlz: provedorDeLeiauteNacional('rlz', {
    provedor: { nome: 'RLZ', identificadorInvoicy: 'RLZ', aliases: ['RLZ - Modelo Nacional'] },
    subsecoes: {
      'Autenticação e acesso': informativo('Certificado A1, usuário e senha e Token Web Service'),
      'Ambiente de homologação': alerta(
        'Homologação obrigatória antes da produção, criada sob demanda',
      ),
      Processamento: alerta('Emissão em tempo real, com risco de status em conflito'),
      Cancelamento: alerta('Alguns municípios não têm API de cancelamento'),
      'Regras de preenchimento': alerta('Local de prestação, tomador e intermediário na RLZ'),
      'Reforma Tributária (IBS/CBS)': informativo(
        'Municípios que já recebem os campos da Reforma Tributária',
      ),
    },
  }),
  sigcorp: provedorDeLeiauteNacional('sigcorp', {
    provedor: { nome: 'Sigcorp', identificadorInvoicy: 'SIGCORP' },
    subsecoes: {
      'Autenticação e acesso': alerta(
        'HTTP Basic com IM e senha, mais o CNPJ do prestador no cabeçalho',
      ),
      Processamento: informativo('Status do processamento assíncrono'),
      'Regras de preenchimento': alerta('Validações próprias do Sigcorp além do padrão nacional'),
      'Reforma Tributária (IBS/CBS)': alerta('O que o Sigcorp rejeita no grupo IBSCBS'),
    },
  }),
  smarapdisil: provedorDeLeiauteNacional('smarapdisil', {
    provedor: {
      nome: 'SMARAPDISIL',
      identificadorInvoicy: 'SMARAPDISIL',
      aliases: ['SMARAPD SIL', 'SMARAPDSil'],
    },
    introducao:
      'XML da NFS-e no leiaute nacional v1.01: o grupo NFSe, com a DPS dentro de infNFSe, como a ' +
      'configuração do InvoiCy monta para a SMARAPDISIL. Tipo, tamanho e obrigatoriedade vêm dos ' +
      'tipos do XSD nacional.',
    // A configuração parte da NFS-e, não da DPS: a raiz é o NFSe (tipo TCNFSe do mesmo XSD).
    esquema: { arquivos: ESQUEMA_DPS_NACIONAL, raiz: 'NFSe', tipoDaRaiz: 'TCNFSe' },
    subsecoes: {
      'Origem da rejeição': informativo('Rejeição do Emissor Nacional ou da prefeitura'),
      'Versões anteriores (WS e WS2)': { ignorar: 'Fala de outras versões do provedor.' },
      'Artigos relacionados': {
        ignorar: 'Só lista artigos da base de conhecimento, que têm carga própria.',
      },
      'Regras de preenchimento': alerta('Regras de preenchimento da SMARAPDISIL'),
    },
  }),
}

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
  ...PROVEDORES_DE_LEIAUTE_NACIONAL,
}
