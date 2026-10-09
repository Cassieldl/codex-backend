# Mapeamento De/Para: Provedor IPM x Tag InvoiCy

Este documento contém o mapeamento completo e consolidado de todos os campos (sequenciais 1 a 159) entre o layout do **Provedor IPM** e o sistema InvoiCy.

| Sequencial | TAG da prefeitura | Elemento XML | Dentro do grupo | Nome do campo (tag InvoiCy) | Ocorrência | Descrição | Interno / Externo |
| ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
| **1** | nfse | Grupo | 0 - | RPS | 1-1 | Lista Rps Recepção (Controle Geração) | Interno |
| **2** | identificador | Elemento | 1 - nfse | RPSNumero | 0-1 | Número do RPS | Externo |
| **3** | rps | Grupo | 1 - nfse |  | 0-1 |  | Interno |
| **4** | nro_recibo_provisorio | Elemento | 3 - rps | RPSNumero | 1-1 | Número do RPS | Externo |
| **5** | serie_recibo_provisorio | Elemento | 3 - rps | RPSSerie | 1-1 | Série do RPS | Externo |
| **6** | data_emissao_recibo_provisorio | Elemento | 3 - rps | dEmis | 1-1 | Data de Emissão | Externo |
| **7** | hora_emissao_recibo_provisorio | Elemento | 3 - rps | dEmis | 1-1 | Data de Emissão | Externo |
| **8** | nf | Grupo | 1 - nfse |  | 1-1 |  | Interno |
| **9** | valor_total | Elemento | 8 - nf | ValServicos | 1-1 | Valor do Serviço | Externo |
| **10** | valor_desconto | Elemento | 8 - nf | ValDescIncond | 1-1 | Valor do desconto incondicionado | Externo |
| **11** | valor_ir | Elemento | 8 - nf | ValIR | 1-1 | Valor do IRRF do serviço | Externo |
| **12** | valor_inss | Elemento | 8 - nf | ValINSS | 1-1 | Valor do INSS do serviço | Externo |
| **13** | valor_contribuicao_social | Elemento | 8 - nf | ValCSLL | 1-1 | Valor do CSLL | Externo |
| **14** | valor_rps | Elemento | 8 - nf |  | 1-1 |  | Interno |
| **15** | pis_cofins | Grupo | 8 - nf |  | 0-1 |  | Interno |
| **16** | cst | Elemento | 15 - pis_cofins | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo |
| **17** | tipo_retencao | Elemento | 15 - pis_cofins | tpRetPISCofins | 0-1 | Tipo de Retenção PIS/COFINS e CSLL | Externo |
| **18** | base_calculo | Elemento | 15 - pis_cofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| **19** | aliquota_pis | Elemento | 15 - pis_cofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| **20** | aliquota_cofins | Elemento | 15 - pis_cofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| **21** | valor_pis | Elemento | 8 - nf | ValPIS | 1-1 | Valor do PIS do serviço | Externo |
| **22** | valor_cofins | Elemento | 8 - nf | ValCOFINS | 1-1 | Valor do COFINS | Externo |
| **23** | observacao | Elemento | 8 - nf | NFSOutrasinformacoes | 1-1 | Outras Informações / observações | Externo |
| **26** | IBSCBS | Grupo | 8 - nf |  | 0-1 |  | Interno |
| **27** | cLocalidadeIncid | Elemento | 26 - IBSCBS | cLocalidadeIncid | 0-1 | Código IBGE da localidade de incidência do IBS/CBS (local da operação). | Externo |
| **28** | pRedutor | Elemento | 26 - IBSCBS | ValpRedutor | 0-1 | Percentual de redução compras governamentais | Externo |
| **29** | valores | Grupo | 26 - IBSCBS |  | 0-1 |  | Interno |
| **30** | vBC | Elemento | 29 - valores | ValBCIBSCBS | 1-1 | Valor da base de cálculo (BC) do IBS/CBS antes das reduções para cálculo do tributo bruto. | Externo |
| **31** | uf | Grupo | 29 - valores |  | 0-1 |  | Interno |
| **32** | pIBSUF | Elemento | 31 - uf | ValpIBSUF | 1-1 |  | Externo |
| **33** | pRedAliqUF | Elemento | 31 - uf | ValRedAliqUF | 1-1 | Percentual de redução de alíquota estadual. | Externo |
| **34** | pAliqEfetUF | Elemento | 31 - uf | ValpAliqEfetUF | 1-1 | pAliqEfetUF = pIBSUF x pRedAliqUF | Externo |
| **35** | mun | Grupo | 29 - valores |  | 0-1 |  | Interno |
| **36** | pIBSMun | Elemento | 35 - mun | ValIBSTot | 1-1 | Alíquota do IBS de competência do Município | Externo |
| **37** | pRedAliqMun | Elemento | 35 - mun | ValpRedAliqMun | 1-1 | Percentual de redução de alíquota Municipal | Externo |
| **38** | pAliqEfetMun | Elemento | 35 - mun | ValpAliqEfetMun | 1-1 | pAliqEfetMun = pIBSMun x pRedAliqMun | Externo |
| **39** | fed | Grupo | 29 - valores |  | 0-1 |  | Interno |
| **40** | pCBS | Elemento | 39 - fed | ValpCBS | 1-1 | Alíquota da UF para CBS da localidade de incidência | Externo |
| **41** | pRedAliqCBS | Elemento | 39 - fed | ValpRedAliqCBS | 1-1 | Percentual da redução de alíquota CBS | Externo |
| **42** | pAliqEfetCBS | Elemento | 39 - fed | ValpAliqEfetCBS | 1-1 | pAliqEfetCBS = pCBS x pRedAliqCBS | Externo |
| **43** | totCIBS | Grupo | 26 - IBSCBS |  | 0-1 |  | Interno |
| **44** | vTotNF | Elemento | 43 - totCIBS | ValTotNF | 1-1 | "Valor Total da NF considerando os impostospor fora: IBS e CBS.O IBS e a CBS são por fora" | Externo |
| **45** | gTribRegular | Grupo | 43 - totCIBS |  | 1-1 |  | Interno |
| **46** | pAliqEfeRegIBSUF | Elemento | 45 - gTribRegular | ValpAliqEfeRegIBSUF | 1-1 | Alíquota efetiva de tributação regular do IBS estadual | Externo |
| **47** | vTribRegIBSUF | Elemento | 45 - gTribRegular | ValTribRegIBSUF | 1-1 | "Valor da tributação regular do IBS estadual.vTribRegIBSUF = vBC x pAliqEfeRegIBSUF" | Externo |
| **48** | pAliqEfeRegIBSMun | Elemento | 45 - gTribRegular | ValpAliqEfeRegIBSMun | 1-1 | Alíquota efetiva de tributação regular do IBS municipal | Externo |
| **49** | vTribRegIBSMun | Elemento | 45 - gTribRegular | ValTribRegIBSMun | 1-1 | "Valor da tributação regular do IBS municipal.vTribRegIBSMun = vBC x pAliqEfeRegIBSMun" | Externo |
| **50** | pAliqEfeRegCBS | Elemento | 45 - gTribRegular | ValpAliqEfeRegCBS | 1-1 | Alíquota efetiva de tributação regular da CBS | Externo |
| **51** | vTribRegCBS | Elemento | 45 - gTribRegular | ValTribRegCBS | 1-1 | Valor da tributação regular da CBS | Externo |
| **52** | gTribCompraGov | Grupo | 43 - totCIBS |  | 1-1 |  | Interno |
| **53** | pIBSUF | Elemento | 52 - gTribCompraGov | ValpIBSUF_CompraGov | 1-1 | Alíquota do IBS de competência do Estado | Externo |
| **54** | vIBSUF | Elemento | 52 - gTribCompraGov | ValIBSUF_CompraGov | 1-1 | Valor do Tributo do IBS da UF calculated | Externo |
| **55** | pIBSMun | Elemento | 52 - gTribCompraGov | ValpIBSMun_CompraGov | 1-1 | Alíquota do IBS de competência do Município | Externo |
| **56** | vIBSMun | Elemento | 52 - gTribCompraGov | ValIBSMun_CompraGov | 1-1 | Valor do Tributo do IBS do Município calculado | Externo |
| **57** | pCBS | Elemento | 52 - gTribCompraGov | ValpCBS_CompraGov | 1-1 | Alíquota da CBS | Externo |
| **58** | vCBS | Elemento | 52 - gTribCompraGov | ValCBS_CompraGov | 1-1 | Valor do Tributo da CBS calculado | Externo |
| **59** | gIBS | Grupo | 43 - totCIBS |  | 0-1 |  | Interno |
| **60** | vIBSTot | Elemento | 59 - gIBS | ValIBSTot | 1-1 | "Valor total do IBS. | Externo |
| **61** | gIBSCredPres | Grupo | 59 - gIBS |  | 0-1 |  | Interno |
| **62** | pCredPresIBS | Elemento | 61 - gIBSCredPres | ValpCredPresIBS | 1-1 | Alíquota do crédito presumido para o IBS | Externo |
| **63** | vCredPresIBS | Elemento | 61 - gIBSCredPres | ValCredPresIBS | 1-1 | "Valor do Crédito Presumido para o IBS | Externo |
| **64** | gIBSUFTot | Grupo | 59 - gIBS |  | 0-1 |  | Interno |
| **65** | vDifUF | Elemento | 64 - gIBSUFTot | ValDifUF | 1-1 | "Total do Diferimento do IBS estadual. | Externo |
| **66** | vIBSUF | Elemento | 64 - gIBSUFTot | ValIBSUF | 1-1 | "Total valor do IBS estadual. | Externo |
| **67** | gIBSMunTot | Grupo | 59 - gIBS |  | 0-1 |  | Interno |
| **68** | vDifMun | Elemento | 67 - gIBSMunTot | ValDifMun | 1-1 | "Total do Diferimento do IBS municipal. | Externo |
| **69** | vIBSMun | Elemento | 67 - gIBSMunTot | ValIBSMun | 1-1 | "Total valor do IBS municipal. | Externo |
| **70** | gCBS | Grupo | 43 - totCIBS |  | 0-1 |  | Interno |
| **71** | gCBSCredPres | Grupo | 70 - gCBS |  | 0-1 |  | Interno |
| **72** | pCredPresCBS | Elemento | 71 - gCBSCredPres | ValpCredPresCBS | 1-1 | Alíquota do crédito presumido para a CBS | Externo |
| **73** | vCredPresCBS | Elemento | 71 - gCBSCredPres | ValCredPresCBS | 1-1 | "Valor do Crédito Presumido da CBS. | Externo |
| **74** | vDifCBS | Elemento | 70 - gCBS | ValDifCBS | 1-1 | Total do Diferimento CBS. (vBC x pDifCBS) | Externo |
| **75** | vCBS | Elemento | 70 - gCBS | ValCBS | 1-1 | "Total valor da CBS da União. | Externo |
| **76** | IBSCBS | Grupo | 1 - nfse |  | 0-1 |  | Interno |
| **77** | finNFSe | Elemento | 76 - IBSCBS | finNFSe | 1-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| **78** | indFinal | Elemento | 76 - IBSCBS | indFinal | 1-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo |
| **79** | cIndOp | Elemento | 76 - IBSCBS | cIndOp | 1-1 | Código indicador da operação de fornecimento, conforme tabela “código indicador de operação | Externo |
| **80** | tpEnteGov | Elemento | 76 - IBSCBS | tpEnteGov | 0-1 | Tipo de ente governamental Para administração pública direta e suas autarquias e fundações: | Externo |
| **81** | xTpEnteGov | Elemento | 76 - IBSCBS | xTpEnteGov | 0-1 | xTpEnteGov | Externo |
| **82** | imovel | Grupo | 76 - IBSCBS |  | 0-1 |  | Interno |
| **83** | insclmobFisc | Elemento | 82 - imovel | ImovInscImobFisc | 0-1 |  | Externo |
| **84** | CCIB | Elemento | 82 - imovel | ImovCIB | 0-1 | Código do Cadastro Imobiliário Brasileiro - CIB | Externo |
| **85** | end | Grupo | 82 - imovel |  | 0-1 |  | Interno |
| **86** | CEP | Elemento | 85 - end | ImovCEP | 0-1 | Código de Endereçamento Postal numérico doendereço nacional do imóvel. | Externo |
| **87** | endExt | Grupo | 85 - end |  | 0-1 |  | Interno |
| **88** | cEndPost | Elemento | 87 - endExt |  | 0-1 |  | Interno |
| **89** | xCidade | Elemento | 87 - endExt | ImovxMun | 0-1 | Nome da cidade no exterior, local do imóvel. | Externo |
| **90** | xEstProvReg | Elemento | 87 - endExt | ImovEstProvReg | 0-1 | Estado, província ou região da cidade no exterior,local do imóvel. | Externo |
| **91** | xLgr | Elemento | 85 - end | ImovEndereco | 0-1 | Tipo e nome do logradouro do endereço do imóve | Externo |
| **92** | nro | Elemento | 85 - end | ImovNumero | 0-1 | Número no logradouro do endereço do imóvel. | Externo |
| **93** | xCpl | Elemento | 85 - end | ImovComplemento | 0-1 | Complemento do endereço do imóvel. | Externo |
| **94** | xBairro | Elemento | 85 - end | ImovBairro | 0-1 | Bairro do endereço do imóvel. | Externo |
| **95** | valores | Grupo | 76 - IBSCBS |  | 0-1 |  | Interno |
| **96** | trib | Grupo | 95 - valores |  | 0-1 |  | Interno |
| **97** | gIBSCBS | Grupo | 96 - trib |  | 0-1 |  | Interno |
| **98** | CST | Elemento | 97 - gIBSCBS | CSTIBSCBS | 1-1 | Código de Situação Tributária do IBS e da CBS | Externo |
| **99** | cClassTrib | Elemento | 97 - gIBSCBS | cClassTrib | 1-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo |
| **100** | cCredPres | Elemento | 97 - gIBSCBS | cCredPres | 0-1 | Código e classificação do crédito presumido: IBS e CBS | Externo |
| **101** | gTribRegular | Grupo | 97 - gIBSCBS |  | 0-1 |  | Interno |
| **102** | CSTReg | Elemento | 101 - gTribRegular | CSTReg | 0-1 | "Código de Situação Tributária do | Externo |
| **103** | cClassTribReg | Elemento | 101 - gTribRegular | cClassTribReg | 0-1 | "Código da Classificação Tributária do | Externo |
| **104** | gDif | Grupo | 97 - gIBSCBS |  | 0-1 |  | Interno |
| **105** | pDifUF | Elemento | 104 - gDif | ValpDifUF | 1-1 | Percentual de diferimento para o IBS estadual | Externo |
| **106** | pDifMun | Elemento | 104 - gDif | ValpDifMun | 1-1 | Percentual de diferimento para o IBS municipal | Externo |
| **107** | pDifCBS | Elemento | 104 - gDif | ValpDifCBS | 1-1 | Percentual de diferimento para a CBS | Externo |
| **108** | prestador | Grupo | 1 - nfse |  | 1-1 |  | Interno |
| **109** | cpfcnpj | Elemento | 108 - prestador | CNPJ_prest | 1-1 | Cnpj do Prestador | Externo |
| **110** | cidade | Elemento | 108 - prestador | cMun | 1-1 | Código do Município do prestador | Externo |
| **111** | tomador | Grupo | 1 - nfse |  | 1-1 |  | Interno |
| **112** | endereco_informado | Elemento | 111 - tomador | TomaEnderecoInformado | 0-1 | Tomador do serviço seja o informado no arquivo xml - IPM | Externo |
| **113** | tipo | Elemento | 111 - tomador | TipoPessoaTomador | 1-1 | Tipo de Tomador (fisico, juridico, exterior) | Interno |
| **114** | cpfcnpj | Elemento | 111 - tomador | TomaCNPJ e TomaCPF | 1-1 | CPF ou CNPJ do Tomador | Externo |
| **115** | ie | Elemento | 111 - tomador | TomaIE | 1-1 | Inscrição Estadual do Tomador | Externo |
| **116** | nome_razao_social | Elemento | 111 - tomador | TomaRazaoSocial | 1-1 | Razão Social do Tomador | Externo |
| **117** | sobrenome_nome_fantasia | Elemento | 111 - tomador | TomaRazaoSocial | 1-1 | Razão Social do Tomador | Externo |
| **118** | logradouro | Elemento | 111 - tomador | TomaEndereco | 1-1 | Endereço do Tomador | Externo |
| **119** | email | Elemento | 111 - tomador | TomaEmail | 1-1 | Email do Tomador | Externo |
| **120** | numero_residencia | Elemento | 111 - tomador | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| **121** | complemento | Elemento | 111 - tomador | TomaComplemento | 1-1 | Complemento do Endereço do Tomador | Externo |
| **122** | ponto_referencia | Elemento | 111 - tomador | TomaComplemento | 1-1 | Complemento do Endereço do Tomador | Externo |
| **123** | bairro | Elemento | 111 - tomador | TomaBairro | 1-1 | Bairro do Tomador | Externo |
| **124** | cidade | Elemento | 111 - tomador | TomacMun | 1-1 | Codigo do Municipio IBGE do Tomador | Externo |
| **125** | cep | Elemento | 111 - tomador | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| **126** | ddd_fone_comercial | Elemento | 111 - tomador | TomaTelefone | 1-1 | DDD Tomador | Externo |
| **127** | fone_comercial | Elemento | 111 - tomador | TomaTelefone | 1-1 | Telefone do Tomador | Externo |
| **128** | ddd_fone_residencial | Elemento | 111 - tomador |  | 1-1 |  | Interno |
| **129** | fone_residencial | Elemento | 111 - tomador |  | 1-1 |  | Interno |
| **130** | ddd_fax | Elemento | 111 - tomador |  | 1-1 |  | Interno |
| **131** | fone_fax | Elemento | 111 - tomador |  | 1-1 |  | Interno |
| **132** | itens | Grupo | 1 - nfse |  | 1-1 |  | Interno |
| **133** | lista | Grupo | 132 - itens | ListaItens | 0-N | Lista de ítens da RPS Recepção (Controle Geração) | Interno |
| **134** | codigo_local_prestacao_servico | Elemento | 133 - lista | cMun | 1-1 | Código do Município do Serviço | Externo |
| **135** | codigo_item_lista_servico | Elemento | 133 - lista | ItemIteListServico | 1-1 | código da lista de servico LC No 116/2003 do Item | Externo |
| **136** | codigo_nbs | Elemento | 133 - lista | CodigoNBS | 1-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo |
| **137** | descritivo | Elemento | 133 - lista | ItemDesc | 1-1 | Descrição do Item | Externo |
| **138** | aliquota_item_lista_servico | Elemento | 133 - lista | ItemAliquota | 0-1 | Alíquota de ISS do Item | Externo |
| **139** | situacao_tributaria | Elemento | 133 - lista | natOp | 1-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| **140** | valor_tributavel | Elemento | 133 - lista | ItemVlrTotal | 0-1 | Valor Total do Item | Externo |
| **141** | valor_deducao | Elemento | 133 - lista | ItemvlDed | 0-1 | Valor da Dedução do Item | Externo |
| **142** | valor_issrf | Elemento | 133 - lista | ItemvlrISSRetido | 0-1 | Valor do ISS Retido do Item | Externo |
| **143** | cno | Elemento | 133 - lista | CodObra | 0-1 | Código da Obra | Externo |
| **144** | tributa_municipio_prestador | Elemento | 133 - lista | RespRetencao | 1-1 | Responsável pela Retenção | Externo |
| **145** | unidade_codigo | Elemento | 133 - lista | ItemuMed | 1-1 | Unidade de Medida do Item | Externo |
| **146** | unidade_quantidade | Elemento | 133 - lista | ItemQtde | 1-1 | Quantidade do Item | Externo |
| **147** | unidade_valor_unitario | Elemento | 133 - lista | ItemvUnit | 1-1 | Valor Unitário do Item | Externo |
| **148** | genericos | Elemento | 1 - nfse | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| **149** | produtos | Elemento | 1 - nfse |  | 1-1 |  | Interno |
| **150** | identificador | Elemento | 111 - tomador | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| **151** | estado | Elemento | 111 - tomador | TomaUF | 0-1 | UF (sigla) do estado do Tomador (Nacional) | Externo |
| **152** | pais | Elemento | 111 - tomador | TomaPais | 0-1 | Nome do País do Tomador | Interno |
| **153** | forma_pagamento | Grupo | 1 - nfse |  | 0-1 |  | Interno |
| **154** | tipo_pagamento | Elemento | 153 - forma_pagamento | fPagamento | 0-1 | Forma De Pagamento | Externo |
| **155** | parcelas | Grupo | 153 - forma_pagamento |  | 0-1 |  | Interno |
| **156** | parcela | Grupo | 155 - parcelas | ListaParcelas | 0-1 | Lista de parcelas da RPS Recepção (Controle Geração) | Interno |
| **157** | numero | Elemento | 156 - parcela | PrcSequencial | 0-1 | Sequencial/Número da Parcela | Externo |
| **158** | valor | Elemento | 156 - parcela | PrcValor | 0-1 | Valor da Parcela | Externo |
| **159** | data_vencimento | Elemento | 156 - parcela | PrcDtaVencimento | 0-1 | Data de vencimento da parcela | Externo |

*Observação: Os sequenciais 24 e 25 estiveram ausentes no log original e foram omitidos mantendo a numeração exata extraída.*