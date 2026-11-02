# Glossário: NFS-e e InvoiCy

Termos usados nos layouts, observações e rejeições desta documentação.

## Documentos e ambientes

| Termo | Significado |
| :--- | :--- |
| **NFS-e** | Nota Fiscal de Serviços eletrônica, emitida pela prefeitura (ou pelo Ambiente Nacional) a partir do RPS/DPS. |
| **RPS** | Recibo Provisório de Serviços. Documento enviado pelo emissor para ser convertido em NFS-e nos padrões municipais (ex.: ABRASF). |
| **DPS** | Declaração de Prestação de Serviço. Equivalente ao RPS no **leiaute nacional** da NFS-e. |
| **Lote** | Conjunto de RPS/DPS enviado numa única requisição. Alguns padrões limitam a quantidade (ex.: 50). |
| **Protocolo** | Número devolvido no processamento assíncrono para consultar o resultado depois. |
| **ADN** | Ambiente de Dados Nacional da NFS-e. Recebe as DPS dos municípios conveniados e autoriza a NFS-e nacional. |
| **Emissor Nacional** | Sistema público de emissão de NFS-e do governo federal, ligado ao ADN. |
| **Homologação** | Ambiente de testes do provedor. Notas emitidas nele não têm valor fiscal. |
| **Produção Restrita** | Ambiente de testes do **ADN**. Precisa ser habilitado pelo município (ver E0037). |
| **Produção** | Ambiente real, com valor fiscal. |

## Padrões e normas

| Termo | Significado |
| :--- | :--- |
| **Padrão / provedor** | Empresa ou sistema que a prefeitura usa para NFS-e (ex.: Betha, IPM, GISS). Um mesmo padrão atende vários municípios. |
| **ABRASF** | Modelo de webservice de NFS-e definido pela Associação Brasileira das Secretarias de Finanças. Versões 2.03 e 2.04 citadas aqui. |
| **Leiaute nacional** | Estrutura da DPS definida pela NFS-e Nacional (versão 1.01). Num provedor, "versão nacional" é o layout com campos de nomes muito parecidos com os do Emissor Nacional (às vezes com um campo ou outro diferente). O que foge disso, inclusive o ABRASF, é **layout próprio**. |
| **NT** | Nota Técnica. Documento que altera o leiaute (ex.: NT004, NT007). |
| **NTE** | Nota Técnica do IPM (ex.: NTE 122/2025). |
| **XSD** | Arquivo que define a estrutura válida do XML. XML fora do XSD gera rejeições como E1235. |
| **LC 116/2003** | Lei Complementar que lista os serviços tributáveis pelo ISS (item da lista de serviço). |

## Tributação

| Termo | Significado |
| :--- | :--- |
| **ISS / ISSQN** | Imposto Sobre Serviços, municipal. |
| **ISS retido** | ISS recolhido pelo tomador (ou intermediário) em vez do prestador. |
| **IBS / CBS** | Imposto e Contribuição sobre Bens e Serviços, criados pela Reforma Tributária. Informados no grupo `IBSCBS`. |
| **RTC** | Reforma Tributária do Consumo. No InvoiCy, `VersaoEsquema=RTC` ativa o grupo IBS/CBS. |
| **cClassTrib** | Código de classificação tributária do IBS/CBS. |
| **NBS** | Nomenclatura Brasileira de Serviços. Código de 9 dígitos, sem pontos. |
| **CNAE** | Classificação Nacional de Atividades Econômicas. 7 dígitos. |
| **Código de tributação municipal** | Código da atividade na tabela da prefeitura (campo `TributMunicipio` no InvoiCy). |
| **Código de tributação nacional** | Código do serviço na tabela nacional da NFS-e. |
| **natOp** | Natureza da operação no InvoiCy (tributada, isenta, imune, etc.). Cada padrão converte para o próprio código. |
| **CST** | Código de Situação Tributária (ex.: CST do PIS/COFINS). |
| **Simples Nacional** | Regime tributário simplificado. Afeta campos como `OptSN` e `AnexoSimplesNacional`. |
| **vLiq** | Valor líquido da nota. Ver [cálculo](artigos/calculo-valor-liquido.md). |
| **Retenção federal** | IRRF, contribuição previdenciária, CSLL, PIS e COFINS retidos pelo tomador. Ver [artigo](artigos/retencoes-federais.md). |
| **Débito próprio (PIS/COFINS)** | PIS e COFINS apurados pelo prestador e destacados na nota (`ValPIS`, `ValCOFINS`). Não é retenção no leiaute nacional. |
| **vRetCSLL** | Campo nacional que, desde a NT 007, recebe a soma de PIS, COFINS e CSLL retidos. `tpRetPisCofins` indica o que entrou. |

## Partes da nota

| Termo | Significado |
| :--- | :--- |
| **Prestador** | Quem presta o serviço e emite a nota. |
| **Tomador** | Quem contrata o serviço. |
| **Intermediário** | Terceiro que intermedia o serviço. |
| **Destinatário** | Quem recebe o serviço, quando difere do tomador (obrigatório em alguns padrões após a Reforma). |
| **IM** | Inscrição Municipal do prestador ou tomador. |
| **NIF** | Número de identificação fiscal de estrangeiro. |
| **CAEPF** | Cadastro de Atividade Econômica da Pessoa Física. |

## Layout InvoiCy

| Termo | Significado |
| :--- | :--- |
| **De/Para** | Mapeamento entre a tag do XML do provedor e o campo do layout InvoiCy. |
| **Campo InvoiCy** | Nome do campo no XML de entrada do InvoiCy (ex.: `RPSNumero`, `CNPJ_prest`). |
| **Seq (C01, C58...)** | Numeração do campo no layout oficial do InvoiCy. |
| **Grupo / Elemento / Atributo** | Grupo agrupa tags; elemento guarda valor; atributo é propriedade da tag (ex.: `Id`). |
| **Ocorrência** | Mínimo-máximo de vezes que a tag aparece (`1-1` obrigatória, `0-1` opcional, `1-N` repetível). |
| **Interno / Externo** | Externo: o ERP informa. Interno: o InvoiCy preenche sozinho. |
| **Chave primária de autenticação** | Campo do InvoiCy para token/chave exigido por alguns padrões (E&L, RLZ). |
| **Webhook** | Notificação automática do InvoiCy ao ERP quando o status do documento muda. |
| **Importação de Documentos** | Extensão do InvoiCy que importa notas emitidas direto na prefeitura. |
| **Status em conflito** | Situação em que o InvoiCy não consegue confirmar se a nota foi autorizada (ex.: falha de consulta). Confira no portal antes de reenviar. Ver [artigo](artigos/status-em-conflito.md). |
| **Tipo de processamento** | Configuração da empresa no InvoiCy: **Síncrono** (envia na hora) ou **Assíncrono** (fila do InvoiCy). |
| **Particularidade condicionada** | Regra configurada no InvoiCy que troca, herda ou preenche o valor de uma tag antes do envio à prefeitura. |
| **Versão do município** | Configuração que define o layout/padrão usado na emissão (ex.: *0.20 – NFS-e Nacional*). |
| **inv03xx** | Códigos de mensagem do próprio InvoiCy para NFS-e. Ver [lista](../03-rejeicoes/invoicy-inv03xx.md). |

## Ciclo de vida do RPS

| Termo | Significado |
| :--- | :--- |
| **Efetivado / autorizado** | RPS convertido em NFS-e pela prefeitura. |
| **Rejeitado** | Recusado pela prefeitura ou pelo InvoiCy; pode ser corrigido e reenviado com o mesmo número. |
| **Pendente / em processamento** | Na fila do InvoiCy ou aguardando a prefeitura (assíncrono). |
| **Cancelado** | NFS-e cancelada. O número de RPS não pode ser reutilizado (inv0314). |
| **Emissão sequencial** | Regra da prefeitura que só autoriza o próximo RPS depois do anterior. Ver [artigo](artigos/emissao-sequencial-rps.md). |
| **Faixa de RPS** | Intervalo de numeração liberado pela prefeitura (ex.: Betha, Governo Digital, Equiplano). |
| **AIDF / código de verificação do RPS** | Arquivo/código que a prefeitura gera por número de RPS (padrão Governa; AIRPS em Caucaia). |
| **Espelho** | Documento auxiliar de impressão do RPS/NFS-e. |

## Códigos de município

| Termo | Significado |
| :--- | :--- |
| **IBGE** | Código de 7 dígitos do município. É o que o InvoiCy espera. |
| **SIAFI** | Código de município da Receita Federal, usado pelo DSFNET. O InvoiCy converte do IBGE automaticamente. |
