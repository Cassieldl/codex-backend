# Mapeamento De/Para: Provedor Elotech x Tag InvoiCy

Este documento contém o mapeamento completo e consolidado de todos os campos (sequenciais 1 a 168) entre o layout do **Provedor Elotech** e o sistema InvoiCy.

| Sequencial | TAG da prefeitura | Elemento XML | Dentro do grupo | Nome do campo (tag InvoiCy) | Ocorrência | Descrição | Interno / Externo | 
| ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | 
| **1** | EnviarLoteRpsSincronoEnvio | Grupo | 0 - |  | 1-1 |  | Interno | 
| **2** | IdentificacaoRequerente | Grupo | 1 - EnviarLoteRpsSincronoEnvio |  | 1-1 |  | Interno | 
| **3** | CpfCnpj | Grupo | 2 - IdentificacaoRequerente |  | 1-1 |  | Interno | 
| **4** | Cpf | Elemento | 3 - CpfCnpj |  | 1-1 |  | Interno | 
| **5** | Cnpj | Elemento | 3 - CpfCnpj | CNPJ_prest | 1-1 | Cnpj do Prestador | Externo | 
| **6** | InscricaoMunicipal | Elemento | 2 - IdentificacaoRequerente | IM | 1-1 | Inscrição Municipal do Prestador | Externo | 
| **7** | Senha | Elemento | 2 - IdentificacaoRequerente | EmpSenhaAutent | 1-1 | Senha de Autenticação da Empresa | Interno | 
| **8** | Homologa | Elemento | 2 - IdentificacaoRequerente | tpAmb | 1-1 | Ambiente Emissão | Interno | 
| **9** | LoteRps | Grupo | 1 - EnviarLoteRpsSincronoEnvio |  | 1-1 |  | Interno | 
| **10** | versao | Atributo | 9 - LoteRps |  | 0-1 |  | Interno | 
| **11** | NumeroLote | Elemento | 9 - LoteRps | NumeroLote | 1-1 | Número do Lote | Interno | 
| **12** | CpfCnpj | Grupo | 9 - LoteRps |  | 0-1 |  | Interno | 
| **13** | Cnpj | Elemento | 12 - CpfCnpj | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo | 
| **14** | InscricaoMunicipal | Elemento | 9 - LoteRps | IM | 0-1 | Inscrição Municipal do Prestador | Externo | 
| **15** | QuantidadeRps | Elemento | 9 - LoteRps | QuantidadeRps | 0-1 | Quantidade de RPS no Lote | Interno | 
| **16** | ListaRps | Grupo | 9 - LoteRps |  | 1-1 |  | Interno | 
| **19** | Rps | Grupo | 16 - ListaRps | RPS | 1-N | Lista Rps Recepção (Controle Geração) | Interno | 
| **20** | InfDeclaracaoPrestacaoServico | Grupo | 19 - Rps |  | 1-1 |  | Interno | 
| **21** | Rps | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 1-1 |  | Interno | 
| **22** | IdentificacaoRps | Grupo | 21 - Rps |  | 1-1 |  | Interno | 
| **23** | Numero | Elemento | 22 - IdentificacaoRps | RPSNumero | 1-1 | Número do RPS | Externo | 
| **24** | Serie | Elemento | 22 - IdentificacaoRps | RPSSerie | 1-1 | Série do RPS | Externo | 
| **25** | Tipo | Elemento | 22 - IdentificacaoRps | RPSTipo | 1-1 | 1 – Recibo Provisório de Serviços | Externo | 
| **26** | DataEmissao | Elemento | 21 - Rps | dEmis | 1-1 | Data de Emissão | Externo | 
| **27** | Status | Elemento | 21 - Rps | Status | 1-1 | Status do RPS | Externo | 
| **28** | RpsSubstituido | Grupo | 21 - Rps |  | 0-1 |  | Interno | 
| **29** | Numero | Elemento | 28 - RpsSubstituido | SubsNumero | 0-1 | Número do RPS Substituído | Externo | 
| **30** | Serie | Elemento | 28 - RpsSubstituido | SubsSerie | 0-1 | Série do RPS Substituído | Externo | 
| **31** | Tipo | Elemento | 28 - RpsSubstituido | SubsTipo | 0-1 | Tipo do RPS Substituído | Externo | 
| **32** | Competencia | Elemento | 20 - InfDeclaracaoPrestacaoServico | dCompetencia | 1-1 | data de Competência | Externo | 
| **33** | Servico | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 1-1 |  | Interno | 
| **34** | Valores | Grupo | 33 - Servico |  | 1-1 |  | Interno | 
| **35** | ValorServicos | Elemento | 34 - Valores | ValServicos | 1-1 | Valor do Serviço | Externo | 
| **36** | ValorDeducoes | Elemento | 34 - Valores | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo | 
| **37** | AliquotaPis | Elemento | 34 - Valores | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo | 
| **38** | RetidoPis | Elemento | 34 - Valores | PISRetido | 0-1 | PIS é retido? | Externo | 
| **39** | ValorPis | Elemento | 34 - Valores | ValPIS | 0-1 | Valor do PIS do serviço | Externo | 
| **40** | AliquotaCofins | Elemento | 34 - Valores | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo | 
| **41** | RetidoCofins | Elemento | 34 - Valores | COFINSRetido | 0-1 | COFINS é retido? | Externo | 
| **42** | ValorCofins | Elemento | 34 - Valores | ValCOFINS | 0-1 | Valor do COFINS | Externo | 
| **43** | AliquotaInss | Elemento | 34 - Valores | ValAliqINSS | 0-1 | Alíquota INSS em formato decimal | Externo | 
| **44** | RetidoInss | Elemento | 34 - Valores | INSSRetido | 0-1 | INSS é retido? | Externo | 
| **45** | ValorInss | Elemento | 34 - Valores | ValINSS | 0-1 | Valor do INSS do serviço | Externo | 
| **46** | AliquotaIr | Elemento | 34 - Valores | ValAliqIR | 0-1 | Alíquota IR em formato decimal | Externo | 
| **47** | RetidoIr | Elemento | 34 - Valores | IRRetido | 0-1 | IR é retido? | Externo | 
| **48** | ValorIr | Elemento | 34 - Valores | ValIR | 0-1 | Valor do IRRF do serviço | Externo | 
| **49** | AliquotaCsll | Elemento | 34 - Valores | ValAliqCSLL | 0-1 | Alíquota CSLL em formato decimal | Externo | 
| **50** | RetidoCsll | Elemento | 34 - Valores | CSLLRetido | 0-1 | CSLL é retido? | Externo | 
| **51** | ValorCsll | Elemento | 34 - Valores | ValCSLL | 0-1 | Valor do CSLL | Externo | 
| **52** | AliquotaCpp | Elemento | 34 - Valores | ValAliqCpp | 0-1 | Valor da alíquota do CPP | Externo | 
| **53** | RetidoCpp | Elemento | 34 - Valores | CppRetido | 0-1 | CPP é retido? | Externo | 
| **54** | ValorCpp | Elemento | 34 - Valores | ValCpp | 0-1 | Valor do CPP | Externo | 
| **55** | OutrasRetencoes | Elemento | 34 - Valores | ValOutrasRetencoes | 0-1 | Valor de Outras Retenções do serviço | Externo | 
| **56** | RetidoOutrasRetencoes | Elemento | 34 - Valores | OutrasRetencoesRetido | 0-1 | Outras retenções é retido? | Externo | 
| **57** | AliquotaTotTributos | Elemento | 34 - Valores | ValAliqTotTributos | 0-1 | Valor da alíquota do total de tributos | Externo | 
| **58** | ValTotTributos | Elemento | 34 - Valores | SomaTributos | 0-1 | Soma Tributos (Federais) | Interno | 
| **59** | ValorIss | Elemento | 34 - Valores |  | 0-1 |  | Interno | 
| **60** | Aliquota | Elemento | 34 - Valores | ValAliqISS | 0-1 | Alíquota do ISS | Externo | 
| **61** | DescontoIncondicionado | Elemento | 34 - Valores | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo | 
| **62** | DescontoCondicionado | Elemento | 34 - Valores | ValDescCond | 0-1 | Valor desconto Condicionado | Externo | 
| **63** | CSTPisCofins | Elemento | 34 - Valores | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo | 
| **64** | IssRetido | Elemento | 33 - Servico | ISSRetido | 1-1 | É ISS Retido? | Externo | 
| **65** | ResponsavelRetencao | Elemento | 33 - Servico | RespRetencao | 0-1 | Responsável pela Retenção | Externo | 
| **66** | Discriminacao | Elemento | 33 - Servico | Discriminacao | 1-1 | Descrição do Serviço | Externo | 
| **67** | CodigoTributacaoMunicipio | Elemento | 33 - Servico | TributMunicipio | 0-1 | Código de Tributação do Município | Externo | 
| **68** | CodigoNbs | Elemento | 33 - Servico | CodigoNBS | 0-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo | 
| **69** | CodigoMunicipio | Elemento | 33 - Servico | cMun | 1-1 | Código do Município do Serviço | Externo | 
| **70** | CodigoPais | Elemento | 33 - Servico | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo | 
| **71** | ExigibilidadeISS | Elemento | 33 - Servico | natOp | 0-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo | 
| **72** | MunicipioIncidencia | Elemento | 33 - Servico | cMunIncidencia | 0-1 | Codigo do Municipio IBGE de Incidência do imposto | Externo | 
| **73** | NumeroProcesso | Elemento | 33 - Servico | NumProcesso | 0-1 | Número do Processo Administrativo | Externo | 
| **74** | ListaItensServico | Grupo | 33 - Servico |  | 1-1 |  | Interno | 
| **75** | ItemServico | Grupo | 74 - ListaItensServico | ListaItens | 1-N | Lista de ítens da RPS Recepção (Controle Geração) | Interno | 
| **76** | ItemListaServico | Elemento | 75 - ItemServico | ItemIteListServico | 1-1 | código da lista de servico LC No 116/2003 do Item | Externo | 
| **77** | CodigoCnae | Elemento | 75 - ItemServico | ItemcCnae | 1-1 | CNAE - Classificação Nacional de Atividades Econômicas do item | Externo | 
| **78** | Descricao | Elemento | 75 - ItemServico | ItemDesc | 1-1 | Descrição do Item | Externo | 
| **79** | Tributavel | Elemento | 75 - ItemServico | ItemTributavel | 1-1 | Item Tributável | Externo | 
| **80** | Quantidade | Elemento | 75 - ItemServico | ItemQtde | 1-1 | Quantidade do Item | Externo | 
| **81** | ValorUnitario | Elemento | 75 - ItemServico | ItemvUnit | 1-1 | Valor Unitário do Item | Externo | 
| **82** | ValorDesconto | Elemento | 75 - ItemServico | ItemvDesconto | 0-1 | Valor do Desconto do Item | Externo | 
| **83** | ValorLiquido | Elemento | 75 - ItemServico | ItemVlrLiquido | 1-1 | Valor líquido do item da nota | Externo | 
| **84** | DadosDeducao | Grupo | 75 - ItemServico |  | 0-1 |  | Interno | 
| **85** | TipoDeducao | Elemento | 84 - DadosDeducao | ItemDedTipo | 0-1 | Tipo da dedução do item | Externo | 
| **86** | Cpf | Elemento | 84 - DadosDeducao | ItemDedCPFRef | 0-1 | CPF da nota de referência do item | Externo | 
| **87** | Cnpj | Elemento | 84 - DadosDeducao | ItemDedCNPJRef | 0-1 | CNPJ da nota de referência do item | Externo | 
| **88** | NumeroNotaFiscalReferencia | Elemento | 84 - DadosDeducao | ItemDedNFRef | 0-1 | NF de referência do item da nota | Externo | 
| **89** | ValorTotalNotaFiscal | Elemento | 84 - DadosDeducao | ItemDedvlTotRef | 0-1 | Valor da nota de referência | Externo | 
| **90** | PercentualADeduzir | Elemento | 84 - DadosDeducao | ItemDedPer | 0-1 | Percentual de Dedução do item da nota | Externo | 
| **91** | ValorADeduzir | Elemento | 84 - DadosDeducao | ItemvlDed | 0-1 | Valor da Dedução do Item | Externo | 
| **92** | Prestador | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 0-1 |  | Interno | 
| **93** | CpfCnpj | Grupo | 92 - Prestador |  | 0-1 |  | Interno | 
| **94** | Cpf | Elemento | 93 - CpfCnpj |  | 0-1 |  | Interno | 
| **95** | Cnpj | Elemento | 93 - CpfCnpj | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo | 
| **96** | InscricaoMunicipal | Elemento | 92 - Prestador | IM | 0-1 | Inscrição Municipal do Prestador | Externo | 
| **97** | Tomador | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 0-1 |  | Interno | 
| **98** | IdentificacaoTomador | Grupo | 97 - Tomador |  | 0-1 |  | Interno | 
| **99** | CpfCnpj | Grupo | 98 - IdentificacaoTomador |  | 0-1 |  | Interno | 
| **100** | Cpf | Elemento | 99 - CpfCnpj | TomaCPF | 0-1 | CPF do Tomador | Externo | 
| **101** | Cnpj | Elemento | 99 - CpfCnpj | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo | 
| **102** | InscricaoMunicipal | Elemento | 98 - IdentificacaoTomador | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo | 
| **103** | NifTomador | Elemento | 97 - Tomador | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo | 
| **104** | RazaoSocial | Elemento | 97 - Tomador | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo | 
| **105** | Endereco | Grupo | 97 - Tomador |  | 0-1 |  | Interno | 
| **106** | Endereco | Elemento | 105 - Endereco | TomaEndereco | 0-1 | Endereço do Tomador | Externo | 
| **107** | Numero | Elemento | 105 - Endereco | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo | 
| **108** | Complemento | Elemento | 105 - Endereco | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo | 
| **109** | Bairro | Elemento | 105 - Endereco | TomaBairro | 0-1 | Bairro do Tomador | Externo | 
| **110** | CodigoMunicipio | Elemento | 105 - Endereco | TomacMun | 0-1 | Codigo do Municipio IBGE do Tomador | Externo | 
| **111** | Uf | Elemento | 105 - Endereco | TomaUF | 0-1 | UF (sigla) do estado do Tomador (Nacional) | Externo | 
| **112** | CodigoPais | Elemento | 105 - Endereco |  | 0-1 |  | Interno | 
| **113** | Cep | Elemento | 105 - Endereco | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo | 
| **114** | Contato | Grupo | 97 - Tomador |  | 0-1 |  | Interno | 
| **115** | Telefone | Elemento | 114 - Contato | TomaTelefone | 0-1 | Telefone do Tomador | Externo | 
| **116** | Email | Elemento | 114 - Contato | TomaEmail | 0-1 | Email do Tomador | Externo | 
| **117** | InscricaoEstadual | Elemento | 97 - Tomador | TomaIE | 0-1 | Inscrição Estadual do Tomador | Externo | 
| **118** | Intermediario | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 0-1 |  | Interno | 
| **119** | IdentificacaoIntermediario | Grupo | 118 - Intermediario |  | 0-1 |  | Interno | 
| **120** | CpfCnpj | Grupo | 119 - IdentificacaoIntermediario |  | 0-1 |  | Interno | 
| **121** | Cnpj | Elemento | 120 - CpfCnpj | IntermCNPJ | 1-1 | CNPJ do Intermediário | Externo | 
| **122** | Cpf | Elemento | 120 - CpfCnpj | IntermCPF | 1-1 | CPF do Intermediário | Externo | 
| **123** | InscricaoMunicipal | Elemento | 119 - IdentificacaoIntermediario | IntermIM | 1-1 | Inscrição Municipal do Intermediário | Externo | 
| **124** | RazaoSocial | Elemento | 118 - Intermediario | IntermRazaoSocial | 1-1 | Razão Social do Intermediário | Externo | 
| **125** | CodigoMunicipio | Elemento | 118 - Intermediario | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo | 
| **126** | ConstrucaoCivil | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 0-1 |  | Interno | 
| **127** | CodigoObra | Elemento | 126 - ConstrucaoCivil | CodObra | 0-1 | Código da Obra | Externo | 
| **128** | NumeroAlvaraConstrucao | Elemento | 126 - ConstrucaoCivil | SerNumAlvara | 0-1 | Numero do Alvará de Servicos | Externo | 
| **129** | Art | Elemento | 126 - ConstrucaoCivil | Art | 1-1 | Código ART da Obra | Externo | 
| **130** | Incorporacao | Elemento | 126 - ConstrucaoCivil |  | 0-1 |  | Interno | 
| **131** | RegimeEspecialTributacao | Elemento | 20 - InfDeclaracaoPrestacaoServico | RegEspTrib | 0-1 | Regime Especial de Tributação | Externo | 
| **132** | IncentivoFiscal | Elemento | 20 - InfDeclaracaoPrestacaoServico | IncCult | 0-1 | Incentivo Cultural | Externo | 
| **133** | InfIbsCbs | Grupo | 20 - InfDeclaracaoPrestacaoServico |  | 0-1 |  | Interno | 
| **134** | finNFSe | Elemento | 133 - InfIbsCbs | finNFSe | 0-1 | Indicador da finalidade da emissão de NFS-e | Externo | 
| **135** | indFinal | Elemento | 133 - InfIbsCbs | indFinal | 0-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo | 
| **136** | cIndOp | Elemento | 133 - InfIbsCbs | cIndOp | 0-1 | Código indicador da operação de fornecimento, conforme tabela “código indicador de operação | Externo | 
| **137** | tpOper | Elemento | 133 - InfIbsCbs | tpOper | 0-1 | Tipo de Operação com Entes Governanementais ou outrosserviços sobre bens imóveis | Externo | 
| **138** | tpEnteGov | Elemento | 133 - InfIbsCbs | tpEnteGov | 0-1 | Tipo de ente governamental Para administração pública direta e suas autarquias e fundações: | Externo | 
| **139** | indDest | Elemento | 133 - InfIbsCbs | indDest | 0-1 | A respeito do Destinatário dos serviços:0 – O tomador na NFS-e é o adquirente e o destinatário | Externo | 
| **140** | gRefNFSe | Grupo | 133 - InfIbsCbs | ListaNFSeRef | 0-1 |  | Interno | 
| **141** | refNFSe | Elemento | 140 - gRefNFSe | DocChaveNFSe | 0-N | Chave da NFS-e referenciada | Externo | 
| **142** | dest | Grupo | 133 - InfIbsCbs |  | 0-1 |  | Interno | 
| **143** | CpfCnpj | Grupo | 142 - dest |  | 0-1 |  | Interno | 
| **144** | Cpf | Elemento | 143 - CpfCnpj | DestCPF | 0-1 | CPF do destinatário | Externo | 
| **145** | Cnpj | Elemento | 143 - CpfCnpj | DestCNPJ | 0-1 | Número da inscrição no Cadastro Nacional de Pessoa Jurídica do destinatário do serviço | Externo | 
| **146** | NIF | Elemento | 142 - dest | DestNIF | 0-1 | Número de identificação fiscal fornecido por órgão de administração trib. do exterior | Externo | 
| **147** | cNaoNif | Elemento | 142 - dest | DestcNaoNIF | 0-1 | Motivo não informe NIF | Externo | 
| **148** | XNome | Elemento | 142 - dest | DestNome | 0-1 | Nome / Nome Empresarial do destinatário | Externo | 
| **149** | fone | Elemento | 142 - dest | DestFone | 0-1 | Número do telefone do destinatário | Externo | 
| **150** | email | Elemento | 142 - dest | DestEmail | 0-1 | Email do destinatário | Externo | 
| **151** | end | Elemento | 142 - dest | DestEndereco | 0-1 | Tipo e nome do logradouro do endereço do destinatário | Externo | 
| **152** | imovel | Grupo | 133 - InfIbsCbs |  | 0-1 |  | Interno | 
| **153** | inscImobFisc | Elemento | 152 - imovel | ImovInscImobFisc | 0-1 |  | Externo | 
| **154** | cCIB | Elemento | 152 - imovel | ImovCIB | 0-1 | Código do Cadastro Imobiliário Brasileiro - CIB | Externo | 
| **155** | end | Elemento | 152 - imovel | ImovEndereco | 0-1 | Tipo e nome do logradouro do endereço do imóve | Externo | 
| **156** | valores | Grupo | 133 - InfIbsCbs |  | 0-1 |  | Interno | 
| **157** | trib | Grupo | 156 - valores |  | 0-1 |  | Interno | 
| **158** | gIBSCBS | Grupo | 157 - trib |  | 0-1 |  | Interno | 
| **159** | Cst | Elemento | 158 - gIBSCBS | CSTIBSCBS | 0-1 | Código de Situação Tributária do IBS e da CBS | Externo | 
| **160** | cClassTrib | Elemento | 158 - gIBSCBS | cClassTrib | 0-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo | 
| **161** | cCredPres | Elemento | 158 - gIBSCBS | cCredPres | 0-1 | Código e classificação do crédito presumido: IBS e CBS | Externo | 
| **162** | gTribRegular | Grupo | 158 - gIBSCBS |  | 0-1 |  | Interno | 
| **163** | CstReg | Elemento | 162 - gTribRegular | CSTReg | 0-1 | Código de Situação Tributária do | Externo | 
| **164** | cClassTribReg | Elemento | 162 - gTribRegular | cClassTribReg | 0-1 | Código da Classificação Tributária do | Externo | 
| **165** | gDif | Grupo | 158 - gIBSCBS |  | 0-1 |  | Interno | 
| **166** | pDifUF | Elemento | 165 - gDif | ValpDifUF | 0-1 | Percentual de diferimento para o IBS estadual | Externo | 
| **167** | pDifMun | Elemento | 165 - gDif | ValpDifMun | 0-1 | Percentual de diferimento para o IBS municipal | Externo | 
| **168** | pDifCBS | Elemento | 165 - gDif | ValpDifCBS | 0-1 | Percentual de diferimento para a CBS | Externo | 

*Observações: Os sequenciais 17 e 18 estiveram ausentes na extração original do log e suas numerações foram preservadas identicamente ao log retornado.*