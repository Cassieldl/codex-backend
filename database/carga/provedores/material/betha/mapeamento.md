# Mapeamento "De/Para" - Layout InvoiCy (Novo Layout DPS)

Este documento contém a consolidação e organização do novo mapeamento de campos do layout InvoiCy, processando todas as 33 páginas do log.

## Tabela de Mapeamento Completa

| Sequencial | TAG Prefeitura | Elemento XML | Dentro do Grupo | Nome do Campo (Tag InvoiCy) | Ocorrência | Descrição | Visibilidade |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | DPS | Grupo | 0 - | | 0-1 | | Interno |
| **2** | infDPS | Grupo | 1 - DPS | | 0-1 | | Interno |
| **3** | Id | Atributo | 2 - infDPS | RPSNumero e RPSSerie | 0-1 | Id do Grupo RPS | Interno |
| **4** | tpAmb | Elemento | 2 - infDPS | tpAmb | 1-1 | Ambiente Emissão | Interno |
| **5** | dhEmi | Elemento | 2 - infDPS | dEmis | 0-1 | Data de Emissão | Externo |
| **6** | verAplic | Elemento | 2 - infDPS | | 0-1 | | Interno |
| **7** | serie | Elemento | 2 - infDPS | RPSSerie | 0-1 | Série do RPS | Externo |
| **8** | nDPS | Elemento | 2 - infDPS | RPSNumero | 0-1 | Número do RPS | Externo |
| **9** | dCompet | Elemento | 2 - infDPS | dCompetencia | 0-1 | data de Competência | Externo |
| **10** | tpEmit | Elemento | 2 - infDPS | | 1-1 | | Interno |
| **11** | cLocEmi | Elemento | 2 - infDPS | cMun | 0-1 | Código do Município do prestador | Externo |
| **12** | subst | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **13** | chSubstda | Elemento | 12 - subst | SubsChave | 0-1 | Chave de Acesso NFSe Substituída | Externo |
| **14** | cMotivo | Elemento | 12 - subst | SubsCodMotivo | 0-1 | Código do Motivo da substituição | Externo |
| **15** | xMotivo | Elemento | 12 - subst | SubsDescMotivo | 0-1 | Descrição do Motivo da substituição | Externo |
| **16** | prest | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **17** | CNPJ | Elemento | 16 - prest | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| **18** | CPF | Elemento | 16 - prest | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| **19** | NIF | Elemento | 16 - prest | | 0-1 | | Interno |
| **20** | cNaoNIF | Elemento | 16 - prest | | 0-1 | | Interno |
| **21** | CAEPF | Elemento | 16 - prest | CAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) do prestador do serviço | Externo |
| **22** | IM | Elemento | 16 - prest | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| **23** | xNome | Elemento | 16 - prest | | 0-1 | | Interno |
| **24** | end | Grupo | 16 - prest | | 0-1 | | Interno |
| **25** | endNac | Grupo | 24 - end | | 0-1 | | Interno |
| **26** | cMun | Elemento | 25 - endNac | | 0-1 | | Interno |
| **27** | CEP | Elemento | 25 - endNac | | 0-1 | | Interno |
| **28** | endExt | Grupo | 24 - end | | 0-1 | | Interno |
| **29** | cPais | Elemento | 28 - endExt | | 0-1 | | Interno |
| **30** | cEndPost | Elemento | 28 - endExt | | 0-1 | | Interno |
| **31** | xCidade | Elemento | 28 - endExt | | 0-1 | | Interno |
| **32** | xEstProvReg | Elemento | 28 - endExt | | 0-1 | | Interno |
| **33** | xLgr | Elemento | 24 - end | | 0-1 | | Interno |
| **34** | nro | Elemento | 24 - end | | 0-1 | | Interno |
| **35** | xCpl | Elemento | 24 - end | | 0-1 | | Interno |
| **36** | xBairro | Elemento | 24 - end | | 0-1 | | Interno |
| **37** | fone | Elemento | 16 - prest | | 0-1 | | Interno |
| **38** | email | Elemento | 16 - prest | | 0-1 | | Interno |
| **39** | regTrib | Grupo | 16 - prest | | 0-1 | | Interno |
| **40** | opSimpNac | Elemento | 39 - regTrib | OptSN | 0-1 | Optante Simples Nacional | Externo |
| **41** | regApTribSN | Elemento | 39 - regTrib | regApTribSN | 0-1 | Regime de tributação para empresas Opt. SN ME/EPP que ultrapassam sublimite | Externo |
| **42** | regEspTrib | Elemento | 39 - regTrib | RegEspTrib | 1-1 | Regime Especial de Tributação | Externo |
| **43** | toma | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **44** | CNPJ | Elemento | 43 - toma | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo |
| **45** | CPF | Elemento | 43 - toma | TomaCPF | 0-1 | CPF do Tomador | Externo |
| **46** | NIF | Elemento | 43 - toma | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| **47** | cNaoNIF | Elemento | 43 - toma | TomaExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **48** | CAEPF | Elemento | 43 - toma | TomaCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| **49** | IM | Elemento | 43 - toma | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo |
| **50** | xNome | Elemento | 43 - toma | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo |
| **51** | end | Grupo | 43 - toma | | 0-1 | | Interno |
| **52** | endNac | Grupo | 51 - end | | 0-1 | | Interno |
| **53** | cMun | Elemento | 52 - endNac | TomacMun | 0-1 | Codigo do Municipio IBGE do Tomador | Externo |
| **54** | CEP | Elemento | 52 - endNac | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| **55** | endExt | Grupo | 51 - end | | 0-1 | | Interno |
| **56** | cPais | Elemento | 55 - endExt | TomaPais | 0-1 | País do Tomador no Exterior | Externo |
| **57** | cEndPost | Elemento | 55 - endExt | TomaCEP | 0-1 | CEP do Tomador (exterior) | Externo |
| **58** | xCidade | Elemento | 55 - endExt | TomaXMun | 0-1 | Nome do Município do tomador (Exterior) | Externo |
| **59** | xEstProvReg | Elemento | 55 - endExt | TomaUF | 0-1 | UF (sigla) do estado do Tomador (exterior) | Externo |
| **60** | xLgr | Elemento | 51 - end | TomaEndereco | 0-1 | Endereço do Tomador | Externo |
| **61** | nro | Elemento | 51 - end | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| **62** | xCpl | Elemento | 51 - end | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo |
| **63** | xBairro | Elemento | 51 - end | TomaBairro | 0-1 | Bairro do Tomador | Externo |
| **64** | fone | Elemento | 43 - toma | TomaTelefone | 0-1 | Telefone do Tomador | Externo |
| **65** | email | Elemento | 43 - toma | TomaEmail | 0-1 | Email do Tomador | Externo |
| **66** | interm | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **67** | CNPJ | Elemento | 66 - interm | IntermCNPJ | 0-1 | CNPJ do Intermediário | Externo |
| **68** | CPF | Elemento | 66 - interm | IntermCPF | 0-1 | CPF do Intermediário | Externo |
| **69** | NIF | Elemento | 66 - interm | IntermNIF | 0-1 | Doc intermediário extrangeiro | Externo |
| **70** | cNaoNIF | Elemento | 66 - interm | IntermExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **71** | CAEPF | Elemento | 66 - interm | IntermCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| **72** | IM | Elemento | 66 - interm | IntermIM | 0-1 | Inscrição Municipal do Intermediário | Externo |
| **73** | xNome | Elemento | 66 - interm | IntermRazaoSocial | 0-1 | Razão Social do Intermediário | Externo |
| **74** | end | Grupo | 66 - interm | | 0-1 | | Interno |
| **75** | endNac | Grupo | 74 - end | | 0-1 | | Interno |
| **76** | cMun | Elemento | 75 - endNac | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo |
| **77** | CEP | Elemento | 75 - endNac | IntermCep | 0-1 | CEP do Intermediário (nacional) | Externo |
| **78** | endExt | Grupo | 74 - end | | 0-1 | | Interno |
| **79** | cPais | Elemento | 78 - endExt | IntermPais | 0-1 | Código do País do Intermediário | Externo |
| **80** | cEndPost | Elemento | 78 - endExt | IntermCep | 0-1 | CEP do intermediário (exterior) | Externo |
| **81** | xCidade | Elemento | 78 - endExt | IntermXmun | 0-1 | Nome do município do intermediário (exterior) | Externo |
| **82** | xEstProvReg | Elemento | 78 - endExt | IntermUFEx | 0-1 | UF/província/região do intermediário no exterior | Interno |
| **83** | xLgr | Elemento | 74 - end | IntermEndereco | 0-1 | Logradouro do Endereço do Intermediário | Externo |
| **84** | nro | Elemento | 74 - end | IntermNumero | 0-1 | Número do Endereço do Intermediário | Externo |
| **85** | xCpl | Elemento | 74 - end | IntermComplemento | 0-1 | Complemento do Endereço do Intermediário | Externo |
| **86** | xBairro | Elemento | 74 - end | IntermBairro | 0-1 | Bairro do Intermediário | Externo |
| **87** | fone | Elemento | 74 - end | IntermFone | 0-1 | Telefone do Intermediário | Externo |
| **88** | email | Elemento | 74 - end | IntermEmail | 0-1 | E-mail do Intermediário | Externo |
| **89** | serv | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **90** | locPrest | Grupo | 89 - serv | | 1-1 | | Interno |
| **91** | cLocPrestacao | Elemento | 90 - locPrest | SerEndcMun | 0-1 | Local da Prestação do Serviço Cod IBGE Municipio | Externo |
| **92** | cPaisPrestacao | Elemento | 90 - locPrest | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| **93** | opConsumServ | Elemento | 90 - locPrest | | 0-1 | | Interno |
| **94** | cServ | Grupo | 89 - serv | | 0-1 | | Interno |
| **95** | cTribNac | Elemento | 94 - cServ | codTributNacional | 0-1 | Código de tributação Nacional | Externo |
| **96** | cTribMun | Elemento | 94 - cServ | TributMunicipio | 0-1 | Código de Tributação do Município | Externo |
| **97** | xDescServ | Elemento | 94 - cServ | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| **98** | cNBS | Elemento | 94 - cServ | CodigoNBS | 0-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo |
| **99** | cIntContrib | Elemento | 94 - cServ | | 0-1 | | Interno |
| **100** | comExt | Grupo | 89 - serv | | 0-1 | | Interno |
| **101** | mdPrestacao | Elemento | 100 - comExt | mdPrestacao | 0-1 | Modo de Prestação | Externo |
| **102** | vincPrest | Elemento | 100 - comExt | vincPrest | 0-1 | Vínculo entre as partes no negócio | Externo |
| **103** | tpMoeda | Elemento | 100 - comExt | tpMoeda | 0-1 | Identifica a moeda da transação comercial | Externo |
| **104** | vServMoeda | Elemento | 100 - comExt | ValServEst | 0-1 | Valor do Serviço em Moeda estrangeira | Externo |
| **105** | mecAFComexP | Elemento | 100 - comExt | mecAFComexP | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo prestador do serviço | Externo |
| **106** | mecAFComexT | Elemento | 100 - comExt | mecAFComexT | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo tomador do serviço | Externo |
| **107** | movTempBens | Elemento | 100 - comExt | movTempBens | 0-1 | Operação está vinculada à Movimentação Temporária de Bens | Externo |
| **108** | nDI | Elemento | 100 - comExt | nroDUIMP | 0-1 | Número da Declaração de Importação (DI/DSI/DA/DRI-E) averbado | Externo |
| **109** | nRE | Elemento | 100 - comExt | nroDUE | 0-1 | Número do Registro de Exportação (RE) averbado | Externo |
| **110** | mdic | Elemento | 100 - comExt | mdic | 0-1 | Compartilhar as informações da NFS-e gerada a partir desta DPS com a Secretaria de Comércio Ext | Externo |
| **111** | lsadppu | Grupo | 89 - serv | | 0-1 | | Interno |
| **112** | categ | Elemento | 111 - lsadppu | categServ | 0-1 | Categoria do serviço | Externo |
| **113** | objeto | Elemento | 111 - lsadppu | objetoLocacao | 0-1 | Tipo de objetos da locação, sublocação, arrendamento, direito de passagem ou permissão de uso | Externo |
| **114** | extensao | Elemento | 111 - lsadppu | extensaoFerrovia | 0-1 | Extensão total da ferrovia, rodovia, cabos, dutos ou condutos | Externo |
| **115** | nPostes | Elemento | 111 - lsadppu | nPostes | 0-1 | Número total de postes | Externo |
| **116** | obra | Grupo | 89 - serv | | 0-1 | | Interno |
| **117** | inscImobFisc | Elemento | 116 - obra | ObraInscImobFis | 0-1 | Inscrição imobiliária fiscal da obra | Externo |
| **118** | cObra | Elemento | 116 - obra | CodObra | 0-1 | Código da Obra | Externo |
| **119** | cCIB | Elemento | 116 - obra | ObraCIB | 0-1 | CIB da Obra | Externo |
| **120** | end | Grupo | 116 - obra | | 0-1 | | Interno |
| **121** | CEP | Elemento | 120 - end | ObraCEP | 0-1 | CEP da Obra | Externo |
| **122** | endExt | Grupo | 120 - end | | 0-1 | | Interno |
| **123** | cEndPost | Elemento | 122 - endExt | ObraCEP | 0-1 | CEP da Obra (exterior) | Externo |
| **124** | xCidade | Elemento | 122 - endExt | ObraXMunEx | 0-1 | Nome do município da Obra no exterior | Interno |
| **125** | xEstProvReg | Elemento | 122 - endExt | ObraUF | 0-1 | UF da obra do exterior | Externo |
| **126** | xLgr | Elemento | 120 - end | ObraLog | 0-1 | Logradouro da Obra | Externo |
| **127** | nro | Elemento | 120 - end | ObraNumero | 0-1 | Número do End. da Obra | Externo |
| **128** | xCpl | Elemento | 120 - end | ObraCompl | 0-1 | Complemento do End. da Obra | Externo |
| **129** | xBairro | Elemento | 120 - end | ObraBairro | 0-1 | Bairro da Obra | Externo |
| **130** | atvEvento | Grupo | 89 - serv | | 0-1 | | Interno |
| **131** | xNome | Elemento | 130 - atvEvento | AtivDesc | 0-1 | Descrição do evento Artístico, Cultural, Esportivo, etc | Externo |
| **132** | dtIni | Elemento | 130 - atvEvento | AtivDataInicial | 0-1 | Data de início da atividade de evento | Externo |
| **133** | dtFim | Elemento | 130 - atvEvento | AtivDataFinal | 0-1 | Data de fim da atividade de evento | Externo |
| **134** | idAtvEvt | Elemento | 130 - atvEvento | AtivIdEvento | 0-1 | Identificação da Atividade de Evento | Externo |
| **135** | end | Grupo | 130 - atvEvento | | 0-1 | | Interno |
| **136** | CEP | Elemento | 135 - end | AtivEndCEP | 0-1 | CEP do endereço do evento | Externo |
| **137** | endExt | Grupo | 135 - end | | 0-1 | | Interno |
| **138** | cEndPost | Elemento | 137 - endExt | AtivEndCEPEx | 0-1 | CEP do endereço do evento no exterior | Interno |
| **139** | xCidade | Elemento | 137 - endExt | AtivEndxMunEx | 0-1 | Nome do município do endereço do evento do exterior | Interno |
| **140** | xEstProvReg | Elemento | 137 - endExt | AtivEndUfEx | 0-1 | UF/província/Região do evento no exterior | Interno |
| **141** | xLgr | Elemento | 135 - end | AtivEndLogradouro | 0-1 | Logradouro do endereço do evento | Externo |
| **142** | nro | Elemento | 135 - end | AtivEndNumero | 0-1 | Número do endereço do evento | Externo |
| **143** | xCpl | Elemento | 135 - end | AtivEndCompl | 0-1 | Complemento do endereço do evento | Externo |
| **144** | xBairro | Elemento | 135 - end | AtivEndBairro | 0-1 | Bairro do endereço do evento | Externo |
| **145** | explRod | Grupo | 89 - serv | | 0-1 | | Interno |
| **146** | categVeic | Elemento | 145 - explRod | PedCategVeiculo | 0-1 | Categorias de veículos para cobrança | Externo |
| **147** | nEixos | Elemento | 145 - explRod | PednEixos | 0-1 | Número de eixos para fins de cobrança | Externo |
| **148** | rodagem | Elemento | 145 - explRod | PedRodagem | 0-1 | Tipo de rodagem | Externo |
| **149** | sentido | Elemento | 145 - explRod | PedSentido | 0-1 | Orientação de passagem do veículo | Externo |
| **150** | placa | Elemento | 145 - explRod | PedPlaca | 0-1 | Placa do veículo | Externo |
| **151** | codAcessoPed | Elemento | 145 - explRod | PedCodAcesso | 0-1 | Código de acesso gerado automaticamente pelo sistema emissor da concessionária | Externo |
| **152** | codContrato | Elemento | 145 - explRod | PedCodContrato | 0-1 | Código de contrato gerado automaticamente pelo sistema nacional no cadastro da concessionária | Externo |
| **153** | infoCompl | Grupo | 89 - serv | | 0-1 | | Interno |
| **154** | idDocTec | Elemento | 153 - infoCompl | idDocTec | 0-1 | Identificador de Documento de Responsabilidade Técnica: ART, RRT, DRT, Outros. | Externo |
| **155** | docRef | Elemento | 153 - infoCompl | docRef | 0-1 | Chave da nota fiscal referenciada. | Externo |
| **156** | xInfComp | Elemento | 153 - infoCompl | NFSOutrasinformacoes | 0-1 | Outras Informações / observações | Externo |
| **157** | valores | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **158** | vServPrest | Grupo | 157 - valores | | 1-1 | | Interno |
| **159** | vReceb | Elemento | 158 - vServPrest | | 0-1 | | Interno |
| **160** | vServ | Elemento | 158 - vServPrest | ValServicos | 1-1 | Valor do Serviço | Externo |
| **161** | vDescCondIncond | Grupo | 157 - valores | | 0-1 | | Interno |
| **162** | vDescIncond | Elemento | 161 - vDescCondIncond | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo |
| **163** | vDescCond | Elemento | 161 - vDescCondIncond | ValDescCond | 0-1 | Valor desconto Condicionado | Externo |
| **164** | vDedRed | Grupo | 157 - valores | | 0-1 | | Interno |
| **165** | pDR | Elemento | 164 - vDedRed | ValPercDeducoes | 0-1 | Valor percentual padrão para dedução/redução do valor do serviço | Externo |
| **166** | vDR | Elemento | 164 - vDedRed | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo |
| **167** | documentos | Grupo | 164 - vDedRed | Ded | 0-1 | Grupo Dedução (controle) | Interno |
| **168** | docDedRed | Grupo | 167 - documentos | | 0-1 | | Interno |
| **169** | chNFSe | Elemento | 168 - docDedRed | DedChAcesNacional | 0-1 | Chave de Acesso da NFS-e (Padrão Nacional) | Externo |
| **170** | chNFe | Elemento | 168 - docDedRed | DedChaveAcesso | 0-1 | Chave de acesso da NF-e de referência | Externo |
| **171** | NFSeMun | Grupo | 168 - docDedRed | | 0-1 | | Interno |
| **172** | cMunNFSeMun | Elemento | 171 - NFSeMun | DedcMunNFSe | 0-1 | Código do município da NFS-e de referência | Externo |
| **173** | nNFSeMun | Elemento | 171 - NFSeMun | | 0-1 | | Interno |
| **174** | cVerifNFSeMun | Elemento | 171 - NFSeMun | DedcVerificaNFSe | 0-1 | Código de verificação da NFS-e de referência | Externo |
| **175** | NFNFS | Grupo | 168 - docDedRed | | 0-1 | | Interno |
| **176** | nNFS | Elemento | 175 - NFNFS | DednNFRef | 0-1 | Número da nota de referência da Dedução | Externo |
| **177** | modNFS | Elemento | 175 - NFNFS | | 0-1 | | Interno |
| **178** | serieNFS | Elemento | 175 - NFNFS | | 0-1 | | Interno |
| **179** | nDocFisc | Elemento | 168 - docDedRed | DedOutroDocFisc | 0-1 | Número de outro documento fiscal | Externo |
| **180** | nDoc | Elemento | 168 - docDedRed | DedOutroDoc | 0-1 | Código de outro documento de referência | Externo |
| **181** | tpDedRed | Elemento | 168 - docDedRed | DedRedTipo | 0-1 | Tipo de dedução/Redução modelo nacional | Externo |
| **182** | xDescOutDed | Elemento | 168 - docDedRed | DedRetxDesc | 0-1 | Descrição da Dedução/Redução | Externo |
| **183** | dtEmiDoc | Elemento | 168 - docDedRed | DedDEmis | 0-1 | Data de emissão da NF-e de referência | Externo |
| **184** | vDedutivelRedutivel | Elemento | 168 - docDedRed | DedvlTotRef | 0-1 | Valor total da nota de referencia de dedução | Externo |
| **185** | vDeducaoReducao | Elemento | 168 - docDedRed | DedValor | 0-1 | Valor da Dedução da lista de deduções | Externo |
| **186** | fornec | Grupo | 168 - docDedRed | | 0-1 | | Interno |
| **187** | CNPJ | Elemento | 186 - fornec | DedCNPJRef | 0-1 | CNPJ do fornecedor ou prestador | Externo |
| **188** | CPF | Elemento | 186 - fornec | DedCPFRef | 0-1 | CPF do fornecedor ou prestador | Externo |
| **189** | NIF | Elemento | 186 - fornec | DedNifFornecedor | 0-1 | NIF do Fornecedor do exterior | Externo |
| **190** | cNaoNIF | Elemento | 186 - fornec | DedForncExSemNIF | 0-1 | Motivo para não informação do NIF do fornecedor | Externo |
| **191** | CAEPF | Elemento | 186 - fornec | DedFornCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| **192** | IM | Elemento | 186 - fornec | DedFornIM | 0-1 | Inscrição Municipal do fornecedor | Externo |
| **193** | xNome | Elemento | 186 - fornec | DedFornNome | 0-1 | Razão Social do fornecedor | Externo |
| **194** | end | Grupo | 186 - fornec | | 0-1 | | Interno |
| **195** | endNac | Grupo | 194 - end | | 0-1 | | Interno |
| **196** | cMun | Elemento | 195 - endNac | DedForncMun | 0-1 | Código IBGE do fornecedor | Externo |
| **197** | CEP | Elemento | 195 - endNac | DedFornCEP | 0-1 | CEP do fornecedor | Externo |
| **198** | endExt | Grupo | 194 - end | | 0-1 | | Interno |
| **199** | cPais | Elemento | 198 - endExt | | 0-1 | | Interno |
| **200** | cEndPost | Elemento | 198 - endExt | DedFornCEPEx | 0-1 | CEP do fornecedor no exterior | Interno |
| **201** | xCidade | Elemento | 198 - endExt | DedFornxMunEx | 0-1 | Nome do Município do fornecedor no exterior | Interno |
| **202** | xEstProvReg | Elemento | 198 - endExt | DedFornUFEx | 0-1 | UF/Província/região do fornecedor no exterior | Interno |
| **203** | xLgr | Elemento | 194 - end | DedFornLogr | 0-1 | Logradouro do endereço do Fornecedor | Externo |
| **204** | nro | Elemento | 194 - end | DedFornNumero | 0-1 | Número do endereço do Fornecedor | Externo |
| **205** | xCpl | Elemento | 194 - end | DedFornCompl | 0-1 | Complemento do endereço do Fornecedor | Externo |
| **206** | xBairro | Elemento | 194 - end | DedFornBairro | 0-1 | Bairro do endereço do Fornecedor | Externo |
| **207** | fone | Elemento | 186 - fornec | DedFornFone | 0-1 | Telefone do fornecedor com DDD | Externo |
| **208** | email | Elemento | 186 - fornec | DedFornEmail | 0-1 | E-mail do fornecedor | Externo |
| **209** | trib | Grupo | 157 - valores | | 0-1 | | Interno |
| **210** | tribMun | Grupo | 209 - trib | | 0-1 | | Interno |
| **211** | tribISSQN | Elemento | 210 - tribMun | natOp | 1-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| **212** | cPaisResult | Elemento | 210 - tribMun | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| **213** | BM | Grupo | 210 - tribMun | | 0-1 | | Interno |
| **214** | tpBM | Elemento | 213 - BM | benefIdNac | 0-1 | Tipo do Benefício Municipal | Externo |
| **215** | nBM | Elemento | 213 - BM | benefNumero | 0-1 | Identificação da Lei parametrizada pelo município que define o benefício no sistema nacional | Externo |
| **216** | vRedBCBM | Elemento | 213 - BM | ValRedBenef | 0-1 | Valor monetário para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| **217** | pRedBCBM | Elemento | 213 - BM | PercRedBenef | 0-1 | Valor percentual para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| **218** | exigSusp | Grupo | 210 - tribMun | | 0-1 | | Interno |
| **219** | tpSusp | Elemento | 218 - exigSusp | tpSusp | 0-1 | Opção para Exigibilidade Suspensa | Externo |
| **220** | nProcesso | Elemento | 218 - exigSusp | NumProcesso | 0-1 | Número do Processo Administrativo | Externo |
| **221** | tpImunidade | Elemento | 210 - tribMun | tpImunidade | 0-1 | Identificação da Imunidade do ISSQN – somente para o caso de Imunidade | Externo |
| **222** | tpRetISSQN | Elemento | 210 - tribMun | ISSRetido | 0-1 | É ISS Retido? | Externo |
| **223** | pAliq | Elemento | 210 - tribMun | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| **224** | tribFed | Grupo | 209 - trib | | 0-1 | | Interno |
| **225** | piscofins | Grupo | 224 - tribFed | | 0-1 | | Interno |
| **226** | CST | Elemento | 225 - piscofins | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo |
| **227** | vBCPisCofins | Elemento | 225 - piscofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| **228** | pAliqPis | Elemento | 225 - piscofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| **229** | pAliqCofins | Elemento | 225 - piscofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| **230** | vPis | Elemento | 225 - piscofins | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| **231** | vCofins | Elemento | 225 - piscofins | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| **232** | tpRetPisCofins | Elemento | 225 - piscofins | tpRetPISCofins | 0-1 | Tipo de Retenção PIS/COFINS e CSLL | Externo |
| **233** | vRetCP | Elemento | 224 - tribFed | ValINSS | 0-1 | Valor do INSS do serviço | Externo |
| **234** | vRetIRRF | Elemento | 224 - tribFed | ValIR | 0-1 | Valor do IRRF do serviço | Externo |
| **235** | vRetCSLL | Elemento | 224 - tribFed | ValCSLL | 0-1 | Valor do CSLL | Externo |
| **236** | totTrib | Grupo | 209 - trib | | 1-1 | | Interno |
| **237** | vTotTrib | Grupo | 236 - totTrib | | 0-1 | | Interno |
| **238** | vTotTribFed | Elemento | 237 - vTotTrib | ValRetFederais | 0-1 | Valor total das retenções federais | Externo |
| **239** | vTotTribEst | Elemento | 237 - vTotTrib | ValRetEstaduais | 0-1 | Valor monetário total aproximado dos tributos estaduais | Externo |
| **240** | vTotTribMun | Elemento | 237 - vTotTrib | ValRetMunicipais | 0-1 | Valor monetário total aproximado dos tributos municipais | Externo |
| **241** | pTotTrib | Grupo | 236 - totTrib | | 0-1 | | Interno |
| **242** | pTotTribFed | Elemento | 241 - pTotTrib | ValAliqRetFederais | 0-1 | Valor percentual total aproximado dos tributos federais | Externo |
| **243** | pTotTribEst | Elemento | 241 - pTotTrib | ValAliqRetEstaduais | 0-1 | Valor percentual total aproximado dos tributos estaduais | Externo |
| **244** | pTotTribMun | Elemento | 241 - pTotTrib | ValAliqRetMunicipais | 0-1 | Valor percentual total aproximado dos tributos municipais | Externo |
| **245** | indTotTrib | Elemento | 236 - totTrib | indTotTrib | 0-1 | Indicador de informação de valor total de tributos. | Interno |
| **246** | pTotTribSN | Elemento | 236 - totTrib | ValAproxTribAliqSN | 0-1 | Valor percentual aproximado do total dos tributos da alíquota do Simples Nacional (%) | Externo |
| **247** | IBSCBS | Grupo | 2 - infDPS | | 0-1 | | Interno |
| **248** | finNFSe | Elemento | 247 - IBSCBS | finNFSe | 0-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| **249** | indFinal | Elemento | 247 - IBSCBS | indFinal | 1-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo |
| **250** | cIndOp | Elemento | 247 - IBSCBS | cIndOp | 0-1 | Código indicador da operação de fornecimento, conforme tabela "código indicador de operação" | Externo |
| **251** | indZFMALC | Elemento | 247 - IBSCBS | indZFMALC | 0-1 | Indicador da operação de fornecimento favorecido com alíquota zero de CBS | Externo |
| **252** | tpOper | Elemento | 247 - IBSCBS | tpOper | 0-1 | Tipo de Operação com Entes Governanementais ou outros serviços sobre bens imóveis | Externo |
| **253** | gRefNFSe | Grupo | 247 - IBSCBS | ListaNFSeRef | 0-1 | | Interno |
| **254** | refNFSe | Elemento | 253 - gRefNFSe | DocChaveNFSe | 1-N | Chave da NFS-e referenciada | Externo |
| **255** | tpEnteGov | Elemento | 247 - IBSCBS | tpEnteGov | 0-1 | Tipo de ente governamental Para administração pública direta e suas autarquias e fundações | Externo |
| **256** | indDest | Elemento | 247 - IBSCBS | indDest | 1-1 | A respeito do Destinatário dos serviços: 0 – O tomador na NFS-e é o adquirente e o destinatário | Externo |
| **257** | dest | Grupo | 247 - IBSCBS | | 0-1 | | Interno |
| **258** | CNPJ | Elemento | 257 - dest | DestCNPJ | 0-1 | Número da inscrição no Cadastro Nacional de Pessoa Jurídica do destinatário do serviço | Externo |
| **259** | CPF | Elemento | 257 - dest | DestCPF | 0-1 | CPF do destinatário | Externo |
| **260** | NIF | Elemento | 257 - dest | DestNIF | 0-1 | Número de identificação fiscal fornecido por órgão de administração trib. do exterior | Externo |
| **261** | cNaoNIF | Elemento | 257 - dest | DestcNaoNIF | 0-1 | Motivo não informe NIF | Externo |
| **262** | xNome | Elemento | 257 - dest | DestNome | 0-1 | Nome / Nome Empresarial do destinatário | Externo |
| **263** | end | Grupo | 257 - dest | | 0-1 | | Interno |
| **264** | endNac | Grupo | 263 - end | | 0-1 | | Interno |
| **265** | cMun | Elemento | 264 - endNac | DestcMun | 0-1 | Código do município do endereço do destinatário (IBGE) | Externo |
| **266** | CEP | Elemento | 264 - endNac | DestCEP | 0-1 | Código numérico do Endereçamento Postal nacional do destinatário | Externo |
| **267** | endExt | Grupo | 263 - end | | 0-1 | | Interno |
| **268** | cPais | Elemento | 267 - endExt | DestPais | 0-1 | País do destinatário | Externo |
| **269** | cEndPost | Elemento | 267 - endExt | DestCEP | 0-1 | Código numérico do Endereçamento Postal nacional do destinatário | Externo |
| **270** | xCidade | Elemento | 267 - endExt | DestxMun | 0-1 | Nome da cidade no exterior do destinatário | Externo |
| **271** | xEstProvReg | Elemento | 267 - endExt | DestEstProvReg | 0-1 | Estado, província ou região da cidade no exterior do destinatário | Externo |
| **272** | xLgr | Elemento | 263 - end | DestEndereco | 0-1 | Tipo e nome do logradouro do endereço do destinatário | Externo |
| **273** | nro | Elemento | 263 - end | DestNumero | 0-1 | Número no logradouro do endereço do destinatário | Externo |
| **274** | xCpl | Elemento | 263 - end | DestComplemento | 0-1 | Complemento do endereço do destinatário | Externo |
| **275** | xBairro | Elemento | 263 - end | DestBairro | 0-1 | Bairro do endereço do destinatário do serviço. | Externo |
| **276** | fone | Elemento | 257 - dest | DestFone | 0-1 | Número do telefone do destinatário | Externo |
| **277** | email | Elemento | 257 - dest | DestEmail | 0-1 | Email do destinatário | Externo |
| **278** | imovel | Grupo | 247 - IBSCBS | | 0-1 | | Interno |
| **279** | inscImobFisc | Elemento | 278 - imovel | ImovInscImobFisc | 0-1 | | Externo |
| **280** | cCIB | Elemento | 278 - imovel | ImovCIB | 0-1 | Código do Cadastro Imobiliário Brasileiro - CIB | Externo |
| **281** | end | Grupo | 278 - imovel | | 0-1 | | Interno |
| **282** | CEP | Elemento | 281 - end | ImovCEP | 0-1 | Código de Endereçamento Postal numérico do endereço nacional do imóvel. | Externo |
| **283** | endExt | Grupo | 281 - end | | 0-1 | | Interno |
| **284** | cEndPost | Elemento | 283 - endExt | ImovCEP | 0-1 | Código de Endereçamento Postal numérico do endereço nacional do imóvel. | Externo |
| **285** | xCidade | Elemento | 283 - endExt | ImovxMun | 0-1 | Nome da cidade no exterior, local do imóvel. | Externo |
| **286** | xEstProvReg | Elemento | 283 - endExt | ImovEstProvReg | 0-1 | Estado, província ou região da cidade no exterior, local do imóvel. | Externo |
| **287** | xLgr | Elemento | 281 - end | ImovEndereco | 0-1 | Tipo e nome do logradouro do endereço do imóvel | Externo |
| **288** | nro | Elemento | 281 - end | ImovNumero | 0-1 | Número no logradouro do endereço do imóvel. | Externo |
| **289** | xCpl | Elemento | 281 - end | ImovComplemento | 0-1 | Complemento do endereço do imóvel. | Externo |
| **290** | xBairro | Elemento | 281 - end | ImovBairro | 0-1 | Bairro do endereço do imóvel. | Externo |
| **291** | valores | Grupo | 247 - IBSCBS | | 0-1 | | Interno |
| **292** | gReeRepRes | Grupo | 291 - valores | | 0-1 | | Interno |
| **293** | documentos | Grupo | 292 - gReeRepRes | ListaDocumentosRef | 1-N | | Interno |
| **294** | dFeNacional | Grupo | 293 - documentos | | 0-1 | | Interno |
| **295** | tipoChaveDFe | Elemento | 294 - dFeNacional | DocTipoChaveDFe | 0-1 | Documento fiscal a que se refere a chaveDfe: 1 = NFS-e, 2 = NF-e, 3 = CT-e, 9 = Outro | Externo |
| **296** | xTipoChaveDFe | Elemento | 294 - dFeNacional | DocDescTipoChaveDFe | 0-1 | Descrição da DF-e a que se refere a chaveDfe que seja um dos documentos do Repositório Nacional | Externo |
| **297** | chaveDFe | Elemento | 294 - dFeNacional | DocChaveDFe | 0-1 | Chave do Documento Fiscal eletrônico do repositório nacional referenciado operações já tributadas. | Externo |
| **298** | docFiscalOutro | Grupo | 293 - documentos | | 0-1 | | Interno |
| **299** | cMunDocFiscal | Elemento | 298 - docFiscalOutro | DoccMun | 1-1 | Código do município emissor do documento fiscal que não se encontra no repositório nacional | Externo |
| **300** | nDocFiscal | Elemento | 298 - docFiscalOutro | DocNumero | 0-1 | Número do documento fiscal | Externo |
| **301** | xDocFiscal | Elemento | 298 - docFiscalOutro | DocDescricao | 0-1 | Descrição do documento fiscal. | Externo |
| **302** | docOutro | Grupo | 293 - documentos | | 0-1 | | Interno |
| **303** | nDoc | Elemento | 302 - docOutro | DocNumero | 0-1 | Número do documento fiscal | Externo |
| **304** | xDoc | Elemento | 302 - docOutro | DocDescricao | 0-1 | Descrição do documento fiscal. | Externo |
| **305** | fornec | Grupo | 293 - documentos | | 0-1 | | Interno |
| **307** | CNPJ | Elemento | 305 - fornec | DocCNPJ | 0-1 | Número da inscrição federal (CNPJ) do fornecedor do documento | Externo |
| **308** | CPF | Elemento | 305 - fornec | DocCPF | 0-1 | Número da inscrição federal (CPF) do fornecedor. | Externo |
| **309** | NIF | Elemento | 305 - fornec | DocNIF | 0-1 | Este elemento só deverá ser preenchido para fornecedores não residentes no Brasil. | Externo |
| **310** | cNaoNIF | Elemento | 305 - fornec | DoccNaoNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **311** | xNome | Elemento | 305 - fornec | DocRazaoSocial | 0-1 | Nome / Razão Social do fornecedor. | Externo |
| **312** | dtEmiDoc | Elemento | 293 - documentos | DocDataEmissao | 0-1 | Data da emissão do documento dedutível. Ano, mês e dia (AAAA-MM-DD) | Externo |
| **313** | dtCompDoc | Elemento | 293 - documentos | DocDCompetencia | 0-1 | Data da competência do documento dedutível. Ano, mês e dia (AAAA-MM-DD) | Externo |
| **314** | tpReeRepRes | Elemento | 293 - documentos | DocTipoReeRepRes | 0-1 | Tipo de valor incluído neste documento | Externo |
| **315** | xTpReeRepRes | Elemento | 293 - documentos | DocDescTpReeRepRes | 0-1 | Descrição do reembolso ou ressarcimento quando a opção é "99" | Externo |
| **316** | vlrReeRepRes | Elemento | 293 - documentos | DocValorReeRepRes | 0-1 | Valor monetário (total ou parcial, conforme documento informado) | Externo |
| **317** | trib | Grupo | 291 - valores | | 0-1 | | Interno |
| **318** | gIBSCBS | Grupo | 317 - trib | | 0-1 | | Interno |
| **319** | CST | Elemento | 318 - gIBSCBS | CSTIBSCBS | 0-1 | Código de Situação Tributária do IBS e da CBS | Externo |
| **320** | cClassTrib | Elemento | 318 - gIBSCBS | cClassTrib | 0-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo |
| **321** | cCredPres | Elemento | 318 - gIBSCBS | cCredPres | 0-1 | Código e classificação do crédito presumido: IBS e CBS | Externo |
| **322** | gTribRegular | Grupo | 318 - gIBSCBS | | 0-1 | | Interno |
| **323** | CSTReg | Elemento | 322 - gTribRegular | CSTReg | 0-1 | Código de Situação Tributária | Externo |
| **324** | cClassTribReg | Elemento | 322 - gTribRegular | cClassTribReg | 0-1 | Código da Classificação Tributária | Externo |
| **325** | gDif | Grupo | 318 - gIBSCBS | | 0-1 | | Interno |
| **326** | pDifUF | Elemento | 325 - gDif | ValpDifUF | 0-1 | Percentual de diferimento para o IBS estadual | Externo |
| **327** | pDifMun | Elemento | 325 - gDif | ValpDifMun | 0-1 | Percentual de diferimento para o IBS municipal | Externo |
| **328** | pDifCBS | Elemento | 325 - gDif | ValpDifCBS | 0-1 | Percentual de diferimento para a CBS | Externo |
