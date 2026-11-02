# Rejeições: Betha

> Fonte: `_origem/Padrões/Betha/OBSERVAÇÕES.txt`. Observações do padrão: [Betha](../02-observacoes/observacoes-provedores.md#betha).

## Erros com causa e solução

| Código | Mensagem | Causa provável | Como resolver |
| :--- | :--- | :--- | :--- |
| E146 | Informação de optante pelo simples nacional inválida. | Simples Nacional enviado em desacordo com o cadastro do prestador no portal da prefeitura. | Envie o enquadramento de Simples Nacional conforme o cadastro do prestador no portal da prefeitura. |
| E999 | Alíquota efetiva da União para CBS incorreta. `ValorInformado == 0.90 && ValorCalculado == 0` | O envio de IBS/CBS depende de parametrização da prefeitura, que ainda não foi feita. | Não envie o campo `VersaoEsquema=RTC`, para que o grupo `<IBSCBS>` não seja gerado. Ver [IBS/CBS e parametrização da prefeitura](../04-base-conhecimento/artigos/ibs-cbs-parametrizacao-prefeitura.md). |

## Erros do webservice Betha (RPS)

> O Betha atual é leiaute nacional (DPS). Esta lista é do webservice de RPS (**legado**) e pode ainda aparecer em retornos da prefeitura. Para a DPS, use a [seção abaixo](#erros-da-dps-leiaute-nacional).

> **[pendente] Pendente:** na origem, a coluna Solução de todos os códigos abaixo aponta para "Artigo no link", mas o link não estava na documentação.

| Código | Descrição |
| :--- | :--- |
| Não possui | Faultstring Invalid byte 1 of 1-byte UTF-8 sequence. |
| 00001 | A data de emissão da nota fiscal é inferior à data de emissão do certificado. |
| 00002 | A data de emissão da nota fiscal é superior à data de emissão do certificado. |
| 00003 | Nota fiscal emitida fora da validade do certificado. |
| 00004 | O certificado da assinatura não é válido. |
| 00005 | Para assinar uma nota fiscal eletrônica, são permitidos somente certificados do tipo 'A1' ou 'A3'. |
| 00006 | Política de segurança do certificado não encontrada. |
| 00007 | Para assinar uma nota fiscal eletrônica, deve ser utilizado um certificado de entidade final. |
| 00008 | Assinatura inválida. |
| 00009 | O certificado utilizado para assinatura da nota fiscal eletrônica não é um certificado da ICP-Brasil ou Betha. |
| 00010 | O certificado utilizado para assinatura da nota fiscal eletrônica não é um certificado da ICP-Brasil. |
| 00011 | O e-CPF utilizado na assinatura da nota fiscal eletrônica não está autorizado pelo prestador. |
| 00012 | O e-CPF utilizado na assinatura está autorizado pelo prestador, porém está fora do período de validade. |
| 00013 | Para um prestador pessoa jurídica, serão aceitas somente notas assinadas com 'e-CNPJ' ou 'e-PJ'. |
| 00014 | Endereço do prestador do serviço não informado no cadastro de pessoas. |
| 00015 | O CEP do prestador do serviço não informado no cadastro de pessoas. |
| 00016 | O código do município do prestador do serviço não informado no cadastro de pessoas. |
| 00017 | O item da lista de serviços informado não consta no cadastro do prestador. |
| 00018 | O item da lista de serviços informado não é válido conforme a Lei 116/2003. |
| 1052 | O prestador deve ser uma pessoa jurídica. |
| 1107 | O CPF/CNPJ do prestador não confere com o CNPJ do certificado. |
| BTH01 | Alíquota inválida. |
| BTH02 | Alíquota inválida. |
| BTH03 | Lote de RPS já enviado. |
| BTH04 | O prestador do serviço não é do mesmo município da entidade. |
| BTH05 | Valor do ISS não informado. |
| BTH06 | XML do Lote de RPS não confere com o lote enviado ou pode estar corrompido. |
| BTH07 | Lote de RPS enviado não contém assinatura digital. |
| BTH08 | Tomador estrangeiro inválido. |
| BTH09 | O sistema foi configurado pela prefeitura para não permitir o cancelamento de notas fiscais. |
| BTH10 | Campo Outras Informações excede o limite de 255 caracteres. |
| BTH11 | Alíquota inválida. |
| BTH12 | E-mail inválido. |
| EB1 | Cancelamento da NFS-e não pode ser realizado por estar contida numa guia de pagamento. |
| EB2 | O atributo Id deve conter no máximo 255 caracteres. |
| L1 | NFS-e vinculada a uma guia não pode ser cancelada. |
| L2 | Quantidade de serviço inválida. |
| L3 | Nenhuma NFS-e foi encontrada na base de dados com os parâmetros de busca informados. |
| L4 | A identificação do Tomador ou a identificação do Intermediário deve ser informada. |
| L5 | A identificação do Tomador ou do Intermediário deve ser igual à identificação do Consulente. |
| L6 | Erro de estrutura. |
| L7 | Erro de validação de script. |
| L8 | RPS com série inválida. |
| L9 | RPS com série inválida. |
| L10 | Competência não identificada. |
| L11 | A NFS-e substituída e a NFS-e substituidora devem ser do mesmo prestador. |
| L12 | Erro de validação. |
| L13 | Assinatura digital inválida. O arquivo contém mais de um certificado digital. |
| L14 | A data de emissão da nota fiscal é inferior à data de emissão do certificado. |
| L15 | A data de emissão da nota fiscal é superior à data de validade do certificado. |
| L16 | Nota fiscal emitida fora da validade do certificado. |
| L17 | Faixa de enquadramento do SIMPLES não informada no cadastro deste prestador. |
| L18 | Código do município gerador do documento inválido para esta Prefeitura. |
| L19 | Atributo identificador da TAG a ser assinada não informado. |
| L999 | Tipo de pagamento inválido. |
| L998 | Tipo de pagamento repetido. |
| L997 | Número de parcelas inválido. |
| L996 | Valores divergentes. |
| L995 | Data de vencimento incorreta. |
| L994 | Tipos de pagamentos inválidos. |
| L993 | O número de identificação da parcela não foi informado. |
| L992 | Quantidade de parcela não informada. |
| L991 | Data de vencimento da parcela não informada ou data inválida. |
| L990 | Quantidade de parcela não informada. |
| L989 | Quantidade de parcelas diverge da quantidade de registros de parcelas. |
| L988 | O valor da parcela não foi informado. |

## Erros da DPS (leiaute nacional)

| Código | Mensagem | Orientação | Categoria |
| :--- | :--- | :--- | :--- |
| E001 | XML mal formado | Verifique a estrutura do XML | Estrutura XML |
| E001A | Protocolo ausente | Informe o protocolo | Estrutura XML |
| E002 | Assinatura digital ausente | Assine digitalmente a DPS antes de enviar | Estrutura XML |
| E003 | Certificado digital inválido | Utilize certificado ICP-Brasil válido | Estrutura XML |
| E004 | Certificado digital expirado | Renove o certificado digital | Estrutura XML |
| E005 | CNPJ/CPF do certificado não corresponde ao prestador | Utilize certificado do prestador ou matriz/filial | Estrutura XML |
| E006 | Campo chNFSe é obrigatório | Informe a chave de acesso da NFS-e | Estrutura XML |
| E007 | Campo chNFSe inválido | Verifique a chave de acesso informada | Estrutura XML |
| E008 | Campo CNPJAutor ou CPFAutor é obrigatório | Informe o CNPJ ou CPF do autor | Estrutura XML |
| E009 | Campo chSubstituta é obrigatório | Informe a chave da NFS-e substituta | Estrutura XML |
| E010 | Campo tpAmb é obrigatório | Informe 1 para Produção ou 2 para Homologação | Campos obrigatórios |
| E011 | Campo tpAmb inválido | Informe 1 para Produção ou 2 para Homologação | Campos obrigatórios |
| E012 | Campo tpEmit é obrigatório | Informe o tipo de emitente | Campos obrigatórios |
| E013 | Campo tpEmit inválido | Apenas tpEmit=1 (Prestador) é aceito | Campos obrigatórios |
| E013A | Campo chSubstituta inválida | Verifique a chave de acesso da substituta | Campos obrigatórios |
| E014 | Campo dhEmi é obrigatório | Informe a data/hora de emissão | Campos obrigatórios |
| E015 | Campo verAplic é obrigatório | Informe a versão do aplicativo | Campos obrigatórios |
| E015 | Campo verAplic inválido | O campo verAplic deve ter entre 1 e 20 caracteres | Campos obrigatórios |
| E0151 | Versão não informada | Informe a versão | Campos obrigatórios |
| E0152 | Versão está em branco | Informe a versão | Campos obrigatórios |
| E0153 | Versão deve conter apenas números e ponto (ex.: 1, 1.0, 1.23) | Revise a versão e retire caracteres inválidos | Campos obrigatórios |
| E0154 | Versão deve estar entre 0 e 9999 | Revise a versão | Campos obrigatórios |
| E0155 | O valor decimal da Versão deve estar entre 0 e 99 | Revise a versão | Campos obrigatórios |
| E015A | Campo cMotivo é obrigatório | Informe o código do motivo | Campos obrigatórios |
| E015B | Campo cMotivo inválido | Informe um código de motivo válido | Campos obrigatórios |
| E015C | Campo xMotivo é obrigatório | Informe a justificativa no campo xMotivo | Campos obrigatórios |
| E016 | Campo serie é obrigatório | Informe a série da DPS | Campos obrigatórios |
| E017 | Campo nDPS é obrigatório | Informe o número da DPS | Campos obrigatórios |
| E018 | Campo dCompet é obrigatório | Informe a data de competência | Campos obrigatórios |
| E019 | Campo cLocEmi é obrigatório | Informe o código da localidade emissora | Campos obrigatórios |
| E020 | Dados do prestador são obrigatórios | Informe os dados do prestador | Prestador |
| E021 | CNPJ ou CPF do prestador é obrigatório | Informe CNPJ ou CPF do prestador | Prestador |
| E021A | Dados do prestador inválidos | Verifique os dados informados | Prestador |
| E021B | Múltiplos campos de identificação encontrados | Escolha entre CPF, CNPJ ou NIF | Prestador |
| E022 | CNPJ do prestador inválido | CNPJ deve ter exatamente 14 dígitos | Prestador |
| E023 | CPF do prestador inválido | CPF deve ter exatamente 11 dígitos | Prestador |
| E023A | IM do prestador inválido | IM deve ter no máximo 15 caracteres | Prestador |
| E023B | Nome do prestador inválido | xNome deve ter no máximo 150 caracteres | Prestador |
| E023C | Telefone do prestador inválido | Fone deve ter entre 6 e 20 caracteres | Prestador |
| E023D | E-mail do prestador inválido | E-mail deve ter no máximo 80 caracteres | Prestador |
| E024 | Regime tributário é obrigatório | Informe o regime tributário do prestador | Prestador |
| E030 | Dados do serviço são obrigatórios | Informe os dados do serviço prestado | Serviço |
| E031 | Código de tributação nacional é obrigatório | Informe o código de tributação nacional | Serviço |
| E031A | Código de tributação municipal identificado, mas com padrão errado | O código de tributação municipal deve ter exatamente 3 caracteres | Serviço |
| E032 | Descrição do serviço é obrigatória | Informe a descrição do serviço | Serviço |
| E033 | Código NBS é obrigatório | Informe o código NBS | Serviço |
| E034 | Local de prestação é obrigatório | Informe o local de prestação do serviço | Serviço |
| E035 | Tipo de integração desconhecido | Verifique se o tipo de integração está correto | Serviço |
| E036 | Código IBGE inválido | O código IBGE tem 7 dígitos | Serviço |
| E040 | Valor do serviço é obrigatório | Informe o valor do serviço | Valores |
| E041 | Valor do serviço deve ser maior que zero | Informe um valor válido | Valores |
| E042 | Tributação do ISSQN é obrigatória | Informe a tributação do ISSQN | Valores |
| E050 | DPS já recepcionada anteriormente | O ID da DPS não pode ser reutilizado | Duplicidade |
| E051 | ID da DPS duplicado | O ID informado já existe | Duplicidade |
| E052 | Número e série da DPS duplicados | Esta combinação já foi utilizada | Duplicidade |
| E060 | Data de competência inválida | A data de competência não pode ser futura | Regra de negócio |
| E061 | Data de emissão inválida | A data de emissão não pode ser futura | Regra de negócio |
| E062 | Certificado não é ICP-Brasil | Utilize certificado ICP-Brasil A1 ou A3 | Regra de negócio |
| E064 | NFS-e não encontrada | A NFS-e informada não existe | Regra de negócio |
| E065 | NFS-e substituta não encontrada | A NFS-e substituta informada não existe | Regra de negócio |
| E070 | Não há nenhuma DPS com os dados utilizados | Verifique os dados novamente | Banco de dados |
| E080 | Finalidade da NFS-e inválida | Informe valores válidos | Grupo IBSCBS |
| E089 | Identificação do destinatário é obrigatória | Informe CNPJ, CPF ou NIF | Destinatário |
| E090 | CNPJ do tomador inválido | CNPJ deve ter exatamente 14 dígitos | Destinatário |
| E091 | CPF do tomador inválido | CPF deve ter exatamente 11 dígitos | Destinatário |
| E094 | Nome do destinatário é obrigatório | Informe o nome ou razão social | Destinatário |
| E099 | Código do país inválido | Informe o código ISO 3166 | Destinatário |
| E900 | O processo de cancelamento via WS está temporariamente indisponível | Será disponibilizado em breve | Genérico |
| E901 | O processo de substituição via WS está temporariamente indisponível | Será disponibilizado em breve | Genérico |
| E999 | Erro interno no processamento | Contate o suporte técnico | Genérico |
| E70 | Inscrição Municipal do prestador divergente | Informe a IM correta | Regra de negócio do RPS reaproveitada na DPS |
