# Mapeamento De/Para: Layout da Prefeitura x Tag InvoiCy

Este documento contém o mapeamento completo e consolidado de todos os campos (sequenciais 1 a 184) entre o layout da prefeitura e o sistema InvoiCy.

---

| Sequencial | TAG da prefeitura | Elemento XML | Dentro do grupo | Nome do campo (tag InvoiCy) | Ocorrência | Descrição | Interno / Externo |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | EnviarLoteRpsEnvio | Grupo | 0 - | | 0-1 | | Interno |
| **2** | LoteRps | Grupo | 1 - EnviarLoteRpsEnvio | | 0-1 | | Interno |
| **3** | versao | Atributo | 2 - LoteRps | | 0-1 | | Interno |
| **4** | Id | Atributo | 2 - LoteRps | IdAleatrio | 0-1 | Atributo ID Aleatório | Interno |
| **5** | NumeroLote | Elemento | 2 - LoteRps | NumeroLote | 0-1 | Número do Lote | Interno |
| **6** | Prestador | Grupo | 2 - LoteRps | | 0-1 | | Interno |
| **7** | CpfCnpj | Grupo | 6 - Prestador | | 0-1 | | Interno |
| **8** | Cnpj | Elemento | 7 - CpfCnpj | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| **9** | Cpf | Elemento | 7 - CpfCnpj | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| **10** | InscricaoMunicipal | Elemento | 6 - Prestador | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| **11** | QuantidadeRps | Elemento | 2 - LoteRps | QuantidadeRps | 0-1 | Quantidade de RPS no Lote | Interno |
| **12** | ListaRps | Grupo | 2 - LoteRps | | 0-1 | | Interno |
| **13** | Rps | Grupo | 12 - ListaRps | RPS | 1-N | Lista Rps Recepção (Controle Geração) | Interno |
| **14** | InfDeclaracaoPrestacaoServico | Grupo | 13 - Rps | | 0-1 | | Interno |
| **15** | Id | Atributo | 14 - InfDeclaracaoPrestacaoServico | InfDeclaracaoPrestacaoServico | 0-1 | Id do Grupo InfDeclaracaoPrestacaoServico | Interno |
| **16** | Rps | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **17** | IdentificacaoRps | Grupo | 16 - Rps | | 0-1 | | Interno |
| **18** | Numero | Elemento | 17 - IdentificacaoRps | RPSNumero | 0-1 | Número do RPS | Externo |
| **19** | Serie | Elemento | 17 - IdentificacaoRps | RPSSerie | 0-1 | Série do RPS | Externo |
| **20** | Tipo | Elemento | 17 - IdentificacaoRps | RPSTipo | 0-1 | 1 – Recibo Provisório de Serviços | Externo |
| **21** | DataEmissao | Elemento | 16 - Rps | dEmis | 0-1 | Data de Emissão | Externo |
| **22** | Status | Elemento | 16 - Rps | Status | 0-1 | Status do RPS | Externo |
| **23** | RpsSubstituido | Grupo | 16 - Rps | | 0-1 | | Interno |
| **24** | Numero | Elemento | 23 - RpsSubstituido | SubsNumero | 0-1 | Número do RPS Substituído | Externo |
| **25** | Serie | Elemento | 23 - RpsSubstituido | SubsSerie | 0-1 | Série do RPS Substituído | Externo |
| **26** | Tipo | Elemento | 23 - RpsSubstituido | SubsTipo | 0-1 | Tipo do RPS Substituído | Externo |
| **27** | Competencia | Elemento | 14 - InfDeclaracaoPrestacaoServico | dCompetencia | 0-1 | data de Competência | Externo |
| **28** | Servico | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **29** | Valores | Grupo | 28 - Servico | | 0-1 | | Interno |
| **30** | ValorServicos | Elemento | 29 - Valores | ValServicos | 0-1 | Valor do Serviço | Externo |
| **31** | ValorDeducoes | Elemento | 29 - Valores | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo |
| **32** | ValorPis | Elemento | 29 - Valores | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| **33** | ValorCofins | Elemento | 29 - Valores | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| **34** | ValorInss | Elemento | 29 - Valores | ValINSS | 0-1 | Valor do INSS do serviço | Externo |
| **35** | ValorIr | Elemento | 29 - Valores | ValIR | 0-1 | Valor do IRRF do serviço | Externo |
| **36** | ValorCsll | Elemento | 29 - Valores | ValCSLL | 0-1 | Valor do CSLL | Externo |
| **37** | OutrasRetencoes | Elemento | 29 - Valores | ValOutrasRetencoes | 0-1 | Valor de Outras Retenções do serviço | Externo |
| **38** | ValTotTributos | Elemento | 29 - Valores | ValAproxTrib | 0-1 | Valor Total Aproximado de Tributos | Externo |
| **39** | ValorIss | Elemento | 29 - Valores | ValISS | 0-1 | Valor do ISS | Externo |
| **40** | Aliquota | Elemento | 29 - Valores | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| **41** | DescontoIncondicionado | Elemento | 29 - Valores | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo |
| **42** | DescontoCondicionado | Elemento | 29 - Valores | ValDescCond | 0-1 | Valor desconto Condicionado | Externo |
| **43** | trib | Grupo | 29 - Valores | | 1-1 | | Interno |
| **44** | tribFed | Grupo | 43 - trib | | 0-1 | | Interno |
| **45** | piscofins | Grupo | 44 - tribFed | | 0-1 | | Interno |
| **46** | CST | Elemento | 45 - piscofins | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo |
| **47** | vBCPisCofins | Elemento | 45 - piscofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| **48** | pAliqPis | Elemento | 45 - piscofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| **49** | pAliqCofins | Elemento | 45 - piscofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| **50** | vPis | Elemento | 45 - piscofins | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| **51** | vCofins | Elemento | 45 - piscofins | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| **52** | tpRetPisCofins | Elemento | 45 - piscofins | tpRetPISCofins | 0-1 | Tipo de Retenção PIS/COFINS e CSLL | Externo |
| **53** | totTrib | Grupo | 43 - trib | | 1-1 | | Interno |
| **55** | pTotTrib | Grupo | 53 - totTrib | | 0-1 | | Interno |
| **56** | pTotTribFed | Elemento | 55 - pTotTrib | ValAliqRetFederais | 0-1 | Valor percentual total aproximado dos tributos federais | Externo |
| **57** | pTotTribEst | Elemento | 55 - pTotTrib | ValAliqRetEstaduais | 0-1 | Valor percentual total aproximado dos tributos estaduais | Externo |
| **58** | pTotTribMun | Elemento | 55 - pTotTrib | ValAliqRetMunicipais | 0-1 | Valor percentual total aproximado dos tributos municipais | Externo |
| **59** | indTotTrib | Elemento | 53 - totTrib | | 1-1 | | Interno |
| **60** | pTotTribSN | Elemento | 53 - totTrib | ValAproxTribAliqSN | 0-1 | Valor percentual aproximado do total dos tributos da alíquota do Simples Nacional (%) | Externo |
| **61** | IBSCBS | Grupo | 29 - Valores | | 0-1 | | Interno |
| **62** | finNFSe | Elemento | 61 - IBSCBS | finNFSe | 1-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| **63** | indFinal | Elemento | 61 - IBSCBS | indFinal | 1-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo |
| **64** | cIndOp | Elemento | 61 - IBSCBS | cIndOp | 0-1 | Código indicador da operação de fornecimento, conforme tabela “código indicador de operação | Externo |
| **65** | tpOper | Elemento | 61 - IBSCBS | tpOper | 0-1 | Tipo de Operação com Entes Governanementais ou outrosserviços sobre bens imóveis | Externo |
| **66** | gRefNFSe | Grupo | 61 - IBSCBS | | 0-1 | | Interno |
| **67** | refNFSe | Elemento | 66 - gRefNFSe | DocChaveNFSe | 0-1 | Chave da NFS-e referenciada | Externo |
| **68** | tpEnteGov | Elemento | 61 - IBSCBS | tpEnteGov | 0-1 | Tipo de ente governamental Para administração pública direta e suas autarquias e fundações: | Externo |
| **69** | indDest | Elemento | 61 - IBSCBS | indDest | 1-1 | A respeito do Destinatário dos serviços:0 – O tomador na NFS-e é o adquirente e o destinatário | Externo |
| **70** | valores | Grupo | 61 - IBSCBS | | 0-1 | | Interno |
| **71** | gReeRepRes | Grupo | 70 - valores | | 0-1 | | Interno |
| **72** | documentos | Grupo | 71 - gReeRepRes | | 0-1 | | Interno |
| **73** | dFeNacional | Grupo | 72 - documentos | | 0-1 | | Interno |
| **74** | tipoChaveDFe | Elemento | 73 - dFeNacional | DocTipoChaveDFe | 0-1 | Documento fiscal a que se refere a chaveDfe:1 = NFS-e2 = NF-e 3 = CT-e9 = Outro | Externo |
| **75** | xTipoChaveDFe | Elemento | 73 - dFeNacional | DocDescTipoChaveDFe | 0-1 | Descrição da DF-e a que se refere a chaveDfe que seja um dos documentos do Repositório Nacional | Externo |
| **76** | chaveDFe | Elemento | 73 - dFeNacional | DocChaveDFe | 0-1 | Chave do Documento Fiscal eletrônico do repositório nacional referenciado operações já tributadas. | Externo |
| **77** | fornec | Grupo | 72 - documentos | | 0-1 | | Interno |
| **78** | CNPJ | Elemento | 77 - fornec | DocCNPJ | 0-1 | Número da inscrição federal (CNPJ) do fornecedor do documento | Externo |
| **79** | CPF | Elemento | 77 - fornec | DocCPF | 0-1 | Número da inscrição federal (CPF) do fornecedor. | Externo |
| **80** | xNome | Elemento | 77 - fornec | DocRazaoSocial | 0-1 | Nome / Razão Social do fornecedor. | Externo |
| **81** | dtEmiDoc | Elemento | 72 - documentos | DocDataEmissao | 0-1 | Data da emissão do documento dedutível.Ano, mês e dia (AAAA-MM-DD) | Externo |
| **82** | dtCompDoc | Elemento | 72 - documentos | DocDCompetencia | 0-1 | Data da competência do documento dedutível.Ano, mês e dia (AAAA-MM-DD) | Externo |
| **83** | tpReeRepRes | Elemento | 72 - documentos | DocTipoReeRepRes | 0-1 | Tipo de valor incluído neste documento | Externo |
| **84** | vlrReeRepRes | Elemento | 72 - documentos | DocValorReeRepRes | 0-1 | Valor monetário (total ou parcial, conforme documento informado) | Externo |
| **85** | trib | Grupo | 70 - valores | | 0-1 | | Interno |
| **86** | gIBSCBS | Grupo | 85 - trib | | 0-1 | | Interno |
| **87** | CST | Elemento | 86 - gIBSCBS | CSTIBSCBS | 0-1 | Código de Situação Tributária do IBS e da CBS | Externo |
| **88** | cClassTrib | Elemento | 86 - gIBSCBS | cClassTrib | 0-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo |
| **89** | cLocalidadeIncid | Elemento | 70 - valores | cLocalidadeIncid | 0-1 | Código IBGE da localidade de incidência do IBS/CBS (local da operação). | Externo |
| **90** | pRedutor | Elemento | 70 - valores | ValpRedutor | 0-1 | Percentual de redução compras governamentais | Externo |
| **91** | vBC | Elemento | 70 - valores | ValBCIBSCBS | 0-1 | Valor da base de cálculo (BC) do IBS/CBS antes das reduções para cálculo do tributo bruto. | Externo |
| **92** | IssRetido | Elemento | 28 - Servico | ISSRetido | 0-1 | É ISS Retido? | Externo |
| **93** | ResponsavelRetencao | Elemento | 28 - Servico | RespRetencao | 0-1 | Responsável pela Retenção | Externo |
| **94** | ItemListaServico | Elemento | 28 - Servico | IteListServico | 0-1 | código do Item da lista de servico LC No 116/2003 | Externo |
| **95** | CodigoCnae | Elemento | 28 - Servico | Cnae | 0-1 | CNAE - Classificação Nacional de Atividades Econômicas | Externo |
| **96** | CodigoTributacaoMunicipio | Elemento | 28 - Servico | TributMunicipio | 0-1 | Código de Tributação do Município | Externo |
| **97** | CodigoNbs | Elemento | 28 - Servico | CodigoNBS | 0-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo |
| **98** | Discriminacao | Elemento | 28 - Servico | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| **99** | CodigoMunicipio | Elemento | 28 - Servico | cMun | 0-1 | Código do Município do Serviço | Externo |
| **100** | CodigoPais | Elemento | 28 - Servico | | 1-1 | | Interno |
| **101** | ExigibilidadeISS | Elemento | 28 - Servico | natOp | 0-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| **102** | IdentifNaoExigibilidade | Elemento | 28 - Servico | | 0-1 | | Interno |
| **103** | MunicipioIncidencia | Elemento | 28 - Servico | cMunIncidencia | 0-1 | Codigo do Municipio IBGE de Incidência do imposto | Externo |
| **104** | NumeroProcesso | Elemento | 28 - Servico | NumProcesso | 0-1 | Número do Processo Administrativo | Externo |
| **105** | comExt | Grupo | 28 - Servico | | 0-1 | | Interno |
| **106** | mdPrestacao | Elemento | 105 - comExt | mdPrestacao | 0-1 | Modo de Prestação | Externo |
| **107** | vincPrest | Elemento | 105 - comExt | vincPrest | 0-1 | Vínculo entre as partes no negócio | Externo |
| **108** | tpMoeda | Elemento | 105 - comExt | tpMoeda | 0-1 | Identifica a moeda da transação comercial | Externo |
| **109** | vServMoeda | Elemento | 105 - comExt | ValServEst | 0-1 | Valor do Serviço em Moeda estrangeira | Externo |
| **110** | mecAFComexP | Elemento | 105 - comExt | mecAFComexP | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo prestador do serviço | Externo |
| **111** | mecAFComexT | Elemento | 105 - comExt | mecAFComexT | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo tomador do serviço | Externo |
| **112** | movTempBens | Elemento | 105 - comExt | movTempBens | 0-1 | Operação está vinculada à Movimentação Temporária de Bens | Externo |
| **113** | nDI | Elemento | 105 - comExt | nDI | 0-1 | Número da Declaração de Importação (DI/DSI/DA/DRI-E) averbado | Externo |
| **114** | nRE | Elemento | 105 - comExt | nRE | 0-1 | Número do Registro de Exportação (RE) averbado | Externo |
| **115** | mdic | Elemento | 105 - comExt | mdic | 0-1 | Compartilhar as informações da NFS-e gerada a partir desta DPS com a Secretaria de Comércio Ext | Externo |
| **116** | Prestador | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **117** | CpfCnpj | Grupo | 116 - Prestador | | 0-1 | | Interno |
| **118** | Cnpj | Elemento | 117 - CpfCnpj | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| **119** | Cpf | Elemento | 117 - CpfCnpj | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| **120** | InscricaoMunicipal | Elemento | 116 - Prestador | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| **121** | TomadorServico | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **122** | IdentificacaoTomador | Grupo | 121 - TomadorServico | | 0-1 | | Interno |
| **123** | CpfCnpj | Grupo | 122 - IdentificacaoTomador | | 0-1 | | Interno |
| **124** | Cpf | Elemento | 123 - CpfCnpj | TomaCPF | 0-1 | CPF do Tomador | Externo |
| **125** | Cnpj | Elemento | 123 - CpfCnpj | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo |
| **126** | InscricaoMunicipal | Elemento | 122 - IdentificacaoTomador | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo |
| **127** | NifTomador | Elemento | 121 - TomadorServico | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| **128** | RazaoSocial | Elemento | 121 - TomadorServico | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo |
| **129** | Endereco | Grupo | 121 - TomadorServico | | 0-1 | | Interno |
| **130** | Endereco | Elemento | 129 - Endereco | TomaEndereco | 0-1 | Endereço do Tomador | Externo |
| **131** | Numero | Elemento | 129 - Endereco | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| **132** | Complemento | Elemento | 129 - Endereco | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo |
| **133** | Bairro | Elemento | 129 - Endereco | TomaBairro | 0-1 | Bairro do Tomador | Externo |
| **134** | CodigoMunicipio | Elemento | 129 - Endereco | TomacMun | 0-1 | Codigo do Municipio IBGE do Tomador | Externo |
| **135** | CodigoPais | Elemento | 129 - Endereco | | 0-1 | | Interno |
| **136** | Uf | Elemento | 129 - Endereco | TomaUF | 0-1 | UF (sigla) do estado do Tomador (Nacional) | Externo |
| **137** | Cep | Elemento | 129 - Endereco | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| **138** | EnderecoExterior | Grupo | 121 - TomadorServico | | 0-1 | | Interno |
| **139** | CodigoPais | Elemento | 138 - EnderecoExterior | TomaPais | 0-1 | País do Tomador no Exterior | Externo |
| **140** | EnderecoCompletoExterior | Elemento | 138 - EnderecoExterior | TomaEndereco | 0-1 | Logradouro do Tomador no Exterior | Externo |
| **141** | cEndPost | Elemento | 138 - EnderecoExterior | TomaCEP | 0-1 | CEP do Tomador (exterior) | Externo |
| **142** | xCidade | Elemento | 138 - EnderecoExterior | TomaXMun | 0-1 | Nome do Município do tomador (Exterior) | Externo |
| **143** | xEstProvReg | Elemento | 138 - EnderecoExterior | TomaUF | 0-1 | UF (sigla) do estado do Tomador (exterior) | Externo |
| **144** | Contato | Grupo | 121 - TomadorServico | | 0-1 | | Interno |
| **145** | Telefone | Elemento | 144 - Contato | TomaTelefone | 0-1 | Telefone do Tomador | Externo |
| **146** | Email | Elemento | 144 - Contato | TomaEmail | 0-1 | Email do Tomador | Externo |
| **147** | Intermediario | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **148** | IdentificacaoIntermediario | Grupo | 147 - Intermediario | | 0-1 | | Interno |
| **149** | CpfCnpj | Grupo | 148 - IdentificacaoIntermediario | | 0-1 | | Interno |
| **150** | Cpf | Elemento | 149 - CpfCnpj | IntermCPF | 0-1 | CPF do Intermediário | Externo |
| **151** | Cnpj | Elemento | 149 - CpfCnpj | IntermCNPJ | 0-1 | CNPJ do Intermediário | Externo |
| **152** | InscricaoMunicipal | Elemento | 148 - IdentificacaoIntermediario | IntermIM | 0-1 | Inscrição Municipal do Intermediário | Externo |
| **153** | RazaoSocial | Elemento | 147 - Intermediario | IntermRazaoSocial | 0-1 | Razão Social do Intermediário | Externo |
| **154** | CodigoMunicipio | Elemento | 147 - Intermediario | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo |
| **155** | ConstrucaoCivil | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **156** | CodigoObra | Elemento | 155 - ConstrucaoCivil | CodObra | 0-1 | Código da Obra | Externo |
| **157** | Art | Elemento | 155 - ConstrucaoCivil | Art | 0-1 | Código ART da Obra | Externo |
| **158** | RegimeEspecialTributacao | Elemento | 14 - InfDeclaracaoPrestacaoServico | RegEspTrib | 0-1 | Regime Especial de Tributação | Externo |
| **159** | OptanteSimplesNacional | Elemento | 14 - InfDeclaracaoPrestacaoServico | OptSN | 0-1 | Optante Simples Nacional | Externo |
| **160** | IncentivoFiscal | Elemento | 14 - InfDeclaracaoPrestacaoServico | IncCult | 0-1 | Incentivo Cultural | Externo |
| **161** | Evento | Grupo | 14 - InfDeclaracaoPrestacaoServico | | 0-1 | | Interno |
| **162** | IdentificacaoEvento | Elemento | 161 - Evento | AtivIdEvento | 0-1 | Identificação da Atividade de Evento | Externo |
| **163** | DescricaoEvento | Elemento | 161 - Evento | | 0-1 | | Interno |
| **164** | InformacoesComplementares | Elemento | 14 - InfDeclaracaoPrestacaoServico | NFSOutrasinformacoes | 0-1 | Outras Informações / observações | Externo |
| **165** | Deducao | Grupo | 14 - InfDeclaracaoPrestacaoServico | Ded | 0-1 | Grupo Dedução (controle) | Interno |
| **166** | TipoDeducao | Elemento | 165 - Deducao | DedTipo | 0-1 | Tipo de Dedução | Externo |
| **167** | DescricaoDeducao | Elemento | 165 - Deducao | DedDescricao | 0-1 | Descrição do item de Dedução | Externo |
| **168** | IdentificacaoDocumentoDeducao | Grupo | 165 - Deducao | | 0-1 | | Interno |
| **169** | IdentificacaoNfse | Grupo | 168 - IdentificacaoDocumentoDeducao | | 0-1 | | Interno |
| **170** | CodigoMunicipioGerador | Elemento | 169 - IdentificacaoNfse | DedcMunNFSe | 0-1 | Código do município da NFS-e de referência | Externo |
| **171** | NumeroNfse | Elemento | 169 - IdentificacaoNfse | DednNFSeRef | 0-1 | Número da NFS-e de referência | Externo |
| **172** | CodigoVerificacao | Elemento | 169 - IdentificacaoNfse | DedcVerificaNFSe | 0-1 | Código de verificação da NFS-e de referência | Externo |
| **173** | IdentificacaoNfe | Grupo | 168 - IdentificacaoDocumentoDeducao | | 0-1 | | Interno |
| **174** | NumeroNfe | Elemento | 173 - IdentificacaoNfe | DednNFRef | 0-1 | Número da nota de referência da Dedução | Externo |
| **175** | UfNfe | Elemento | 173 - IdentificacaoNfe | DedUFNFe | 0-1 | UF da NF-e de referência | Externo |
| **176** | ChaveAcessoNfe | Elemento | 173 - IdentificacaoNfe | DedChaveAcesso | 0-1 | Chave de acesso da NF-e de referência | Externo |
| **177** | DadosFornecedor | Grupo | 165 - Deducao | | 0-1 | | Interno |
| **178** | IdentificacaoFornecedor | Grupo | 177 - DadosFornecedor | | 0-1 | | Interno |
| **179** | CpfCnpj | Grupo | 178 - IdentificacaoFornecedor | | 0-1 | | Interno |
| **180** | Cpf | Elemento | 179 - CpfCnpj | | 0-1 | | Interno |
| **181** | Cnpj | Elemento | 179 - CpfCnpj | DedCNPJRef | 0-1 | CNPJ do fornecedor ou prestador | Externo |
| **182** | DataEmissao | Elemento | 165 - Deducao | DedDEmis | 0-1 | Data de emissão da NF-e de referência | Externo |
| **183** | ValorDedutivel | Elemento | 165 - Deducao | DedValor | 0-1 | Valor da Dedução da lista de deduções | Externo |
| **184** | ValorUtilizadoDeducao | Elemento | 165 - Deducao | DedValor | 0-1 | Valor da Dedução da lista de deduções | Externo |

---
*Observação: A numeração sequencial 54 não esteve presente no log original de extração e foi mantida conforme os dados extraídos.*