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
 *
 * `naturezaOperacao` é a seção do artigo de natureza da operação com o de-para da versão.
 */

import { TABELAS_NATUREZA } from '../naturezas-operacao.mjs'

const ESQUEMA_DPS_NACIONAL = [
  'dps-nacional-1.01/DPS_v1.01.xsd',
  'dps-nacional-1.01/tiposComplexos_v1.01.xsd',
  'dps-nacional-1.01/tiposSimples_v1.01.xsd',
]

const informativo = (titulo) => ({ severidade: 'Informativo', titulo })
const alerta = (titulo) => ({ severidade: 'Alerta', titulo })

// Prints da tela do InvoiCy onde as credenciais são informadas, repetidos nos avisos de credencial.
const PASTA_DA_TELA_DE_EMISSAO = '/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades'
const PRINT_USUARIO_E_SENHA =
  '![Tela Configurações para a emissão de NFS-e com os campos Usuário de autenticação e Senha de ' +
  `autenticação destacados no bloco Gerais](${PASTA_DA_TELA_DE_EMISSAO}/usuario-e-senha-de-autenticacao.png ` +
  '"Usuário e senha de autenticação no InvoiCy")'
const PRINT_CHAVE_PRIMARIA =
  '![Tela Configurações para a emissão de NFS-e com o campo Chave primária de autenticação ' +
  `destacado](${PASTA_DA_TELA_DE_EMISSAO}/chave-primaria-de-autenticacao.png ` +
  '"Chave primária de autenticação no InvoiCy")'
const ONDE_FICAM_AS_CREDENCIAIS =
  'Painel de controle > NFS-e > Configurações para emissão de NFS-e, bloco Gerais'

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
    provedor: { categoria: 'Nacional', aliases: [], ...provedor },
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
    naturezaOperacao: TABELAS_NATUREZA.nacional,
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
    avisos: [
      {
        severidade: 'Informativo',
        titulo: 'Usuário e senha do portal nos dados da empresa',
        corpo:
          'Informe o usuário e a senha do portal da prefeitura em Usuário de autenticação e Senha ' +
          `de autenticação, em ${ONDE_FICAM_AS_CREDENCIAIS}.\n\n` +
          PRINT_USUARIO_E_SENHA,
      },
    ],
    subsecoes: {
      'Autenticação e acesso': { ignorar: 'Vira o aviso de credencial, escrito no catálogo.' },
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
    avisos: [
      {
        severidade: 'Informativo',
        titulo: 'Certificado A1, usuário e senha e Token Web Service',
        corpo:
          'Vale para RLZ (cidadão online), RLZ - Modelo Nacional, RLZ ABRASF e RLZ ABRASF ' +
          'Assíncrono. A RLZ exige três credenciais:\n' +
          '- Certificado digital A1.\n' +
          `- Usuário e senha de autenticação, em ${ONDE_FICAM_AS_CREDENCIAIS}.\n` +
          '- Token Web Service, gerado no menu Token Web Service do sistema online do município e ' +
          'informado em Chave primária de autenticação.\n\n' +
          '![Sistema online do município no padrão RLZ com o menu Token Web Service](/imagens/rlz/' +
          'token-web-service-rlz.jpg "Token Web Service no sistema da RLZ")\n\n' +
          PRINT_CHAVE_PRIMARIA,
      },
    ],
    subsecoes: {
      'Autenticação e acesso': { ignorar: 'Vira o aviso de credencial, escrito no catálogo.' },
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
      categoria: 'Nacional',
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
    naturezaOperacao: TABELAS_NATUREZA.nacional,
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
        severidade: 'Alerta',
        titulo: 'O envio é autenticado por um token do contribuinte',
        corpo:
          'A API da E&L recebe a DPS assinada, compactada em GZip e codificada em Base64, com um ' +
          'token de integração gerado no portal do município. O token vale só para as DPS do ' +
          'contribuinte que o gerou, pode ser inativado a qualquer momento e é recusado depois de ' +
          'expirar.\n\n' +
          'Antes da primeira emissão:\n' +
          '- No portal da prefeitura, depois do login, abra Configurações > Integração e clique ' +
          'em Gerar Token. O token tem o formato abcdef12-3456-7890-abcd-ef1234567890.\n' +
          `- No InvoiCy, cole o token em Chave primária de autenticação, em ${ONDE_FICAM_AS_CREDENCIAIS}.\n\n` +
          '![Portal da prefeitura no padrão E&L com o menu Configurações, a opção Integração e o ' +
          'botão Gerar Token destacados](/imagens/e-l/gerar-token-de-integracao-e-l.jpg "Gerar ' +
          'token no portal E&L")\n\n' +
          PRINT_CHAVE_PRIMARIA,
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
    naturezaOperacao: TABELAS_NATUREZA.elotech,
    esquema: { arquivos: ['elotech/nfse_v2_03.xsd'], raiz: 'EnviarLoteRpsSincronoEnvio' },
    prefixoDestino: 'EnviarLoteRpsSincronoEnvio/LoteRps/ListaRps/Rps/',
    avisos: [
      {
        severidade: 'Alerta',
        titulo: 'Antes da primeira emissão: usuário e senha do webservice',
        corpo:
          'A Elotech autentica com o usuário e a senha do webservice da empresa, gerados no portal ' +
          'da prefeitura, e assina o envio com o certificado digital A1.\n\n' +
          '1. No portal da prefeitura, abra Informações > Informações da Empresa e clique no ' +
          'cadeado, no canto inferior direito, para gerar a senha.\n' +
          '2. No InvoiCy, preencha Usuário de autenticação com o CPF do responsável e Senha de ' +
          `autenticação com a senha gerada, em ${ONDE_FICAM_AS_CREDENCIAIS}.\n\n` +
          '![Portal da prefeitura no padrão Elotech com o menu Informações da Empresa e o botão de ' +
          'cadeado destacados](/imagens/elotech/senha-do-webservice-elotech.png "Senha do ' +
          'webservice no portal Elotech")\n\n' +
          PRINT_USUARIO_E_SENHA,
      },
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
    naturezaOperacao: TABELAS_NATUREZA.giss,
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
    naturezaOperacao: TABELAS_NATUREZA.ipm,
    leiauteTranscrito: 'ipm/layout.json',
    erros: 'ipm/erros.json',
    avisos: [
      {
        severidade: 'Alerta',
        titulo: 'Antes da primeira emissão: usuário e senha do portal e liberação do webservice',
        corpo:
          'O IPM tem duas obrigatoriedades para emitir via webservice.\n\n' +
          'No InvoiCy, preencha Usuário de autenticação com o CNPJ/CPF sem máscara e Senha de ' +
          `autenticação com a senha do portal da prefeitura, em ${ONDE_FICAM_AS_CREDENCIAIS}.\n\n` +
          `${PRINT_USUARIO_E_SENHA}\n\n` +
          'No Portal de Autoatendimento do município, libere o usuário para o webservice:\n\n' +
          '1. No menu Empresa, ou na busca do portal, localize Emissão de NFS-e por WebService.\n\n' +
          '   ![Busca do Portal de Autoatendimento com o serviço Emissão de NFS-e por WebService no ' +
          'grupo Empresa](/imagens/ipm/autoatendimento-busca-emissao-por-webservice.png "Emissão ' +
          'de NFS-e por WebService no autoatendimento")\n\n' +
          '2. Clique em Acessar para abrir a página de liberação.\n\n' +
          '   ![Página do serviço Emissão de NFS-e por WebService com o botão Acessar](/imagens/ipm/' +
          'autoatendimento-acessar-emissao-por-webservice.png "Acessar a página de liberação")\n\n' +
          '3. Em Liberar Acesso ao Usuário, selecione Sim e clique em Confirmar.\n\n' +
          '   ![Campo Liberar Acesso ao Usuário com a opção Sim e o botão Confirmar](/imagens/ipm/' +
          'autoatendimento-liberar-acesso-ao-usuario.png "Liberar acesso ao usuário")',
      },
      {
        severidade: 'Alerta',
        titulo: 'A natureza da operação do IPM tem valores diferentes dos do InvoiCy',
        corpo:
          'O IPM usa códigos próprios de situação tributária. Envie o valor do InvoiCy que ' +
          'corresponde ao código do IPM, conforme a tabela da seção Natureza da operação desta ' +
          'versão.',
      },
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
      categoria: 'Nacional',
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
    naturezaOperacao: TABELAS_NATUREZA.nacional,
    esquema: {
      arquivos: ESQUEMA_DPS_NACIONAL,
      raiz: 'DPS',
      prefixo: 'EnviarLoteDpsSincronoEnvio/LoteDps/ListaDps/',
    },
    prefixoDestino: 'EnviarLoteDpsSincronoEnvio/LoteDps/ListaDps/',
    avisos: [
      {
        severidade: 'Alerta',
        titulo: 'Liberação na Nota Control e faixa de RPS/DPS antes de emitir',
        corpo:
          'Antes de configurar a emissão:\n' +
          '- Peça à Nota Control (ISS.net) a liberação do ambiente de produção ou de homologação, ' +
          'pelo e-mail suporte@notaeletronica.com.br. Sem ela, o retorno é o E157 (usuário não ' +
          'autorizado a utilizar esse serviço para esse contribuinte).\n' +
          '- Peça a liberação de uma faixa de RPS/DPS no portal ISS.net Online do município, em ' +
          'Solicitação de Documentos Fiscais > Solicitação, e aguarde a aprovação do fiscal. Só a ' +
          'numeração liberada pode ser usada.\n\n' +
          '![Portal ISS.net com o menu Solicitação de Documentos Fiscais, a opção Solicitação e o ' +
          'formulário com modelo RPS e quantidade de notas](/imagens/issnet/solicitacao-de-faixa-' +
          'de-rps.png "Solicitação de faixa de RPS no ISS.net")',
      },
      {
        severidade: 'Alerta',
        titulo: 'Em homologação, use só a série 8',
        corpo: 'Todas as emissões em homologação precisam usar exclusivamente a série 8.',
      },
      {
        severidade: 'Alerta',
        titulo: 'Município com tabela própria de código de tributação municipal',
        corpo:
          'Alguns municípios usam uma tabela própria de CodigoTributacaoMunicipio. Confira o ' +
          'código vigente antes de emitir, para evitar rejeição ou alíquota incorreta:\n' +
          '- No portal ISSNETonline da cidade, clique em Downloads, no menu superior direito.\n' +
          '- Baixe o arquivo "Correlação Tributação Municipal X Tributação Nacional", atualizado ' +
          'periodicamente pela prefeitura.\n\n' +
          '![Portal ISSNETonline de Brasília/DF com o botão Downloads destacado no menu ' +
          'superior](/imagens/issnet/portal-issnetonline-downloads.png "Downloads do portal ' +
          'ISSNETonline")\n\n' +
          '![Lista Arquivos para Download com o arquivo Correlação Tributação Municipal X ' +
          'Tributação Nacional destacado](/imagens/issnet/arquivo-correlacao-tributacao-municipal-' +
          'nacional.png "Arquivo de correlação da tributação")',
      },
    ],
  },
  ...PROVEDORES_DE_LEIAUTE_NACIONAL,
}
