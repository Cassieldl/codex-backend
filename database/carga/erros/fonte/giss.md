# Rejeições: GISS

> Fontes: `_origem/Padrões/GISS/Erros_e_Alertas.xlsx` (lista completa do provedor) e `_origem/Padrões/GISS/OBSERVAÇÕES.txt` (erros mais comuns). Observações do padrão: [GISS](../02-observacoes/observacoes-provedores.md#giss).

Códigos iniciados por **A** são **alertas**: a NFS-e é gerada, mas há pendência a regularizar. Códigos iniciados por **E** são **erros**: o RPS é rejeitado.

## Erros mais comuns

| Código | Mensagem | Causa provável | Como resolver |
| :--- | :--- | :--- | :--- |
| E1 | Assinatura do lote inválida | Certificado digital expirado, revogado ou cadeia ICP-Brasil incompleta. | Verifique a validade do certificado A1/A3 e atualize a cadeia de confiança. |
| E4 | Envio com erro no schema XML | Uma ou mais tags obrigatórias ausentes ou fora da ordem do XSD. | Valide o XML no validador XSD oficial da versão 2.04 antes do envio. |
| E10 | RPS já informado | Envio de um número de RPS já convertido anteriormente. | Consulte o RPS via ConsultarNfsePorRps para recuperar a NFS-e já existente. |
| E20 | Tomador não informado | Identificação do tomador ausente ou inválida. | Preencha CPF/CNPJ válido, ou NIF quando o tomador for estrangeiro. |
| E172 | Código de tributação inexistente | Código de tributação municipal não cadastrado na prefeitura. | Confira o código municipal compatível com o item da LC 116/2003. |

## Lista completa de erros e alertas

| Código | Tipo | Mensagem | Solução |
| :--- | :--- | :--- | :--- |
| A01 | Alerta | Não foi possivel atender a solicitação | Tente novamente mais tarde ou entre em contato com o atendimento. |
| A1 | Alerta | Alguns dos dados informados não constam do cadastro do contribuinte. | O tipo do serviço prestado não está registrado no cadastro do contribuinte. Procure a Prefeitura para registrar os códigos de tributação, códigos de serviço, códigos da subclasse CNAE ou o item da lista da Lei Complementar correspondentes aos serviços prestados pela empresa. |
| A10 | Alerta | Inscrição municipal do prestador de serviço encontra-se paralisada ou cancelada ou desativada ou baixada. | A NFS-e foi gerada e o prestador de serviço deverá comparecer à Prefeitura para regularizar sua situação. |
| A11 | Alerta | NFS-e emitida com falta de elementos de qualificação do prestador do serviço. | NFS-e foi gerada e o prestador de serviço deverá comparecer à Prefeitura para regularizar sua situação. |
| A12 | Alerta | RPS em duplicata no lote foram ignorados. | A Prefeitura desconsiderou os RPS cujos elementos eram idênticos aos elementos de RPS já processados. |
| A13 | Alerta | Prazo para a conversão do RPS em NFS-e vencido | Procure a Prefeitura para regularizar a situação. |
| A14 | Alerta | O campo ValorISS informado indevidamente. | O valor do ISSQN é calculado pela Prefeitura e não deve ser informado pelo contribuinte. O valor informado foi desconsiderado. |
| A15 | Alerta | Alíquota informada indevidamente. | A alíquota utilizada para a determinação do valor do ISSQN é determinada pela Prefeitura. Não deve ser informada pelo contribuinte. A alíquota informada foi desconsiderada. |
| A16 | Alerta | Código da subclasse CNAE informado indevidamente. | O código da subclasse CNAE correspondente ao serviço prestado não é utilizado pela Prefeitura. O elemento informado foi desconsiderado. |
| A17 | Alerta | Não informe inscrição municipal quando o intermediário do serviço for domiciliado em outro município. | Inscrição municipal do intermediário desconsiderada. |
| A18 | Alerta | O tipo de serviço informado não permite indicação de código da obra. | Código da obra desconsiderado. |
| A19 | Alerta | O tipo de serviço informado não permite indicação de ART - Anotação de Responsabilidade Técnica. | ART - Anotação de Responsabilidade Técnica desconsiderada. |
| A2 | Alerta | Valor das deduções indevido. | A NFS-e foi emitida considerando a informação do RPS, no entanto o tipo de serviço prestado, identificado pelo código de tributação, código se serviço, código da subclasse CNAE ou item da lista da Lei Complementar, não permite dedução da base de cálculo do ISSQN. Consulte a legislação do município para informações sobre as deduções permitidas. |
| A20 | Alerta | O regime de tributação informado difere do registrado na Prefeitura. | A NFS-e foi emitida considerando o regime de tributação informado. Procure a Prefeitura para atualizar o regime de tributação no cadastro do contribuinte. |
| A21 | Alerta | Opção pelo SIMPLES Nacional não consta do cadastro do contribuinte. | Não há registro no cadastro do contribuinte de opção pelo SIMPLES Nacional. A NFS-e foi emitida considerando a informação do RPS. Procure a Prefeitura para regularizar a situação. |
| A22 | Alerta | Código de tributação incompatível com o município de incidência do ISSQN. | A NFS-e foi emitida considerando a informação do RPS, no entanto o código de tributação informado não permite a tributação em outro município. Consulte na legislação o local de incidência do ISSQN. |
| A23 | Alerta | O prestador do serviço não possui incentivo fiscal no cadastro da Prefeitura. | Não há concessão de incentivo para o prestador de serviço registrada na Prefeitura. A NFS-e foi emitida considerando a informação do RPS. Procure a Prefeitura para regularizar a situação. |
| A24 | Alerta | País da prestação do serviço indevido. | O código do país da prestação do serviço somente deverá ser informado quando a exigibilidade do ISSQN for igual a <Exportação>. |
| A25 | Alerta | País do tomador do serviço indevido. | O código do país do tomador do serviço somente deverá ser informado quando o município do tomador do serviço for igual a <9999999>. |
| A26 | Alerta | E-mail do tomador do serviços inválido. | O e-mail do tomador de serviços não é válido e foi desconsiderado. |
| A27 | Alerta | CEP do logradouro do tomador do serviço inexistente. | A NFS-e foi emitida com o CEP do tomador do serviço informado no RPS. |
| A28 | Alerta | Inscrição municipal do tomador do serviço indevida. | Não informe o campo inscrição municipal do tomador do serviço quando este for do exterior. |
| A29 | Alerta | CNPJ/CPF do tomador do serviço indevida. | Não informe o campo CNPJ/CPF do tomador do serviço quando este for do exterior. |
| A30 | Alerta | RPS possui campo(s) informado(s) que não é (são) adotado(s) pelo município | Algum campo não obrigatório no Termo de Referência informado no RPS não é adotado pelo município. Por este motivo, não foi consistido e nem será considerado como informação válida para a NFS-e. |
| A5 | Alerta | Endereço do tomador do serviço difere do constante na base de dados do município. | A NFS-e foi emitida considerando o endereço do tomador do serviço informado no RPS. |
| A6 | Alerta | Inscrição municipal do tomador não encontrada. | A inscrição municipal do tomador do serviço informada no RPS não está registrada na Prefeitura. A NFS-e foi emitida considerando que o tomador do serviço não está inscrito na Prefeitura. |
| A7 | Alerta | A inscrição municipal do tomador do serviço está paralisada ou cancelada ou desativada ou baixada. | A inscrição municipal do tomador do serviço informada no RPS está paralisada ou cancelada ou desativada ou baixada no cadastro municipal. A NFS-e foi emitida considerando que o tomador do serviço não está inscrito na Prefeitura. |
| A8 | Alerta | E-mail do tomador do serviço inválido. | O e-mail do tomador de serviços não é válido e foi ignorado. Não será enviado e-mail de confirmação da emissão da NFS-e para o tomador do serviço. |
| A9 | Alerta | Inscrição municipal incorreta. | A NFS-e foi gerada considerando a inscrição municipal vinculada ao CNPJ/CPF informado. |
| E10 | Erro | RPS já informado. | Para essa Inscrição Municipal/CNPJ já existe um RPS informado com o mesmo número, série e tipo. |
| E100 | Erro | Valor dos serviços inválido. | O campo valor dos serviços é numérico e deverá ter tamanho máximo de 15 números inteiros e dois decimais. |
| E101 | Erro | Valor das deduções inválido. | O campo valor das deduções é numérico e deverá ter tamanho máximo de 15 números inteiros e dois decimais. |
| E102 | Erro | Valor dos descontos inválido. | O campo valor dos descontos é numérico e deverá ter tamanho máximo de 15 números inteiros e dois decimais. |
| E103 | Erro | Valor da Retenção de tributo federal inválida. | O campo referente a retenção de tributo federal é numérico e deverá ter tamanho máximo de 15 números inteiros e dois decimais. |
| E104 | Erro | Item da lista inválido. | O campo item da lista de serviço deverá ter tamanho máximo de 5 caracteres. |
| E105 | Erro | Código CNAE inválido. | Utilize sete caracteres para informar o código da subclasse CNAE correspondente ao serviço prestado. |
| E106 | Erro | Código de tributação inválido. | Utilize no máximo 20 caracteres para informar o código do serviço prestado. |
| E107 | Erro | Discriminação do serviço inválida. | Utilize no máximo 2000 caracteres para descrever o serviço prestado. |
| E108 | Erro | Código do município da prestação do serviço inválido. | Informe o código do município da prestação do serviço com sete dígitos, Consulte tabela de Municípios do IBGE. Se o município da prestação do serviço for do exterior do país, informe "9999999". |
| E109 | Erro | Código do município do tomador do serviço inválido. | Informe o código do município do tomador do serviço com sete dígitos, Consulte tabela de Municípios do IBGE. Se o tomador do serviço for do exterior do país, informe "9999999". |
| E11 | Erro | Número do RPS não informado. | Informe o número do RPS. |
| E111 | Erro | Município da prestação do serviço não informado. | Informe o código do município da prestação do serviço com sete dígitos, Consulte tabela de Municípios do IBGE. Se o município da prestação do serviço for do exterior do país, informe "9999999". |
| E112 | Erro | Inscrição Municipal do intermediário do serviço inválida. | O campo Inscrição Municipal do intermediário do serviço é numérico e deverá ter tamanho máximo de 15 dígitos. |
| E113 | Erro | Número do logradouro não informado. | Para este tomador do serviço informe o número do logradouro. |
| E114 | Erro | Bairro do tomador do serviço não informado. | Para este tomador do serviço informe o bairro. |
| E115 | Erro | UF do tomador do serviço não informada. | Para este tomador do serviço informe a UF. |
| E116 | Erro | A UF do tomador do serviço incorreta. | Informe corretamente a UF do tomador do serviço. Em caso de cidades do exterior preencher a UF com "EX" e a cidade com 9999999. |
| E117 | Erro | Nome/Razão Social do tomador do serviço inválida. | Nome/Razão Social do tomador do serviço deverá ter tamanho máximo de 115 caracteres. |
| E118 | Erro | Nome/Razão Social do tomador do serviço não informado | Para este tomador do serviço informe o Nome/Razão Social. |
| E119 | Erro | Logradouro do endereço do tomador do serviço inválido. | O campo endereço do tomador do serviço deverá ter tamanho máximo de 125 caracteres. |
| E12 | Erro | Tipo do RPS não informado. | Informe o tipo do RPS, conforme indicado na documentação da NFS-e. |
| E120 | Erro | Número do logradouro inválido. | O campo número do endereço do tomador do serviço deverá ter tamanho máximo de 10 caracteres. |
| E121 | Erro | Complemento do endereço inválido. | O campo complemento do endereço do tomador do serviço deverá ter tamanho máximo de 60 caracteres. |
| E122 | Erro | Bairro do tomador do serviço inválido. | O campo bairro do tomador do serviço deverá ter tamanho máximo de 60 caracteres. |
| E124 | Erro | UF do tomador do serviço inválida. | O campo UF do tomador do serviço deverá ter tamanho máximo de 2 caracteres. Em caso de cidades do exterior preencher a UF com "EX". |
| E125 | Erro | CEP do tomador do serviço inválido. | O campo CEP do tomador do serviço deverá ter tamanho máximo de 8 dígitos. |
| E126 | Erro | E-mail do tomador do serviço inválido. | O campo e-mail do tomador do serviço deverá ter tamanho máximo de 80 caracteres. |
| E127 | Erro | Telefone do tomador do serviço inválido. | Consulte o Manual da NFS-e para saber quais as regras de preenchimento do telefone do tomador do serviço. |
| E128 | Erro | Razão Social do intermediário do serviço inválida. | O campo razão social do intermediário do serviço deverá ter tamanho máximo de 115 caracteres. |
| E129 | Erro | Código da obra inválido. | O campo código da obra deverá ter tamanho máximo de 15 caracteres. |
| E13 | Erro | RPS inválido. | Utilize um dos tipos especificados: 1 - "RPS", 2 - "Nota Fiscal Conjugada (Mista)" ou 3 - "Cupom". |
| E130 | Erro | Número da ART inválido. | O campo ART deverá ter tamanho máximo de 15 caracteres. |
| E131 | Erro | Data inicial inválida. | A data informada deverá estar no formato DD/MM/AAAA e deve ser uma data válida. |
| E132 | Erro | Data final inválida. | A data informada deverá estar no formato DD/MM/AAAA e deve ser uma data válida. |
| E133 | Erro | Data final posterior a data de hoje. | Informe uma data final igual ou anterior a data de hoje. |
| E134 | Erro | Data final anterior à data inicial. | Informe uma data final igual ou superior a data inicial da pesquisa. |
| E135 | Erro | Período consultado superior ao permitido. | Consulte o manual da NFS-e e limite as datas inicial e final ao período permitido. |
| E136 | Erro | Número da NFS-e consultada inválido. | O campo número da NFS-e é numérico e deverá ter tamanho máximo de 15 dígitos. |
| E137 | Erro | Data de emissão da NFS-e informada incorretamente. | Informe corretamente a data da emissão da NFS-e a ser consultada. |
| E138 | Erro | Usuário não autorizado a realizar o serviço. | Informe os dados de um usuário autorizado a executar o serviço. |
| E139 | Erro | Número da NFS-e a substituir inválido. | O campo número da NFS-e substituída é numérico e deverá ter tamanho máximo de 15 dígitos. |
| E14 | Erro | Data da emissão do RPS não informada. | Informe a Data da emissão do RPS conforme indicado na documentação da NFS-e. |
| E140 | Erro | Bairro do prestador do serviço inexistente | Informe corretamente o nome bairro do prestador do serviço |
| E141 | Erro | Inscrição Municipal do prestador do serviço não informada | informe a Inscrição Municipal do prestador do serviço |
| E145 | Erro | Regime Especial de Tributação inválido. | Informe o regime especial de tributação utilizando dois algarismos. Consulte o Manual da NFS-e para saber quais são os regimes autorizados pela Prefeitura a utilizar NFS-e. |
| E146 | Erro | Opção pelo Simples Nacional inválida. | Utilize um dos tipos: 1 ¿ Sim; 2 - Não. |
| E148 | Erro | Status do RPS não informado | Utilize um dos tipos: 1 ¿ Normal; 2 - Cancelado. |
| E149 | Erro | CNPJ do prestador do serviço inválido | O campo CNPJ Prestador do serviço é numérico e deverá ter tamanho máximo de 14 dígitos. |
| E15 | Erro | Data da emissão do RPS inválida. | Informe a Data da emissão do RPS conforme indicado na documentação da NFS-e. |
| E150 | Erro | Série do RPS não informada | Campo de preenchimento obrigatório, caso não utilize série, preencha o campo com 00000. |
| E151 | Erro | Quantidade de RPS não informada | Informe a quantidade de RPS contidos no lote |
| E152 | Erro | Indicador de ISSQN retido não informado. | Informe 1 para ISSQN Retido ou 2 para ISSQN não Retido. |
| E153 | Erro | Valor ISSQN retido inválido | O campo valor do ISSQN retido é numérico e deverá ter tamanho máximo de 15 números inteiros e dois decimais. |
| E154 | Erro | CNPJ/CPF do intermediário do serviço inválido. | O campo CNPJ/CPF do Intermediário do serviço é numérico. E deverá ter tamanho de 14 dígitos para CNPJ e 11 dígitos para CPF. |
| E155 | Erro | CNPJ/CPF do tomador do serviço inválido. | O campo CNPJ/CPF do tomador do serviço é numérico. E deverá ter tamanho de 14 dígitos para CNPJ e 11 dígitos para CPF. |
| E157 | Erro | Usuário não tem autorização para solicitar o serviço. | A pessoa física ou jurídica, identificada pelo CPF ou CNPJ, não está autorizada a solicitar o serviço para o contribuinte identificado pelo CPF ou CNPJ. |
| E158 | Erro | Inscrição Municipal do prestador do serviço inválida | O campo Inscrição Municipal do prestador do serviço é numérico e deverá ter tamanho máximo de 15 dígitos. |
| E16 | Erro | A data da emissão do RPS não poderá ser superior a data de hoje. | Informe uma data de emissão de RPS válida. |
| E160 | Erro | Arquivo em desacordo com o XML Schema. | Consulte o Manual da NFS-e para saber quais são as versões de XML Schema suportadas pelo sistema. |
| E162 | Erro | Alíquota do simples nacional incorreta. | Informe um percentual de acordo com o enquadramento na tabela de alíquota do simples nacional. |
| E163 | Erro | Alíquota não informada para retenção do ISSQN no Simples Nacional | Informe um percentual de acordo com o enquadramento na tabela de alíquota do simples nacional. |
| E164 | Erro | Valor do ISSQN devido inválido. | O campo valor do ISSQN é numérico e deverá ter tamanho máximo de 15 números inteiros e 2 decimais. |
| E165 | Erro | Alíquota do serviço inválida | Consulte o manual da NFS-e para verificar o formato e o tamanho do campo. |
| E166 | Erro | Regime Especial de Tributação não informado para optante do simples nacional.. | Quando a empresa prestadora do serviço for optante pelo simples nacional informe "05" MEI ou "06" ME/EPP. |
| E167 | Erro | Município informado não pertence à Unidade Federativa. | Acerte o município ou a Unidade Federativa. |
| E168 | Erro | CEP do tomador do serviço informado indevidamente. | Não informe o CEP do tomador do serviço quando ele for domiciliado no exterior. |
| E169 | Erro | CNPJ/CPF do tomador do serviço informado indevidamente. | Não informe o CNPJ/CPF do tomador do serviço quando ele for domiciliado no exterior. |
| E17 | Erro | A data da emissão do RPS não poderá ser inferior à data de habilitação do prestador do serviço para emissão da NFS-e. | A geração de NFS-e só é permitida para competência posterior à data de habilitação da empresa. |
| E170 | Erro | O usuário não tem autorização para solicitar serviço de processamento de lote de RPS. | A solicitação de serviço de processamento de lote de RPS só pode ser feita por usuário previamente autorizado pelo contribuinte. |
| E171 | Erro | O usuário não tem autorização para assinar RPS para o prestador de serviço | Acerte a assinatura do RPS. |
| E172 | Erro | Arquivo enviado com erro na assinatura. | Acerte a assinatura do arquivo. |
| E173 | Erro | Lote de RPS não assinado. | Assine o lote de RPS |
| E174 | Erro | RPS não assinado. | Assine o RPS |
| E175 | Erro | O somatório das deduções e do desconto incondicionado está maior que o valor dos serviços | O preço do serviço deve ser maior ou igual à soma das deduções com o desconto incondicionado. |
| E176 | Erro | O somatório dos descontos, retenções federais e ISSQN retido na fonte está maior que o valor dos serviços | O preço do serviço deve ser maior ou igual à soma dos tributos federais, o ISSQN devido e os descontos. |
| E177 | Erro | Pessoa Física não pode efetuar a retenção do ISSQN | Tomador de serviço pessoa física não pode reter o ISSQN. Altere o indicador de retenção do ISSQN para 2 - sem retenção. |
| E178 | Erro | Lote aguardando processamento. | O lote de RPS correspondente ao protocolo informado foi recebido pela Prefeitura, mas ainda não foi processado. |
| E179 | Erro | Erro na autenticação. | O usuário correspondente ao documento utilizado para autenticação não possui autorização para realizar o serviço para a empresa parametrizada via CNPJ e inscrição municipal do arquivo. |
| E18 | Erro | O valor dos serviços deverá ser superior a R$ 0,00 (zero). | Não é permitido envio de valor de serviços igual a zero. |
| E180 | Erro | Pedido de Cancelamento não está assinado. | O pedido de cancelamento deve conter assinatura digital |
| E181 | Erro | Telefone do tomador do serviço incorreto | Utilize um telefone no formato válido. |
| E182 | Erro | Requisição do serviço não acompanhada de autenticação por certificação digital. | É necessário efetuar o processo de autenticação SSL bidirecional no sistema cliente. |
| E183 | Erro | A mensagem XML do cabeçalho do arquivo enviado está fora do padrão especificado. | Consulte o Manual da NFS-e para saber quais são as versões de XML Schema suportadas pelo sistema |
| E184 | Erro | A versão de dados não é mais suportada pelo sistema. A versão do XML Schema não é mais suportada pelo sistema. | Utilize uma versão de dados suportada para requisição dos serviços. Consulte o Manual da NFS-e para saber quais são as versões de XML Schema suportadas pelo sistema |
| E185 | Erro | A versão de dados não existe. A versão do XML Schema não existe. | Utilize uma versão de dados existente e suportada pelo sistema. Consulte o Manual da NFS-e para saber quais são as versões de XML Schema suportadas pelo sistema |
| E186 | Erro | Requisição de serviço inválida. O sistema não conseguiu capturar o parâmetro nfseCabecMsg e/ou nfseDadosMsg. | Verifique sua mensagem SOAP e a passagem desses parâmetros necessários. Não conseguiu obter a mensagem XML do cabeçalho ou dos dados. Verifique a passagem desses parâmetros pela mensagem SOAP. |
| E187 | Erro | A versão do XML Schema do cabeçalho não existe. | Consulte o Manual da NFS-e para saber quais são as versões de XML Schema suportadas pelo sistema |
| E188 | Erro | Opção simples nacional conflita com o regime especial de tributação informado. | Quando se tratar de empresa com Regime Especial de Tributação 05 - MEI do simples nacional ou 06 - ME ou EPP do simples nacional marcar campo de Optante pelo Simples como 1 ¿ SIM. |
| E189 | Erro | Arquivo enviado com erro de certificado. Certificado digital inválido. | O solicitante do serviço de processamento do arquivo deve utilizar um certificado digital padrão ICP Brasil, não revogado e não expirado. |
| E19 | Erro | O valor das deduções deverá ser inferior ou igual ao valor dos serviços. | Não é permitido valor de dedução superior ao valor de serviços. |
| E190 | Erro | Erro na autenticação. Certificado digital inválido. | O solicitante do serviço de processamento do arquivo deve utilizar um certificado digital padrão ICP Brasil, não revogado e não expirado. |
| E191 | Erro | Tipo do RPS inválido. | Neste município utilizar somente o Tipo 1 - RPS |
| E192 | Erro | Status RPS invalido | Neste município utilizar somente o Tipo 1 ¿ Normal. |
| E193 | Erro | Valor da Retenção do PIS inválido | O campo referente a retenção do PIS é numérico e deverá ter tamanho máximo 15 números inteiros e dois decimais. |
| E194 | Erro | Valor da Retenção da COFINS inválido | O campo referente a retenção da COFINS é numérico e deverá ter tamanho máximo 15 números inteiros e dois decimais. |
| E195 | Erro | Valor da Retenção de INSS inválido | O campo referente a retenção de INSS é numérico e deverá ter tamanho máximo 15 números inteiros e dois decimais. |
| E196 | Erro | Valor da Retenção do IR inválido | O campo referente a retenção do IR é numérico e deverá ter tamanho máximo 15 números inteiros e dois decimais. |
| E197 | Erro | Valor da Retenção da CSLL inválido | O campo referente a retenção da CSLL é numérico e deverá ter tamanho máximo 15 números inteiros e dois decimais. |
| E198 | Erro | Valor das Outras retenções inválido | O campo referente a outras retenções é numérico e deverá ter tamanho máximo 15 números inteiros e dois decimais. |
| E199 | Erro | CEP do tomador do serviço não informado | Informe o CEP do tomador do serviço |
| E2 | Erro | Data de competência superior a de emissão do RPS. | Informe uma data de competência inferior ou igual a de emissão do RPS. |
| E20 | Erro | O valor das deduções deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de dedução inferior a zero (negativo). |
| E200 | Erro | Data de Competência anterior à da autorização para emissão de NFS-e | A geração de NFS-e só é permitida para competência posterior à data de habilitação da empresa. |
| E201 | Erro | Status do RPS não informado | Informe o status do RPS. Para este município utilizar somente o Tipo 1 ¿ Normal. |
| E202 | Erro | Código de tributação não informado | Informe o código de tributação referente aos serviços prestados |
| E203 | Erro | Tamanho do arquivo enviado excede o permitido. | Consulte o Manual da NFS-e para saber qual o tamanho de arquivo permitido pelo sistema. |
| E204 | Erro | Código de cancelamento não informado. | Consulte o Manual da NFS-e para saber os códigos de cancelamento permitidos pelo sistema. |
| E205 | Erro | Código de cancelamento inválido. | O campo referente ao cancelamento da NFS-e é numérico e deverá ter tamanho de 4 números inteiros. |
| E206 | Erro | Cancelamento da NFS-e por erro na emissão não pode ser feito por esse serviço. | Utilize o serviço de substituição de NFS-e. |
| E207 | Erro | Cancelamento de NFS-e com imposto recolhido não pode ser feito por esse serviço. | O cancelamento de uma NFS-e com ISSQN pago deverá ser feito por meio de processo administrativo aberto na repartição fazendária. |
| E208 | Erro | Cancelamento de NFS-e com tomador do serviço não informado não pode ser feito por esse serviço. | O cancelamento de uma NFS-e com tomador do serviço não informado deverá ser feito por meio de processo administrativo aberto na repartição fazendária. |
| E209 | Erro | Data inicial não informada | Informe a data inicial para pesquisa |
| E21 | Erro | O valor dos descontos deverá ser inferior ou igual ao valor dos serviços. | Não é permitido valor de desconto superior ao valor de serviços. |
| E210 | Erro | Data inicial da pesquisa não poderá ser posterior a data de hoje. | Informe uma data inicial igual ou anterior a data de hoje |
| E211 | Erro | Data inicial da pesquisa não poderá ser posterior a data final. | Informe uma data inicial igual ou anterior a data final |
| E212 | Erro | NFS-e não encontrada. | Não existe NFS-e emitida com o número do documento ou do RPS ou período pesquisado. |
| E213 | Erro | Código de cancelamento informado de uso exclusivo da Prefeitura | Código de motivo de cancelamento <3-Erro de assinatura> e <5-Erro de processamento> são de uso exclusivo da Prefeitura. |
| E214 | Erro | Número de RPS no lote excede o permitido. | Consulte o Manual da NFS-e para saber o número máximo de RPS por lote permitido pelo sistema. |
| E215 | Erro | Item da lista de serviço incompatível com o código de tributação. | Consulte o Manual da NFS-e para verificar a correlação entre o código de tributação e o item da lista de serviço.. |
| E216 | Erro | Não é permitida a substituição de RPS. | O campo RpsSubstituido não pode ser preenchido para este município. |
| E217 | Erro | Número inicial da faixa de NFS-e não encontrado. | Informe um número inicial válido. |
| E218 | Erro | Número inicial da faixa de NFS-e é maior que o número final. | Informe um número final maior que o número inicial. |
| E219 | Erro | Tipo de Regime Especial de Tributação indevido. | Consulte o Manual da NFS-e para informar uma opção de Regime Especial de Tributação valido no município. |
| E22 | Erro | O valor dos descontos deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de desconto inferior a zero (negativo). |
| E220 | Erro | Valor do ISSQN informado indevidamente. | O valor do ISSQN será calculado pela Prefeitura e não deve ser informado pelo contribuinte. |
| E221 | Erro | Alíquota informada indevidamente. | A alíquota do ISSQN só deve ser informada quando: o ISSQN for devido a outro município ou o prestador do serviço for optante pelo Simples Nacional e houver retenção do ISSQN. Em outras situações a alíquota a ser aplicada será determinada pela Prefeitura. |
| E222 | Erro | Código CNAE informado indevidamente. | Esse campo não deve ser preenchido neste município. |
| E223 | Erro | Intervalo de NFS-e não encontradas. | Não foram emitidas NFS-e com números no intervalo informado. |
| E224 | Erro | A NFS-e a substituir está cancelada. | NFS-e cancelada não pode ser substituída. |
| E225 | Erro | O documento de substituição não está assinado digitalmente. | Assine o documento de substituição. |
| E226 | Erro | Número da NFS-e inicial não informado | Informe o número da NFS-e inicial do intervalo a ser consultado. |
| E227 | Erro | Alíquota Serviços fora do intervalo de 2% e 5% | Quando o local da incidência for diferente deste município esse campo deve ser maior ou igual a 2% e menor ou igual a 5%. |
| E228 | Erro | Para esta prestação de serviços a alíquota do ISSQN é obrigatória. | Informe um valor percentual da alíquota de ISSQN, que deve ser maior ou igual a 2% e menor ou igual a 5%. |
| E229 | Erro | A Inscrição Municipal do tomador do serviço não existe na base de dados da Prefeitura. | Informe corretamente a Inscrição Municipal do tomador do serviço. |
| E23 | Erro | O valor do PIS deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior a zero (negativo). |
| E230 | Erro | As informações do prestador do serviço não foram identificadas no RPS. | Informe corretamente as informações do prestador do serviço para o RPS. |
| E231 | Erro | O prazo legal para conversão de um RPS em NFS-e está expirado. | Consulte a legislação do município para verificar o prazo de conversão. |
| E232 | Erro | Ocorreu um erro no processamento do arquivo. | Procure a Prefeitura para regularização. |
| E233 | Erro | Lote já processado. | Atribua um novo número ao lote de RPS, caso os RPS nele contidos ainda não tenham sidos processados. |
| E234 | Erro | Número do protocolo de recebimento do lote não informado. | Informe o número do protocolo do lote. |
| E235 | Erro | Número da NFS-e a cancelar não informado. | Informe o número da NFS-e a ser cancelada. |
| E236 | Erro | O RPS já está cancelado e não pode ser substituído. | RPS cancelado não pode ser substituído. |
| E237 | Erro | O arquivo de cancelamento de lote de NFS-e está corrompido. | Corrija e reenvie o arquivo. |
| E238 | Erro | O parâmetro nomeado (método) informado não existe | Consulte o Manual da NFS-e para saber quais são os métodos disponíveis. |
| E239 | Erro | Código de tributação não permite dedução na base de cálculo | Consulte a legislação vigente para saber se o código de tributação permite dedução da base de cálculo |
| E24 | Erro | O valor da COFINS deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior a zero (negativo). |
| E240 | Erro | Código de tributação não permite incidência do ISSQN fora deste município. | Informe corretamente o município de incidência do ISSQN. |
| E241 | Erro | Retenções de Tributos Federais só podem ser efetuados por tomador do serviço pessoa jurídica. | Preencher os campos de retenções federais com zeros. |
| E242 | Erro | Indicador de ISSQN retido incorreto. | Não é permitida a retenção do ISSQN quando o tomador do serviço é o próprio prestador do serviço. Altere o indicador de retenção do ISSQN devido para 2 - sem retenção. |
| E243 | Erro | Esta NFS-e não deverá ter o ISSQN Retido pelo tomador do serviço. | Consulte a legislação vigente para saber as regras de retenção do ISSQN na fonte e selecione ISS Retido = 2 (NÃO). |
| E244 | Erro | Código de tributação não permite a utilização de NFS-e. | Consulte a legislação aplicável e verifique os códigos de tributação que permitem a emissão de NFS-e. |
| E245 | Erro | Código de tributação não permite desconto condicionado.. | O tipo de serviço prestado não permite desconto condicionado. |
| E246 | Erro | Código de tributação não permite desconto incondicionado. | O tipo de serviço prestado não permite desconto incondicionado. |
| E247 | Erro | Código de tributação não permite informação de construção civil. | Consulte a legislação aplicável e verifique os códigos de tributação que permitem informação de construção civil. |
| E248 | Erro | Código de tributação não permite informação de intermediário do serviço. | Consulte a legislação aplicável e verifique os códigos de tributação que permitem informação de intermediário do serviço. |
| E249 | Erro | Código do município de incidência do ISSQN incorreto. | Contribuintes com regime especial de tributação "Microempresário Individual" (MEI) não pode emitir NFS-e com município de incidência diferente do município gerador. |
| E25 | Erro | O valor do INSS deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior a zero (negativo). |
| E250 | Erro | Opção SIMPLES Nacional conflita com o Regime especial de tributação. | Não é permitida a opção pelo SIMPLES Nacional por empresa que adote o Regime especial de tributação "03 -Sociedade de Profissionais". |
| E251 | Erro | Para o regime especial <Estimativa> não é permitida a emissão de NFS-e | Consulte a legislação aplicável e verifique as situações de emissão de NFS-e. |
| E252 | Erro | Para o regime especial <Cooperativa> não é permitida a emissão de NFS-e | Consulte a legislação aplicável e verifique as situações de emissão de NFS-e. |
| E253 | Erro | A data da nova competência não deve ser inferior à competência de criação da NFS-e. | Altere a data da competência. |
| E254 | Erro | NFS-e vinculada a uma guia não pode ser substituída. | Consulte a legislação aplicável e verifique as situações de substituição de NFS-e. |
| E255 | Erro | O status atual desta nota não permite alteração | A NFS-e não pode ser alterada. |
| E256 | Erro | A NFS-e informada não está apta para a troca de competência | A competência da NFS-e não pode ser alterada. |
| E257 | Erro | Esse lote não possui informações de retorno. | Verifique o lote enviado. |
| E258 | Erro | CNPJ/CPF do prestador do serviço inválido. | Informe o número do CNPJ com 14 posições ou o CPF do prestador do serviço com 11 posições. |
| E259 | Erro | CNPJ/CPF não encontrado na base de dados. | Confira o número do CNPJ/CPF informado. Caso esteja correto, o prestador do serviço não está inscrito no município. |
| E26 | Erro | O valor do IR deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior a zero (negativo). |
| E260 | Erro | CNPJ/CPF do prestador do serviço não informado. | Informe o CNPJ/CPF do prestador do serviço. |
| E261 | Erro | CNPJ/CPF do prestador do serviço incorreto. | Informe corretamente o CNPJ/CPF do prestador do serviço. |
| E262 | Erro | Inscrição municipal paralisada, cancelada ou desativada. | Prestador do serviço com inscrição municipal paralisada, cancelada ou desativada não pode utilizar NFS-e. |
| E263 | Erro | Número do RPS substituído inválido. | Identifique o RPS substituído utilizando no máximo 15 algarismos. |
| E264 | Erro | Número do RPS substituído incorreto | Informe corretamente o número do RPS substituído. |
| E265 | Erro | Número do RPS substituído não informado | Informe o número do RPS substituído quando informado série ou tipo do RPS substituído. |
| E266 | Erro | Número do RPS substituído não encontrado na base de dados | Informe um número de RPS substituído que conste da base de dados. |
| E267 | Erro | Série do RPS substituído inválida. | A série do RPS pode ser alfanumérico com tamanho máximo de 5 caracteres. |
| E268 | Erro | Série do RPS substituído não informada. | Informe a série do RPS substituído, conforme indicado no Manual da NFS-e. |
| E269 | Erro | Série do RPS substituído incorreta. | Informe corretamente a série do RPS substituído. |
| E27 | Erro | O valor da CSLL deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior a zero (negativo). |
| E270 | Erro | Tipo de RPS substituído inválido. | Informe o tipo de RPS substituído, conforme indicado no Manual da NFS-e. |
| E271 | Erro | Tipo de RPS substituído não informado. | Informe o tipo de RPS substituído, conforme indicado no Manual da NFS-e. |
| E272 | Erro | Tipo de RPS substituído incorreto. | Informe o tipo de RPS substituído, conforme indicado no Manual da NFS-e. |
| E273 | Erro | PIS retido incorreto. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E274 | Erro | COFINS retida incorreta. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E275 | Erro | Contribuição ao INSS retida incorreta. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E276 | Erro | Imposto de renda retido incorreto. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E277 | Erro | CSLL retida incorreta. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E278 | Erro | Indicador de ISSQN retido incorreto. | Empresa com inscrição municipal paralisada, cancelada ou desativada não pode reter ISSQN. Altere o indicador de retenção do ISSQN para 2 - sem retenção. |
| E279 | Erro | Responsável/Retentor inválido. | Informe Responsável/Retentor conforme indicado no Manual da NFS-e. |
| E28 | Erro | Item da lista de serviço incompatível com opção SIMPLES Nacional. | Consulte a legislação vigente para saber se o item da lista de serviço informado permite a opção pelo simples nacional. |
| E280 | Erro | Responsável/Retentor não informado. | Informe 1 se o responsável pelo recolhimento do ISSQN for o tomador do serviço ou 2 se for o intermediário do serviço. |
| E281 | Erro | Responsável/Retentor informado incorretamente. | Informe 1 se o responsável pelo recolhimento do ISSQN for o tomador do serviço ou 2 se for o intermediário do serviço. |
| E282 | Erro | Responsável/Retentor informado indevido. | O campo Responsável/Retentor somente deverá ser preenchido quando o ISSQN for retido na fonte. |
| E283 | Erro | Código CNAE incompatível com o item da lista. | Consulte a legislação aplicável e informe o código de subclasse CNAE que seja compatível com o item da lista da LC 116 informado. |
| E284 | Erro | País da prestação do serviço inválido. | Informe o código do país onde foi prestado o serviço conforme consta da Tabela de Países do BACEN. |
| E285 | Erro | País da prestação do serviço não informado. | Quando a exigibilidade do ISSQN for <Exportação> informar o país da prestação do serviço. |
| E286 | Erro | País da prestação do serviço incorreto | Informe o código do país onde foi prestado o serviço conforme consta da Tabela de Países do BACEN. |
| E287 | Erro | País da prestação do serviço indevido | O código do país da prestação do serviço somente deverá ser informado quando a exigibilidade do ISSQN for <Exportação>. |
| E288 | Erro | Código do município do tomador do serviço não vinculada a UF informada. | Corrija a UF ou o código do município do tomador do serviço. |
| E289 | Erro | País do tomador do serviço inválido. | Informe o código do país do tomador do serviço conforme consta da Tabela de Países do BACEN. |
| E29 | Erro | O código de serviço prestado não permite retenção de ISSQN. | O tipo do serviço prestado não permite a retenção do ISS devido. Altere o indicador de retenção do ISSQN devido para 2 - sem retenção. |
| E290 | Erro | País do tomador do serviço não informado. | Quando a exigibilidade do ISSQN for <Exportação> informar o país do tomador do serviço. |
| E291 | Erro | País do tomador do serviço incorreto | Informe o código do país do tomador do serviço conforme consta da Tabela de Países do BACEN. |
| E292 | Erro | País do tomador do serviço indevido | O código do país do tomador do serviço somente deverá ser informado quando o município for igual a <9999999>. |
| E293 | Erro | Intermediário do serviço não informado. | Quando o campo Responsável/Retenção for igual a <2> a qualificação do intermediário do serviço é informação obrigatória. |
| E294 | Erro | CPF do intermediário do serviço informado indevidamente. | O intermediário do serviço deve ser pessoa jurídica. |
| E295 | Erro | Intermediário do serviço pessoa física não pode reter ISSQN | Para a informação <2> no campo Responsável/Retenção o intermediário do serviço tem de ser pessoa jurídica estabelecida e inscrita neste município. |
| E296 | Erro | CNPJ/CPF do intermediário do serviço não informado. | Informe o CNPJ/CPF do intermediário do serviço. |
| E297 | Erro | CNPJ/CPF do intermediário do serviço não consta da base de dados | O intermediário do serviço não foi encontrado na base de dados do município, |
| E298 | Erro | CNPJ/CPF do intermediário do serviço incorreto | Informe corretamente o CNPJ/CPF do intermediário do serviço. |
| E299 | Erro | Inscrição municipal do intermediário do serviço não informada. | Quando o CPF/CNPJ do intermediário do serviço estiver vinculado a mais de uma inscrição municipal esta informação é obrigatória. |
| E30 | Erro | Item da lista de serviço inexistente. | Consulte a legislação vigente para saber o item da lista de serviço que deverá ser informado. |
| E300 | Erro | Inscrição municipal do intermediário do serviço não consta da base de dados | O intermediário do serviço não foi encontrado na base de dados do município, |
| E301 | Erro | Inscrição municipal do intermediário do serviço incorreta | Informe corretamente a Inscrição Municipal do intermediário do serviço. |
| E302 | Erro | Código da obra não informado. | Para o serviço informado o código da obra é informação obrigatória. |
| E303 | Erro | Código da obra informada indevidamente. | O serviço informado não permite a informação do código da obra. |
| E304 | Erro | ART informada indevidamente. | O serviço informado não permite indicação de ART. |
| E305 | Erro | ART não informada. | Para o serviço informado a ART - Anotação de Responsabilidade Técnica - é informação obrigatória. |
| E306 | Erro | Tipo de exigibilidade do ISSQN não informado. | O tipo de exigibilidade do ISSQN é informação obrigatória. |
| E307 | Erro | Tipo de exigibilidade do ISSQN inválido. | O campo referente a exigibilidade do ISSQN deverá ter tamanho de 2 números inteiros. Consulte o Manual da NFS-e e verifique os códigos de exigibilidade do ISSQN. |
| E308 | Erro | Tipo de exigibilidade do ISSQN incorreto. | Consulte o Manual da NFS-e e verifique os códigos de exigibilidade do ISSQN. |
| E309 | Erro | Código do município de incidência do ISSQN inválido. | Informe o código do município de incidência do ISSQN com sete caracteres conforme Tabela de Municípios do IBGE. |
| E31 | Erro | Item da lista de serviço não informado. | Informe o item relativo ao serviço prestado nessa operação. |
| E310 | Erro | Código do município de incidência do ISSQN incorreto. | Informe o código do município da incidência do ISSQN conforme Tabela de Municípios do IBGE. |
| E311 | Erro | Código do município de incidência do ISSQN não informado | A informação do código do município da incidência do ISSQN é obrigatório quando <Exigibilidade do ISS> for tipo <1 - Exigível>, <6 - Exigibilidade Suspensa por Decisão Judicial> ou <7 - Exigibilidade Suspensa por Processo Administrativo>. Consulte a Tabela de Municípios do IBGE para preenchimento do campo. |
| E312 | Erro | Número do processo inválido. | Utilize no máximo 30 dígitos para informar o número do processo que suspenda a exigibilidade do ISSQN. |
| E313 | Erro | Número do processo informado indevidamente | Somente deverá ser informado o número do processo quando <Exigibilidade do ISS> for tipo <6 - Exigibilidade Suspensa por Decisão Judicial> ou <7 - Exigibilidade Suspensa por Processo Administrativo>. |
| E314 | Erro | Número do processo não informado. | A informação do número do processo é obrigatória quando <Exigibilidade do ISS> for tipo <6 - Exigibilidade Suspensa por Decisão Judicial> ou <7 - Exigibilidade Suspensa por Processo Administrativo>. |
| E315 | Erro | Número da NFS-e a cancelar inválido. | O número da NFS-e deve ser informado com no máximo 15 algarismos. |
| E316 | Erro | Número da NFS-e consultada não informado. | Informe o número da NFS-e a ser consultada. |
| E317 | Erro | Data final não informada. | A data informada deverá estar no formato DD/MM/AAAA e deve ser uma data válida. |
| E318 | Erro | Página inválida. | Informe o número da página utilizando no máximo seis algarismos. |
| E319 | Erro | Página não encontrada. | A página informada inexistente. |
| E32 | Erro | Código CNAE incompatível com opção SIMPLES Nacional. | Consulte a legislação vigente para saber se o código informado permite a opção pelo simples nacional. |
| E320 | Erro | Número da NFS-e inicial inválido. | O número da NFS-e inicial da faixa deve ser informado com no máximo 15 algarismos. |
| E321 | Erro | Número da NFS-e final inválido. | O número da NFS-e final da faixa deve ser informado com no máximo 15 algarismos. |
| E322 | Erro | Número da NFS-e final não informado. | Informe o número da NFS-e final da faixa de consulta. |
| E323 | Erro | NFS-e final não encontrada. | A NFS-e final da faixa não foi encontrada na base de dados. |
| E324 | Erro | Assinatura do RPS inválida. | O RPS deve conter assinatura digital vinculada a certificado digital padrão ICP Brasil, não revogado e não expirado. |
| E325 | Erro | Assinatura do lote de RPS inválida. | O lote de RPS deve conter assinatura digital vinculada a certificado digital padrão ICP Brasil, não revogado e não expirado. |
| E326 | Erro | Pedido de serviço não assinado. | O pedido de serviço deve conter assinatura digital vinculada a certificado digital padrão ICP Brasil, não revogado e não expirado. |
| E327 | Erro | O regime de tributação informado difere do registrado na Prefeitura. | Procure a Prefeitura para atualizar o regime de tributação no cadastro do contribuinte. |
| E328 | Erro | Opção pelo SIMPLES Nacional não consta do cadastro do contribuinte. | Não há registro no cadastro do contribuinte de opção pelo SIMPLES Nacional. Procure a Prefeitura para regularizar a situação. |
| E329 | Erro | Apenas serviços tributados podem sofrer retenção de ISSQN. | A retenção na fonte é permitida somente se o campo <Exigibilidade do ISS> for igual a <1 - Exigível>. |
| E33 | Erro | Código CNAE inexistente. | Consulte a legislação vigente para saber o código CNAE que deverá ser informado. |
| E330 | Erro | Contribuintes enquadrados como MEI do simples nacional não podem sofrer retenção de ISSQN. | Empresas enquadradas como MEI do simples nacional não devem sofrer retenção na fonte do ISSQN. |
| E331 | Erro | Contribuintes enquadrados como Microempresa Municipal não podem sofrer retenção de ISSQN. | Empresas enquadradas como Microempresa Municipal não devem sofrer retenção na fonte do ISSQN. |
| E332 | Erro | Contribuintes enquadrados como Estimativa não podem sofrer retenção de ISSQN. | Empresas enquadradas como Estimativa não devem sofrer retenção na fonte do ISSQN. |
| E333 | Erro | Contribuintes enquadrados como Sociedade de Profissionais não podem sofrer retenção de ISSQN. | Empresas enquadradas como Sociedade de Profissionais não devem sofrer retenção na fonte do ISSQN. |
| E334 | Erro | Contribuintes enquadrados como Incentivador Fiscal não podem sofrer retenção de ISSQN. | Empresas enquadradas como Incentivador Fiscal não devem sofrer retenção na fonte do ISSQN. |
| E335 | Erro | Empresas cadastradas com CPF não podem sofrer retenção de ISSQN. | Empresas cadastradas com CPF não devem sofrer retenção na fonte do ISSQN. |
| E336 | Erro | Inscrição municipal do tomador do serviço indevida. | Não informe o campo inscrição municipal do tomador do serviço quando este for do exterior. |
| E337 | Erro | Indicador de incentivo fiscal não informado. | Indicador de incentivo fiscal é informação obrigatória. |
| E338 | Erro | Indicador de incentivo fiscal invalido. | O campo Indicador de incentivo fiscal deverá ter tamanho de 1 número inteiro. |
| E339 | Erro | Indicador de incentivo fiscal incorreto. | Consulte o Manual da NFS-e e verifique os códigos de incentivo fiscal. |
| E34 | Erro | Código de tributação incompatível com opção SIMPLES Nacional. | Consulte a legislação vigente para saber se o código informado permite a opção pelo simples nacional. |
| E340 | Erro | Valor do ISSQN não informado. | Quando o ISSQN é devido a outro município, o valor do tributo deve ser calculado e informado pelo prestador do serviço. |
| E341 | Erro | Alíquota do ISSQN não informado. | Quando o ISSQN é devido a outro município, alíquota deve ser informada pelo prestador do serviço. |
| E342 | Erro | Informação de Incentivador Fiscal não confere com o cadastro da prefeitura. | Acerte o campo Incentivador Fiscal |
| E343 | Erro | Código de cancelamento incorreto | Consulte o Manual da NFS-e para saber os códigos de cancelamento permitidos pelo sistema. |
| E344 | Erro | Item da lista de serviço informado para a operação não permite tributação fora do município | Consulte a legislação vigente para saber se o item informado permite a tributação fora do município. |
| E345 | Erro | Inscrição Municipal do prestador do serviço não está vinculada ao CNPJ ou CPF informado. | A inscrição municipal informada não está vinculada ao prestador de serviço identificado pelo CNPJ ou CPF. |
| E346 | Erro | Inscrição Municipal do tomador do serviço não está vinculada ao CNPJ ou CPF informado. | A inscrição municipal do tomador do serviço não está vinculada ao CNPJ/CPF. |
| E347 | Erro | CNPJ/CPF do Prestador do serviço não autorizado a emitir NFS-e | Solicite autorização para emitir NFS-e para o CNPJ/CPF informado. |
| E348 | Erro | CNPJ/CPF informado no lote difere do CNPJ/CPF informado no RPS | CNPJ/CPF do prestador do serviço especificado no lote não confere com o prestador do serviço informado no RPS |
| E349 | Erro | Valor total dos tributos federais, estaduais e municipais maior que a base de cálculo | Valor total dos tributos federais, estaduais e municipais, em conformidade com o artigo 1o da Lei no 12.741/2012, deve ser menor ou igual à base de cálculo |
| E35 | Erro | Código de tributação inexistente. | Consulte a legislação vigente para saber o Código de tributação que deverá ser informado. |
| E350 | Erro | Valor total dos tributos federais, estaduais e municipai inválido. | O campo valor total dos tributos federais, estaduais e municipai é numérico e deverá ter tamanho máximo de 15 números inteiros e dois decimais. |
| E351 | Erro | Código NBS não informado | Informe o código NBS conforme padrão da Receita Federal do Brasil |
| E352 | Erro | Código NBS inválido. | Informe o código NBS com nove caracteres conforme padrão da Receita Federal do Brasil |
| E353 | Erro | NIF do Tomador não informado | Informe o NIF para tomadores não residentes no Brasil |
| E354 | Erro | NIF do Tomador inválido. | Informe o NIF do Tomador com quarenta caracteres. |
| E355 | Erro | Inscrição municipal do Tomador do serviço não informada. | Quando o CPF/CNPJ do intermediário do serviço estiver vinculado a mais de uma inscrição municipal esta informação é obrigatória. |
| E356 | Erro | Número da NFS-e inválido. | O número da NFS-e deve ser informado com no máximo 15 algarismos. |
| E357 | Erro | Inscrição municipal do Tomador do inválida. | Informar Inscrição Municipal do Tomador |
| E358 | Erro | Serviço inexistente. | Informar Código de serviço correto. |
| E359 | Erro | Prestador sem endereço informado. | Informar o endereço completo do prestador. |
| E36 | Erro | Informação de ISSQN retido inválido. | Utilize um dos tipos: 1 para ISS Retido ou 2 para ISSQN não Retido. |
| E360 | Erro | Erro ao emitir a nota. | Tente novamente mais tarde |
| E361 | Erro | Empresa não localizada. | A empresa não está cadastrada no município. |
| E362 | Erro | A nota fiscal não pode ser substituída. Prazo de substituição expirado. | Entre em contato com a prefeitura para realizar o cancelamento através de processo administrativo. |
| E363 | Erro | Dados inconsistentes. Trata-se de tomador substituto tributário estabelecido no Município. Favor verificar CNPJ, endereço/município e inscrição municipal | CNPJ está lançado como substituto tributário, favor verificar dados do tomador CNPJ, IM e Endereço |
| E364 | Erro | Valor da alíquota informado não condiz com a lista de serviços do município. | Informe o valor da alíquota correto. |
| E365 | Erro | Valor da alíquota informado não condiz com a lista de beneficios do município. | Informe o valor da alíquota com os beneficios. |
| E366 | Erro | Não existe regra de imposto para o serviço informado. | Informe um serviço que exista na regra. |
| E367 | Erro | Emissão com serviço eventual não permitido pelo município. | Informe um serviço que não seja eventual. |
| E368 | Erro | Este tomador é um substituto tributário. Para prestação de serviços à substitutos tributários é necessário a indicação do imposto retido. | Informe o campo ISSRetido com o valor 1, bem como o campo ValorISSRetido. |
| E369 | Erro | As funcionalidades: Emitir NFS-e, Substituir NFS-e e Cancelar NFS-e estão suspensas. | Para maiores informações entre em contato com a prefeitura |
| E370 | Erro | Não é permitido o uso de Outras Retenções no município. | Caso esteja utilizando RPS, remova a tag de Outras Retenções. Se a emissão da NFS-e estiver sendo executada OnLine, deixe o valor do campo em 0,00. |
| E371 | Erro | Conforme legislação municipal esta prestação de serviço deve ter o imposto retido. | Informe o campo ISSRetido com o valor 1, bem como o campo ValorISSRetido. |
| E372 | Erro | Não é permitido substituir a NFS-e devido a empresa estar em período de fiscalização na competência informada. | Favor contatar o órgão responsável. |
| E373 | Erro | O cnpj do tomador informado não está cadastrado na base de dados do município. | Informe o cpnj do tomador corretamente. |
| E374 | Erro | Prestador com dados cadastrais invalidos. Falta dados de endereço. | Contactar prefeitura para atualizar dados de endereço em seu cadastro. |
| E375 | Erro | Não é permitido emitir a NFS-e devido a empresa estar em período de fiscalização na competência informada. | Favor contatar o órgão responsável. |
| E376 | Erro | Não é permitido cancelar a NFS-e devido a empresa estar em período de fiscalização na competência informada. | Favor contatar o órgão responsável. |
| E377 | Erro | A NFS-e não pode ser cancelada, pois o tomador já realizou o aceite. | Para cancelamento da NFS-e será necessário o seu descarte pelo tomador de serviço. |
| E378 | Erro | Cancelamento não permitido porque NFSe possui vinculo a um encerramento. | Entrar em contato com a Prefeitura. |
| E379 | Erro | Para realizar o cancelamento entre em contato com a prefeitura. | Para realizar o cancelamento entre em contato com a prefeitura. |
| E380 | Erro | Local da prestação diferente do local da obra. | O local da prestação deve ser o mesmo do local da obra. |
| E381 | Erro | O valor de dedução excede o valor de abatimento liberado para a obra. | Verificar o valor de abatimento liberado na obra e corrigir o valor de dedução na nota. |
| E382 | Erro | Tag de Construção civil é obrigatória e não foi informada no arquivo. Insira a Tag de Construção Civil juntamente com o código da Obra e Código ART.O local da prestação deve ser o mesmo do local da obra. | Informe a Tag de Construção civil. |
| E383 | Erro | Código do pais não informado. | Infome o código do pais. |
| E384 | Erro | Código do pais informado não existe. | Infome um código de pais válido. |
| E385 | Erro | Não existe vinculo da atividade com o serviço. | Infome uma atividade que possua vinculo com o serviço. |
| E385 | Erro | Não existe serviço na lista de Issqn para o código informado. | Infome um código issqn válido. |
| E386 | Erro | Atividade informada no campo Código de tributacão do município não existe. | Infome uma atividade válida. |
| E388 | Erro | Cancelamento não permitido por estar em discordância com o prazo máximo estabelecido pela prefeitura. | Entre em contato com a prefeitura para realizar o cancelamento através de processo administrativo. |
| E389 | Erro | Lote nao Convertido | Retransmitir o lote |
| E39 | Erro | Apenas empresas inscritas neste município podem efetuar retenção de ISSQN. | O tomador do serviço não foi encontrado na base de dados do município, não sendo permitida a retenção. Acerte o CNPJ e/ou Inscrição Municipal ou altere o campo ISSQN Retido para 2 (Sem retenção de ISSQN). |
| E4 | Erro | Esse RPS não foi encontrado em nossa base de dados. | Informe um número de RPS que tenha sido convertido em NFS-e. |
| E41 | Erro | O campo discriminação dos serviços não foi preenchido. | O preenchimento da discriminação dos serviços é obrigatório. |
| E42 | Erro | Código do município da prestação do serviço inválido. | Informe o código do município onde foi prestado o serviço, conforme Tabela de Municípios do IBGE. |
| E43 | Erro | Inscrição Municipal do prestador do serviço não encontrada na base de dados do município. | Não existe registro de inscrição municipal que corresponda ao número informado. |
| E49 | Erro | Lote de RPS com excesso de inconsistências. | O serviço de validação de RPS é abortado quando atinge o número de 50 inconsistências. Corrija os erros e reenvie o lote de RPS. |
| E5 | Erro | O número da NFS-e a ser substituída não foi encontrada na base de dados. | Confira e informe novamente os dados da NFS-e que deseja substituir. |
| E50 | Erro | Inscrição Municipal do prestador do serviço incorreta. | Informe corretamente a inscrição municipal do prestador do serviço. |
| E51 | Erro | Inscrição Municipal do tomador do serviço incorreta. | Informe corretamente a inscrição municipal do tomador do serviço. |
| E52 | Erro | O tomador do serviço informado é o próprio prestador do serviço. | Na emissão da NFS-e não é permitido que o prestador do serviço seja igual ao tomador do serviço. |
| E53 | Erro | O campo Inscrição Municipal do tomador do serviço só deverá ser preenchido para tomadores estabelecidos neste município. | Para tomador de serviço estabelecido fora deste município não deve ser preenchido o campo Inscrição municipal. |
| E54 | Erro | CNPJ do tomador do serviço está vinculado a mais de uma inscrição municipal. | Informe a Inscrição Municipal do tomador do serviço vinculada ao CNPJ informado. |
| E55 | Erro | Logradouro do tomador do serviço não corresponde ao CEP informado. | Acerte o logradouro ou o CEP do tomador do serviço. |
| E56 | Erro | Logradouro do tomador do serviço não informado. | Informe o logradouro para o tomador do serviço. |
| E57 | Erro | Bairro não corresponde ao CEP informado. | Corrija o Bairro ou o CEP do tomador do serviço. |
| E58 | Erro | Código do município do tomador do serviço não corresponde ao CEP informado. | Corrija o código do município ou o CEP do tomador do serviço. |
| E59 | Erro | Município do tomador do serviço não informado. | Informe o município para o tomador do serviço. |
| E6 | Erro | Essa NFS-e não pode ser cancelada através desse serviço, pois há crédito informado. | O cancelamento de uma NFS-e com crédito deve ser feito através de processo administrativo aberto em uma repartição fazendária. |
| E60 | Erro | Município do tomador do serviço não encontrado. | Informe o código do município do tomador do serviço conforme consta da Tabela de Municípios do IBGE. Se o tomador do serviço for do exterior do país, informe "9999999". |
| E61 | Erro | UF do tomador do serviço não corresponde ao CEP informado. | Corrija a sigla da UF ou o CEP do tomador do serviço. |
| E62 | Erro | CEP do logradouro do tomador do serviço inexistente. | Informe corretamente o CEP do tomador do serviço. |
| E63 | Erro | Razão Social do intermediário do serviço não informada. | Razão social do intermediário do serviço é obrigatória quando informado o CNPJ/CPF ou a Inscrição Municipal do mesmo. |
| E64 | Erro | Inscrição Municipal do intermediário do serviço inválida. | Informe a Inscrição Municipal do intermediário do serviço com tamanho máximo de 15 caracteres. |
| E65 | Erro | Inscrição Municipal do intermediário do serviço não esta vinculada ao CNPJ/CPF informado. | A inscrição municipal informada não está vinculada ao intermediário do serviço identificado pelo CNPJ ou CPF. |
| E67 | Erro | Código da obra incorreto. | Informe corretamente o código da obra. |
| E68 | Erro | Situação do RPS inválida. | Utilize um dos tipos:1 ¿ Normal; 2 ¿ Cancelado. |
| E69 | Erro | Quantidade de RPS incorreta. | A quantidade de RPS informada difere da quantidade de RPS do lote. |
| E7 | Erro | Essa NFS-e já foi substituída. | A NFS-e só pode ser substituída uma vez. |
| E70 | Erro | Inscrição Municipal do prestador do serviço especificada no lote não confere com o prestador do serviço informado no RPS. | Informe corretamente a Inscrição Municipal do prestador do serviço no lote e no RPS. |
| E71 | Erro | RPS em duplicidade no arquivo enviado. | Retire do lote o RPS duplicado ou altere sua identificação: número, série ou tipo, e reenvie o lote. |
| E72 | Erro | Campo Quantidade de RPS informado incorretamente. | O campo quantidade de RPS é numérico e deverá ter tamanho máximo de 4 dígitos. |
| E76 | Erro | O número do RPS substituído informado não existe na base de dados. | Informe corretamente o número do RPS substituído. |
| E77 | Erro | Número da NFS-e não informado | Informe o número da NFS-e. |
| E78 | Erro | Número da NFS-e inexistente na base de dados para o prestador do serviço pesquisado. | Informe corretamente o número da NFS-e. |
| E79 | Erro | NFS-e já está cancelada. | Verifique e informe corretamente os dados da NFS-e que deseja cancelar. |
| E8 | Erro | Opção pelo simples nacional não informado. | Se o prestador do serviço for optante pelo SIMPLES Nacional, informe "1" (sim). Caso contrário, informe "2" (não). |
| E86 | Erro | Número do protocolo de recebimento do lote inexistente na base de dados. | Confira se o lote foi enviado e informe o número correto do protocolo de recebimento. |
| E87 | Erro | Número de lote inexistente na base de dados. | Confira se o lote foi enviado e informe o número correto. |
| E88 | Erro | Número de lote não informado. | Informe o número do lote. |
| E89 | Erro | Não existe na base de dados uma NFS-e emitida para o número de RPS informado. | Informe corretamente o número do RPS. |
| E90 | Erro | Número do RPS inválido. | Informe um número de RPS que corresponda à seqüência utilizada pelo prestador do serviço. |
| E91 | Erro | Esse RPS não consta em nossa base de dados. | Solicite do prestador do serviço a emissão da NFS-e. |
| E92 | Erro | RPS ainda não convertido em NFS-e. | Efetue nova consulta mais tarde. |
| E93 | Erro | Série do RPS inválida. | Identifique o RPS utilizando no máximo 15 dígitos. |
| E94 | Erro | Data de competência não informada. | Informe a data de competência no formato data. |
| E95 | Erro | Data de competência Inválida | Informe a data de competência no formato data. |
| E96 | Erro | Número do RPS incorreto. | Informe corretamente o número do RPS. |
| E97 | Erro | Série do RPS incorreta. | Informe corretamente a série para o RPS pesquisado. |
| E98 | Erro | Valor dos serviços não informado. | Informe o valor dos serviços. |
| E99 | Erro | Valor da retenção deverá ser inferior ou igual ao valor dos serviços. | Não é permitido valor de retenção superior ao valor de serviços. |
