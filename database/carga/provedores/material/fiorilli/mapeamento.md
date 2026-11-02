# Mapeamento "De/Para" - Layout InvoiCy (DPS / NFS-e)

Este documento contém o mapeamento estruturado entre as **TAGs da Prefeitura (DPS)** e os **Campos do InvoiCy**, com remoção de duplicidades encontradas nos logs de extração.

---

## Tabela de Mapeamento

| Sequencial | TAG Prefeitura | Elemento XML | Dentro do Grupo | Nome do Campo (Tag InvoiCy) | Ocorrência | Descrição | Visibilidade |
| :---: | :--- | :--- | :--- | :--- | :---: | :--- | :---: |
| **1** | RecepcionarDpsEnvio | Grupo | 0 - | | 1-1 | | Interno |
| **10** | DPS | Grupo | 1 - RecepcionarDpsEnvio | | 0-1 | | Interno |
| **12** | versao | Atributo | 10 - DPS | | 1-1 | | Interno |
| **13** | infDPS | Grupo | 10 - DPS | | 0-1 | | Interno |
| **15** | Id | Atributo | 13 - infDPS | RPSNumero e RPSSerie | 0-1 | Id do Grupo RPS | Interno |
| **17** | tpAmb | Elemento | 13 - infDPS | tpAmb | 1-1 | Ambiente Emissão | Interno |
| **18** | dhEmi | Elemento | 13 - infDPS | dEmis | 0-1 | Data de Emissão | Externo |
| **19** | verAplic | Elemento | 13 - infDPS | | 0-1 | | Interno |
| **20** | serie | Elemento | 13 - infDPS | RPSSerie | 0-1 | Série do RPS | Externo |
| **21** | nDPS | Elemento | 13 - infDPS | RPSNumero | 0-1 | Número do RPS | Externo |
| **22** | dCompet | Elemento | 13 - infDPS | dCompetencia | 0-1 | Data de Competência | Externo |
| **23** | tpEmit | Elemento | 13 - infDPS | | 1-1 | | Interno |
| **24** | cMotivoEmisTI | Elemento | 13 - infDPS | | 0-1 | | Interno |
| **25** | chNFSeRej | Elemento | 13 - infDPS | | 0-1 | | Interno |
| **26** | cLocEmi | Elemento | 13 - infDPS | cMun | 0-1 | Código do Município do prestador | Externo |
| **27** | subst | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **28** | chSubstda | Elemento | 27 - subst | SubsChave | 0-1 | Chave de Acesso NFSe Substituída | Externo |
| **29** | cMotivo | Elemento | 27 - subst | SubsCodMotivo | 0-1 | Código do Motivo da substituição | Externo |
| **30** | xMotivo | Elemento | 27 - subst | SubsDescMotivo | 0-1 | Descrição do Motivo da substituição | Externo |
| **31** | prest | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **32** | CNPJ | Elemento | 31 - prest | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| **33** | CPF | Elemento | 31 - prest | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| **34** | NIF | Elemento | 31 - prest | | 0-1 | | Interno |
| **35** | cNaoNIF | Elemento | 31 - prest | | 0-1 | | Interno |
| **36** | CAEPF | Elemento | 31 - prest | CAEPF | 0-1 | Número do CAEPF do prestador | Externo |
| **37** | IM | Elemento | 31 - prest | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| **38** | xNome | Elemento | 31 - prest | | 0-1 | | Interno |
| **39** | end | Grupo | 31 - prest | | 0-1 | | Interno |
| **40** | endNac | Grupo | 39 - end | | 0-1 | | Interno |
| **41** | cMun | Elemento | 40 - endNac | | 0-1 | | Interno |
| **42** | CEP | Elemento | 40 - endNac | | 0-1 | | Interno |
| **43** | endExt | Grupo | 39 - end | | 0-1 | | Interno |
| **44** | cPais | Elemento | 43 - endExt | | 0-1 | | Interno |
| **45** | cEndPost | Elemento | 43 - endExt | | 0-1 | | Interno |
| **46** | xCidade | Elemento | 43 - endExt | | 0-1 | | Interno |
| **47** | xEstProvReg | Elemento | 43 - endExt | | 0-1 | | Interno |
| **48** | xLgr | Elemento | 39 - end | | 0-1 | | Interno |
| **49** | nro | Elemento | 39 - end | | 0-1 | | Interno |
| **50** | xCpl | Elemento | 39 - end | | 0-1 | | Interno |
| **51** | xBairro | Elemento | 39 - end | | 0-1 | | Interno |
| **52** | fone | Elemento | 31 - prest | | 0-1 | | Interno |
| **53** | email | Elemento | 31 - prest | | 0-1 | | Interno |
| **54** | regTrib | Grupo | 31 - prest | | 0-1 | | Interno |
| **55** | opSimpNac | Elemento | 54 - regTrib | OptSN | 0-1 | Optante Simples Nacional | Externo |
| **56** | regApTribSN | Elemento | 54 - regTrib | regApTribSN | 0-1 | Regime de tributação SN ME/EPP além sublimite | Externo |
| **57** | regEspTrib | Elemento | 54 - regTrib | RegEspTrib | 1-1 | Regime Especial de Tributação | Externo |
| **58** | toma | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **59** | CNPJ | Elemento | 58 - toma | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo |
| **60** | CPF | Elemento | 58 - toma | TomaCPF | 0-1 | CPF do Tomador | Externo |
| **61** | NIF | Elemento | 58 - toma | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| **62** | cNaoNIF | Elemento | 58 - toma | TomaExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **63** | CAEPF | Elemento | 58 - toma | TomaCAEPF | 0-1 | Número do CAEPF do Tomador | Externo |
| **64** | IM | Elemento | 58 - toma | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo |
| **65** | xNome | Elemento | 58 - toma | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo |
| **66** | end | Grupo | 58 - toma | | 0-1 | | Interno |
| **67** | endNac | Grupo | 66 - end | | 0-1 | | Interno |
| **68** | cMun | Elemento | 67 - endNac | TomacMun | 0-1 | Código do Município IBGE do Tomador | Externo |
| **69** | CEP | Elemento | 67 - endNac | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| **70** | endExt | Grupo | 66 - end | | 0-1 | | Interno |
| **71** | cPais | Elemento | 70 - endExt | TomaPais | 0-1 | País do Tomador no Exterior | Externo |
| **72** | cEndPost | Elemento | 70 - endExt | TomaCEP | 0-1 | CEP do Tomador (exterior) | Externo |
| **73** | xCidade | Elemento | 70 - endExt | TomaXMun | 0-1 | Nome do Município do tomador (Exterior) | Externo |
| **74** | xEstProvReg | Elemento | 70 - endExt | TomaUF | 0-1 | UF do estado do Tomador (exterior) | Externo |
| **75** | xLgr | Elemento | 66 - end | TomaEndereco | 0-1 | Endereço do Tomador | Externo |
| **76** | nro | Elemento | 66 - end | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| **77** | xCpl | Elemento | 66 - end | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo |
| **78** | xBairro | Elemento | 66 - end | TomaBairro | 0-1 | Bairro do Tomador | Externo |
| **79** | fone | Elemento | 58 - toma | TomaTelefone | 0-1 | Telefone do Tomador | Externo |
| **80** | email | Elemento | 58 - toma | TomaEmail | 0-1 | Email do Tomador | Externo |
| **81** | interm | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **82** | CNPJ | Elemento | 81 - interm | IntermCNPJ | 0-1 | CNPJ do Intermediário | Externo |
| **83** | CPF | Elemento | 81 - interm | IntermCPF | 0-1 | CPF do Intermediário | Externo |
| **84** | NIF | Elemento | 81 - interm | IntermNIF | 0-1 | Doc intermediário estrangeiro | Externo |
| **85** | cNaoNIF | Elemento | 81 - interm | IntermExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **86** | CAEPF | Elemento | 81 - interm | IntermCAEPF | 0-1 | Número do CAEPF | Externo |
| **87** | IM | Elemento | 81 - interm | IntermIM | 0-1 | Inscrição Municipal do Intermediário | Externo |
| **88** | xNome | Elemento | 81 - interm | IntermRazaoSocial | 0-1 | Razão Social do Intermediário | Externo |
| **89** | end | Grupo | 81 - interm | | 0-1 | | Interno |
| **90** | endNac | Grupo | 89 - end | | 0-1 | | Interno |
| **91** | cMun | Elemento | 90 - endNac | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo |
| **92** | CEP | Elemento | 90 - endNac | IntermCep | 0-1 | CEP do Intermediário (nacional) | Externo |
| **93** | endExt | Grupo | 89 - end | | 0-1 | | Interno |
| **94** | cPais | Elemento | 93 - endExt | IntermPais | 0-1 | Código do País do Intermediário | Externo |
| **95** | cEndPost | Elemento | 93 - endExt | IntermCep | 0-1 | CEP do intermediário (exterior) | Externo |
| **96** | xCidade | Elemento | 93 - endExt | IntermXmun | 0-1 | Nome do município do intermediário (exterior) | Externo |
| **97** | xEstProvReg | Elemento | 93 - endExt | IntermUFEx | 0-1 | UF/província/região do intermediário no exterior | Interno |
| **98** | xLgr | Elemento | 89 - end | IntermEndereco | 0-1 | Logradouro do Endereço do Intermediário | Externo |
| **99** | nro | Elemento | 89 - end | IntermNumero | 0-1 | Número do Endereço do Intermediário | Externo |
| **100** | xCpl | Elemento | 89 - end | IntermComplemento | 0-1 | Complemento do Endereço do Intermediário | Externo |
| **101** | xBairro | Elemento | 89 - end | IntermBairro | 0-1 | Bairro do Intermediário | Externo |
| **102** | fone | Elemento | 81 - interm | IntermFone | 0-1 | Telefone do Intermediário | Externo |
| **103** | email | Elemento | 81 - interm | IntermEmail | 0-1 | E-mail do Intermediário | Externo |
| **104** | serv | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **105** | locPrest | Grupo | 104 - serv | | 1-1 | | Interno |
| **106** | cLocPrestacao | Elemento | 105 - locPrest | SerEndcMun | 0-1 | Local da Prestação do Serviço Cod IBGE Municipio | Externo |
| **107** | cPaisPrestacao | Elemento | 105 - locPrest | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| **109** | cServ | Grupo | 104 - serv | | 0-1 | | Interno |
| **110** | cTribNac | Elemento | 109 - cServ | codTributNacional | 0-1 | Código de tributação Nacional | Externo |
| **111** | cTribMun | Elemento | 109 - cServ | TributMunicipio | 0-1 | Código de Tributação do Município | Externo |
| **112** | xDescServ | Elemento | 109 - cServ | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| **113** | cNBS | Elemento | 109 - cServ | CodigoNBS | 0-1 | Código NBS | Externo |
| **114** | cIntContrib | Elemento | 109 - cServ | | 0-1 | | Interno |
| **115** | comExt | Grupo | 104 - serv | | 0-1 | | Interno |
| **116** | mdPrestacao | Elemento | 115 - comExt | mdPrestacao | 0-1 | Modo de Prestação | Externo |
| **117** | vincPrest | Elemento | 115 - comExt | vincPrest | 0-1 | Vínculo entre as partes no negócio | Externo |
| **118** | tpMoeda | Elemento | 115 - comExt | tpMoeda | 0-1 | Identifica a moeda da transação comercial | Externo |
| **119** | vServMoeda | Elemento | 115 - comExt | ValServEst | 0-1 | Valor do Serviço em Moeda estrangeira | Externo |
| **120** | mecAFComexP | Elemento | 115 - comExt | mecAFComexP | 0-1 | Mecanismo fomento Comex (prestador) | Externo |
| **121** | mecAFComexT | Elemento | 115 - comExt | mecAFComexT | 0-1 | Mecanismo fomento Comex (tomador) | Externo |
| **122** | movTempBens | Elemento | 115 - comExt | movTempBens | 0-1 | Vinculado à Movimentação Temporária de Bens | Externo |
| **123** | nDI | Elemento | 115 - comExt | nroDUIMP | 0-1 | Número da Declaração de Importação (DI/DUIMP) | Externo |
| **124** | nRE | Elemento | 115 - comExt | nroDUE | 0-1 | Número do Registro de Exportação (RE/DUE) | Externo |
| **125** | mdic | Elemento | 115 - comExt | mdic | 0-1 | Compartilhar informações com SECEX | Externo |
| **126** | lsadppu | Grupo | 104 - serv | | 0-1 | | Interno |
| **127** | categ | Elemento | 126 - lsadppu | categServ | 0-1 | Categoria do serviço | Externo |
| **128** | objeto | Elemento | 126 - lsadppu | objetoLocacao | 0-1 | Tipo de objetos da locação/permissão | Externo |
| **129** | extensao | Elemento | 126 - lsadppu | extensaoFerrovia | 0-1 | Extensão total ferrovia/rodovia/dutos | Externo |
| **130** | nPostes | Elemento | 126 - lsadppu | nPostes | 0-1 | Número total de postes | Externo |
| **131** | obra | Grupo | 104 - serv | | 0-1 | | Interno |
| **132** | inscImobFisc | Elemento | 131 - obra | ObraMatricula | 0-1 | Número de Matrícula da Obra | Externo |
| **133** | cObra | Elemento | 131 - obra | CodObra | 0-1 | Código da Obra | Externo |
| **134** | end | Grupo | 131 - obra | | 0-1 | | Interno |
| **135** | CEP | Elemento | 134 - end | ObraCEP | 0-1 | CEP da Obra | Externo |
| **136** | endExt | Grupo | 134 - end | | 0-1 | | Interno |
| **137** | cEndPost | Elemento | 136 - endExt | ObraCEP | 0-1 | CEP da Obra (exterior) | Externo |
| **138** | xCidade | Elemento | 136 - endExt | ObraXMunEx | 0-1 | Nome do município da Obra no exterior | Interno |
| **139** | xEstProvReg | Elemento | 136 - endExt | ObraUF | 0-1 | UF da obra do exterior | Externo |
| **140** | xLgr | Elemento | 134 - end | ObraLog | 0-1 | Logradouro da Obra | Externo |
| **141** | nro | Elemento | 134 - end | ObraNumero | 0-1 | Número do End. da Obra | Externo |
| **142** | xCpl | Elemento | 134 - end | ObraCompl | 0-1 | Complemento do End. da Obra | Externo |
| **143** | xBairro | Elemento | 134 - end | ObraBairro | 0-1 | Bairro da Obra | Externo |
| **144** | atvEvento | Grupo | 104 - serv | | 0-1 | | Interno |
| **145** | xNome | Elemento | 144 - atvEvento | AtivDesc | 0-1 | Descrição do evento | Externo |
| **146** | dtIni | Elemento | 144 - atvEvento | AtivDataInicial | 0-1 | Data de início do evento | Externo |
| **147** | dtFim | Elemento | 144 - atvEvento | AtivDataFinal | 0-1 | Data de fim do evento | Externo |
| **148** | idAtvEvt | Elemento | 144 - atvEvento | AtivIdEvento | 0-1 | Identificação da Atividade de Evento | Externo |
| **149** | end | Grupo | 144 - atvEvento | | 0-1 | | Interno |
| **150** | CEP | Elemento | 149 - end | AtivEndCEP | 0-1 | CEP do endereço do evento | Externo |
| **151** | endExt | Grupo | 149 - end | | 0-1 | | Interno |
| **152** | cEndPost | Elemento | 151 - endExt | AtivEndCEPEx | 0-1 | CEP do evento no exterior | Externo |
| **153** | xCidade | Elemento | 151 - endExt | AtivEndxMunEx | 0-1 | Nome do município do evento no exterior | Interno |
| **154** | xEstProvReg | Elemento | 151 - endExt | AtivEndUfEx | 0-1 | UF/província do evento no exterior | Interno |
| **155** | xLgr | Elemento | 149 - end | AtivEndLogradouro | 0-1 | Logradouro do evento | Externo |
| **156** | nro | Elemento | 149 - end | AtivEndNumero | 0-1 | Número do endereço do evento | Externo |
| **157** | xCpl | Elemento | 149 - end | AtivEndCompl | 0-1 | Complemento do endereço do evento | Externo |
| **158** | xBairro | Elemento | 149 - end | AtivEndBairro | 0-1 | Bairro do endereço do evento | Externo |
| **159** | explRod | Grupo | 104 - serv | | 0-1 | | Interno |
| **160** | categVeic | Elemento | 159 - explRod | PedCategVeiculo | 0-1 | Categorias de veículos para cobrança | Externo |
| **161** | nEixos | Elemento | 159 - explRod | PednEixos | 0-1 | Número de eixos | Externo |
| **162** | rodagem | Elemento | 159 - explRod | PedRodagem | 0-1 | Tipo de rodagem | Externo |
| **163** | sentido | Elemento | 159 - explRod | PedSentido | 0-1 | Orientação de passagem do veículo | Externo |
| **164** | placa | Elemento | 159 - explRod | PedPlaca | 0-1 | Placa do veículo | Externo |
| **165** | codAcessoPed | Elemento | 159 - explRod | PedCodAcesso | 0-1 | Código de acesso gerado pela concessionária | Externo |
| **166** | codContrato | Elemento | 159 - explRod | PedCodContrato | 0-1 | Código de contrato no cadastro nacional | Externo |
| **167** | infoCompl | Grupo | 104 - serv | | 0-1 | | Interno |
| **168** | idDocTec | Elemento | 167 - infoCompl | idDocTec | 0-1 | Doc Resp Tec (ART, RRT, etc) | Externo |
| **169** | docRef | Elemento | 167 - infoCompl | docRef | 0-1 | Chave da nota fiscal referenciada | Externo |
| **170** | xInfComp | Elemento | 167 - infoCompl | NFSOutrasinformacoes | 0-1 | Outras Informações / observações | Externo |
| **171** | valores | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **172** | vServPrest | Grupo | 171 - valores | | 0-1 | | Interno |
| **173** | vReceb | Elemento | 172 - vServPrest | | 0-1 | | Interno |
| **174** | vServ | Elemento | 172 - vServPrest | ValServicos | 0-1 | Valor do Serviço | Externo |
| **175** | vDescCondIncond | Grupo | 171 - valores | | 0-1 | | Interno |
| **176** | vDescIncond | Elemento | 175 - vDescCondIncond | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo |
| **177** | vDescCond | Elemento | 175 - vDescCondIncond | ValDescCond | 0-1 | Valor desconto Condicionado | Externo |
| **178** | vDedRed | Grupo | 171 - valores | | 0-1 | | Interno |
| **179** | pDR | Elemento | 178 - vDedRed | ValPercDeducoes | 0-1 | Percentual padrão para dedução/redução | Externo |
| **180** | vDR | Elemento | 178 - vDedRed | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo |
| **181** | documentos | Grupo | 178 - vDedRed | Ded | 0-1 | Grupo Dedução (controle) | Interno |
| **182** | docDedRed | Grupo | 181 - documentos | | 0-1 | | Interno |
| **183** | chNFSe | Elemento | 182 - docDedRed | DedChAcesNacional | 0-1 | Chave de Acesso da NFS-e Nacional | Externo |
| **184** | chNFe | Elemento | 182 - docDedRed | DedChaveAcesso | 0-1 | Chave de acesso da NF-e de referência | Externo |
| **185** | NFSeMun | Grupo | 182 - docDedRed | | 0-1 | | Interno |
| **186** | cMunNFSeMun | Elemento | 185 - NFSeMun | DedcMunNFSe | 0-1 | Código do município da NFS-e ref. | Externo |
| **187** | nNFSeMun | Elemento | 185 - NFSeMun | | 0-1 | | Interno |
| **188** | cVerifNFSeMun | Elemento | 185 - NFSeMun | DedcVerificaNFSe | 0-1 | Código de verificação da NFS-e ref. | Externo |
| **189** | NFNFS | Grupo | 182 - docDedRed | | 0-1 | | Interno |
| **190** | nNFS | Elemento | 189 - NFNFS | DednNFRef | 0-1 | Número da nota de referência da Dedução | Externo |
| **191** | modNFS | Elemento | 189 - NFNFS | | 0-1 | | Interno |
| **192** | serieNFS | Elemento | 189 - NFNFS | | 0-1 | | Interno |
| **193** | nDocFisc | Elemento | 182 - docDedRed | DedOutroDocFisc | 0-1 | Número de outro documento fiscal | Externo |
| **194** | nDoc | Elemento | 182 - docDedRed | DedOutroDoc | 0-1 | Código de outro documento de referência | Externo |
| **195** | tpDedRed | Elemento | 182 - docDedRed | DedRedTipo | 0-1 | Tipo de dedução/Redução modelo nacional | Externo |
| **196** | xDescOutDed | Elemento | 182 - docDedRed | DedRetxDesc | 0-1 | Descrição da Dedução/Redução | Externo |
| **197** | dtEmiDoc | Elemento | 182 - docDedRed | DedDEmis | 0-1 | Data de emissão da NF-e de referência | Externo |
| **198** | vDedutivelRedutivel | Elemento | 182 - docDedRed | DedvlTotRef | 0-1 | Valor total da nota de referência | Externo |
| **199** | vDeducaoReducao | Elemento | 182 - docDedRed | DedValor | 0-1 | Valor da Dedução | Externo |
| **200** | fornec | Grupo | 182 - docDedRed | | 0-1 | | Interno |
| **201** | CNPJ | Elemento | 200 - fornec | DedCNPJRef | 0-1 | CNPJ do fornecedor ou prestador | Externo |
| **202** | CPF | Elemento | 200 - fornec | DedCPFRef | 0-1 | CPF do fornecedor ou prestador | Externo |
| **203** | NIF | Elemento | 200 - fornec | DedNifFornecedor | 0-1 | NIF do Fornecedor do exterior | Externo |
| **204** | cNaoNIF | Elemento | 200 - fornec | DedForncExSemNIF | 0-1 | Motivo sem NIF do fornecedor | Externo |
| **205** | CAEPF | Elemento | 200 - fornec | DedFornCAEPF | 0-1 | CAEPF do fornecedor | Externo |
| **206** | IM | Elemento | 200 - fornec | DedFornIM | 0-1 | Inscrição Municipal do fornecedor | Externo |
| **207** | xNome | Elemento | 200 - fornec | DedFornNome | 0-1 | Razão Social do fornecedor | Externo |
| **208** | end | Grupo | 200 - fornec | | 0-1 | | Interno |
| **209** | endNac | Grupo | 208 - end | | 0-1 | | Interno |
| **210** | cMun | Elemento | 209 - endNac | DedForncMun | 0-1 | Código IBGE do fornecedor | Externo |
| **211** | CEP | Elemento | 209 - endNac | DedFornCEP | 0-1 | CEP do fornecedor | Externo |
| **212** | endExt | Grupo | 208 - end | | 0-1 | | Interno |
| **213** | cPais | Elemento | 212 - endExt | DedPaisFornecedor | 0-1 | Código do país do Fornecedor | Externo |
| **214** | cEndPost | Elemento | 212 - endExt | DedFornCEPEx | 0-1 | CEP do fornecedor no exterior | Interno |
| **215** | xCidade | Elemento | 212 - endExt | DedFornxMunEx | 0-1 | Município do fornecedor no exterior | Interno |
| **216** | xEstProvReg | Elemento | 212 - endExt | DedFornUFEx | 0-1 | UF/Província do fornecedor no exterior | Interno |
| **217** | xLgr | Elemento | 208 - end | DedFornLogr | 0-1 | Logradouro do Fornecedor | Externo |
| **218** | nro | Elemento | 208 - end | DedFornNumero | 0-1 | Número do endereço do Fornecedor | Externo |
| **219** | xCpl | Elemento | 208 - end | DedFornCompl | 0-1 | Complemento do Fornecedor | Externo |
| **220** | xBairro | Elemento | 208 - end | DedFornBairro | 0-1 | Bairro do Fornecedor | Externo |
| **221** | fone | Elemento | 200 - fornec | DedFornFone | 0-1 | Telefone do fornecedor com DDD | Externo |
| **222** | email | Elemento | 200 - fornec | DedFornEmail | 0-1 | E-mail do fornecedor | Externo |
| **223** | trib | Grupo | 171 - valores | | 0-1 | | Interno |
| **224** | tribMun | Grupo | 223 - trib | | 0-1 | | Interno |
| **225** | tribISSQN | Elemento | 224 - tribMun | natOp | 1-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| **226** | cPaisResult | Elemento | 224 - tribMun | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| **227** | tpImunidade | Elemento | 224 - tribMun | tpImunidade | 0-1 | Identificação da Imunidade do ISSQN | Externo |
| **228** | exigSusp | Grupo | 224 - tribMun | | 0-1 | | Interno |
| **229** | tpSusp | Elemento | 228 - exigSusp | tpSusp | 0-1 | Opção para Exigibilidade Suspensa | Externo |
| **230** | nProcesso | Elemento | 228 - exigSusp | NumProcesso | 0-1 | Número do Processo Administrativo | Externo |
| **231** | BM | Grupo | 224 - tribMun | | 0-1 | | Interno |
| **233** | nBM | Elemento | 231 - BM | benefNumero | 0-1 | Lei do Benefício Municipal | Externo |
| **234** | vRedBCBM | Elemento | 231 - BM | | 0-1 | | Interno |
| **235** | pRedBCBM | Elemento | 231 - BM | ValRedBenef | 0-1 | Valor redução da base de cálculo do ISSQN (BM) | Externo |
| **240** | tpRetISSQN | Elemento | 224 - tribMun | ISSRetido | 0-1 | É ISS Retido? | Externo |
| **241** | pAliq | Elemento | 224 - tribMun | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| **242** | tribFed | Grupo | 223 - trib | | 0-1 | | Interno |
| **243** | piscofins | Grupo | 242 - tribFed | | 0-1 | | Interno |
| **244** | CST | Elemento | 243 - piscofins | CST | 0-1 | CST do PIS/COFINS | Externo |
| **245** | vBCPisCofins | Elemento | 243 - piscofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| **246** | pAliqPis | Elemento | 243 - piscofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| **247** | pAliqCofins | Elemento | 243 - piscofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| **248** | vPis | Elemento | 243 - piscofins | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| **249** | vCofins | Elemento | 243 - piscofins | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| **250** | tpRetPisCofins | Elemento | 243 - piscofins | PISRetido | 0-1 | PIS é retido? | Externo |
| **251** | vRetCP | Elemento | 242 - tribFed | ValINSS | 0-1 | Valor do INSS do serviço | Externo |
| **252** | vRetIRRF | Elemento | 242 - tribFed | ValIR | 0-1 | Valor do IRRF do serviço | Externo |
| **253** | vRetCSLL | Elemento | 242 - tribFed | ValCSLL | 0-1 | Valor do CSLL | Externo |
| **254** | totTrib | Grupo | 223 - trib | | 1-1 | | Interno |
| **255** | vTotTrib | Grupo | 254 - totTrib | | 0-1 | | Interno |
| **256** | vTotTribFed | Elemento | 255 - vTotTrib | ValRetFederais | 0-1 | Valor total das retenções federais | Externo |
| **257** | vTotTribEst | Elemento | 255 - vTotTrib | ValRetEstaduais | 0-1 | Valor aproximado tributos estaduais | Externo |
| **258** | vTotTribMun | Elemento | 255 - vTotTrib | ValRetMunicipais | 0-1 | Valor aproximado tributos municipais | Externo |
| **259** | pTotTrib | Grupo | 254 - totTrib | | 0-1 | | Interno |
| **260** | pTotTribFed | Elemento | 259 - pTotTrib | ValAliqRetFederais | 0-1 | Percentual aproximado tributos federais | Externo |
| **261** | pTotTribEst | Elemento | 259 - pTotTrib | ValAliqRetEstaduais | 0-1 | Percentual aproximado tributos estaduais | Externo |
| **262** | pTotTribMun | Elemento | 259 - pTotTrib | ValAliqRetMunicipais | 0-1 | Percentual aproximado tributos municipais | Externo |
| **263** | indTotTrib | Elemento | 254 - totTrib | indTotTrib | 0-1 | Indicador de informação total de tributos | Interno |
| **264** | pTotTribSN | Elemento | 254 - totTrib | ValAproxTribAliqSN | 0-1 | Percentual aprox. tributos alíquota SN (%) | Externo |
| **265** | IBSCBS | Grupo | 13 - infDPS | | 0-1 | | Interno |
| **266** | finNFSe | Elemento | 265 - IBSCBS | finNFSe | 0-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| **267** | indFinal | Elemento | 265 - IBSCBS | indFinal | 1-1 | Indica operação uso/consumo pessoal (0/1) | Externo |
| **268** | cIndOp | Elemento | 265 - IBSCBS | cIndOp | 0-1 | Código indicador da operação | Externo |
| **269** | tpOper | Elemento | 265 - IBSCBS | tpOper | 0-1 | Tipo Operação Entes Gov / Bens Imóveis | Externo |
| **270** | gRefNFSe | Grupo | 265 - IBSCBS | | 0-1 | | Interno |
| **271** | refNFSe | Elemento | 270 - gRefNFSe | | 1-N | | Interno |
| **272** | tpEnteGov | Elemento | 265 - IBSCBS | | 0-1 | | Interno |
| **273** | indDest | Elemento | 265 - IBSCBS | | 1-1 | | Interno |
| **274** | dest | Grupo | 265 - IBSCBS | | 0-1 | | Interno |
| **275** | CNPJ | Elemento | 274 - dest | | 0-1 | | Interno |
| **276** | CPF | Elemento | 274 - dest | | 0-1 | | Interno |
| **277** | NIF | Elemento | 274 - dest | | 0-1 | | Interno |
| **278** | cNaoNIF | Elemento | 274 - dest | | 0-1 | | Interno |
| **279** | xNome | Elemento | 274 - dest | | 0-1 | | Interno |
| **280** | end | Grupo | 274 - dest | | 0-1 | | Interno |
| **281** | endNac | Grupo | 280 - end | | 0-1 | | Interno |
| **282** | cMun | Elemento | 281 - endNac | | 0-1 | | Interno |
| **283** | CEP | Elemento | 281 - endNac | | 0-1 | | Interno |
| **284** | endExt | Grupo | 280 - end | | 0-1 | | Interno |
| **285** | cPais | Elemento | 284 - endExt | | 0-1 | | Interno |
| **286** | cEndPost | Elemento | 284 - endExt | | 0-1 | | Interno |
| **287** | xCidade | Elemento | 284 - endExt | | 0-1 | | Interno |
| **288** | xEstProvReg | Elemento | 284 - endExt | | 0-1 | | Interno |
| **289** | xLgr | Elemento | 280 - end | | 0-1 | | Interno |
| **290** | nro | Elemento | 280 - end | | 0-1 | | Interno |
| **291** | xCpl | Elemento | 280 - end | | 0-1 | | Interno |
| **292** | xBairro | Elemento | 280 - end | | 0-1 | | Interno |
| **293** | fone | Elemento | 274 - dest | | 0-1 | | Interno |
| **294** | email | Elemento | 274 - dest | | 0-1 | | Interno |
| **295** | imovel | Grupo | 265 - IBSCBS | | 0-1 | | Interno |
| **296** | inscImobFisc | Elemento | 295 - imovel | ImovInscImobFisc | 0-1 | Inscrição Imobiliária Fiscal do Imóvel | Externo |
| **297** | cCIB | Elemento | 295 - imovel | ImovCIB | 0-1 | Cadastro Imobiliário Brasileiro (CIB) | Externo |
| **298** | end | Grupo | 295 - imovel | | 0-1 | | Interno |
| **299** | CEP | Elemento | 298 - end | ImovCEP | 0-1 | CEP do imóvel | Externo |
| **300** | endExt | Grupo | 298 - end | | 0-1 | | Interno |
| **301** | cEndPost | Elemento | 300 - endExt | ImovCEP | 0-1 | CEP do imóvel (exterior) | Externo |
| **302** | xCidade | Elemento | 300 - endExt | ImovxMun | 0-1 | Cidade do imóvel no exterior | Externo |
| **303** | xEstProvReg | Elemento | 300 - endExt | ImovEstProvReg | 0-1 | Estado/Província do imóvel no exterior | Externo |
| **304** | xLgr | Elemento | 298 - end | ImovEndereco | 0-1 | Endereço / Logradouro do imóvel | Externo |
| **305** | nro | Elemento | 298 - end | ImovNumero | 0-1 | Número do imóvel | Externo |
| **306** | xCpl | Elemento | 298 - end | ImovComplemento | 0-1 | Complemento do endereço do imóvel | Externo |
| **307** | xBairro | Elemento | 298 - end | ImovBairro | 0-1 | Bairro do imóvel | Externo |
| **308** | valores | Grupo | 265 - IBSCBS | | 0-1 | | Interno |
| **309** | gReeRepRes | Grupo | 308 - valores | | 0-1 | | Interno |
| **310** | documentos | Grupo | 309 - gReeRepRes | | 1-N | | Interno |
| **311** | dFeNacional | Grupo | 310 - documentos | | 0-1 | | Interno |
| **312** | tipoChaveDFe | Elemento | 311 - dFeNacional | | 0-1 | | Interno |
| **313** | xTipoChaveDFe | Elemento | 311 - dFeNacional | | 0-1 | | Interno |
| **314** | chaveDFe | Elemento | 311 - dFeNacional | | 0-1 | | Interno |
| **315** | docFiscalOutro | Grupo | 310 - documentos | | 0-1 | | Interno |
| **316** | cMunDocFiscal | Elemento | 315 - docFiscalOutro | | 1-1 | | Interno |
| **317** | nDocFiscal | Elemento | 315 - docFiscalOutro | | 0-1 | | Interno |
| **318** | xDocFiscal | Elemento | 315 - docFiscalOutro | | 0-1 | | Interno |
| **319** | docOutro | Grupo | 310 - documentos | | 0-1 | | Interno |
| **320** | nDoc | Elemento | 319 - docOutro | | 0-1 | | Interno |
| **321** | xDoc | Elemento | 319 - docOutro | | 0-1 | | Interno |
| **322** | fornec | Grupo | 310 - documentos | | 0-1 | | Interno |
| **324** | CNPJ | Elemento | 322 - fornec | | 0-1 | | Interno |
| **325** | CPF | Elemento | 322 - fornec | | 0-1 | | Interno |
| **326** | NIF | Elemento | 322 - fornec | | 0-1 | | Interno |
| **327** | cNaoNIF | Elemento | 322 - fornec | | 0-1 | | Interno |
| **328** | xNome | Elemento | 322 - fornec | | 0-1 | | Interno |
| **329** | dtEmiDoc | Elemento | 310 - documentos | | 0-1 | | Interno |
| **330** | dtCompDoc | Elemento | 310 - documentos | | 0-1 | | Interno |
| **331** | tpReeRepRes | Elemento | 310 - documentos | | 0-1 | | Interno |
| **332** | xTpReeRepRes | Elemento | 310 - documentos | | 0-1 | | Interno |
| **333** | vlrReeRepRes | Elemento | 310 - documentos | | 0-1 | | Interno |
| **334** | trib | Grupo | 308 - valores | | 0-1 | | Interno |
| **335** | gIBSCBS | Grupo | 334 - trib | | 0-1 | | Interno |
| **336** | CST | Elemento | 335 - gIBSCBS | CSTIBSCBS | 0-1 | CST do IBS e da CBS | Externo |
| **337** | cClassTrib | Elemento | 335 - gIBSCBS | cClassTrib | 0-1 | Código de Classificação Tributária IBS/CBS | Externo |
| **338** | cCredPres | Elemento | 335 - gIBSCBS | | 0-1 | | Interno |
| **339** | gTribRegular | Grupo | 335 - gIBSCBS | | 0-1 | | Interno |
| **340** | CSTReg | Elemento | 339 - gTribRegular | | 0-1 | | Interno |
| **341** | cClassTribReg | Elemento | 339 - gTribRegular | | 0-1 | | Interno |
| **342** | gDif | Grupo | 335 - gIBSCBS | | 0-1 | | Interno |
| **343** | pDifUF | Elemento | 342 - gDif | | 0-1 | | Interno |
| **344** | pDifMun | Elemento | 342 - gDif | | 0-1 | | Interno |
| **345** | pDifCBS | Elemento | 342 - gDif | | 0-1 | | Interno |