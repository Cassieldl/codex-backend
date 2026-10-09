# Mapeamento De/Para: Layout ISS.NET x Tag InvoiCy

Este documento contém o mapeamento completo e consolidado de todos os campos (sequenciais 1 a 352) entre o layout do provedor **ISS.NET** e o sistema InvoiCy.

| Sequencial | TAG da prefeitura | Elemento XML | Dentro do grupo | Nome do campo (tag InvoiCy) | Ocorrência | Descrição | Interno / Externo |
| ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
| **1** | EnviarLoteDpsSincronoEnvio | Grupo | 0 - |  | 1-1 |  | Interno |
| **2** | LoteDps | Grupo | 1 - EnviarLoteDpsSincronoEnvio |  | 1-1 |  | Interno |
| **3** | versao | Atributo | 2 - LoteDps |  | 0-1 |  | Interno |
| **4** | NumeroLote | Elemento | 2 - LoteDps | NumeroLote | 1-1 | Número do Lote | Interno |
| **5** | Prestador | Grupo | 2 - LoteDps |  | 1-1 |  | Interno |
| **6** | CNPJ | Elemento | 5 - Prestador | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| **7** | CPF | Elemento | 5 - Prestador | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| **8** | IM | Elemento | 5 - Prestador | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| **9** | QuantidadeDps | Elemento | 2 - LoteDps | QuantidadeRps | 0-1 | Quantidade de RPS no Lote | Interno |
| **10** | ListaDps | Grupo | 2 - LoteDps |  | 0-1 |  | Interno |
| **11** | DPS | Grupo | 10 - ListaDps |  | 0-1 |  | Interno |
| **12** | versao | Atributo | 11 - DPS |  | 0-1 |  | Interno |
| **13** | infDPS | Grupo | 11 - DPS |  | 0-1 |  | Interno |
| **14** | Id | Atributo | 13 - infDPS | RPSNumero e RPSSerie | 0-1 | Id do Grupo RPS | Interno |
| **15** | tpAmb | Elemento | 13 - infDPS | tpAmb | 1-1 | Ambiente Emissão | Interno |
| **16** | dhEmi | Elemento | 13 - infDPS | dEmis | 0-1 | Data de Emissão | Externo |
| **17** | verAplic | Elemento | 13 - infDPS |  | 0-1 |  | Interno |
| **18** | serie | Elemento | 13 - infDPS | RPSSerie | 0-1 | Série do RPS | Externo |
| **19** | nDPS | Elemento | 13 - infDPS | RPSNumero | 0-1 | Número do RPS | Externo |
| **20** | dCompet | Elemento | 13 - infDPS | dCompetencia | 0-1 | data de Competência | Externo |
| **21** | tpEmit | Elemento | 13 - infDPS |  | 1-1 |  | Interno |
| **22** | cLocEmi | Elemento | 13 - infDPS | cMun | 0-1 | Código do Município do prestador | Externo |
| **23** | subst | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **24** | chSubstda | Elemento | 23 - subst | SubsChave | 0-1 | Chave de Acesso NFSe Substituída | Externo |
| **25** | cMotivo | Elemento | 23 - subst | SubsCodMotivo | 0-1 | Código do Motivo da substituição | Externo |
| **26** | xMotivo | Elemento | 23 - subst | SubsDescMotivo | 0-1 | Descrição do Motivo da substituição | Externo |
| **27** | prest | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **28** | CNPJ | Elemento | 27 - prest | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| **29** | CPF | Elemento | 27 - prest | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| **30** | NIF | Elemento | 27 - prest |  | 0-1 |  | Interno |
| **31** | cNaoNIF | Elemento | 27 - prest |  | 0-1 |  | Interno |
| **32** | CAEPF | Elemento | 27 - prest | CAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) do prestador do serviço | Externo |
| **33** | IM | Elemento | 27 - prest | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| **34** | xNome | Elemento | 27 - prest |  | 0-1 |  | Interno |
| **35** | end | Grupo | 27 - prest |  | 0-1 |  | Interno |
| **36** | endNac | Grupo | 35 - end |  | 0-1 |  | Interno |
| **37** | cMun | Elemento | 36 - endNac |  | 0-1 |  | Interno |
| **38** | CEP | Elemento | 36 - endNac |  | 0-1 |  | Interno |
| **39** | endExt | Grupo | 35 - end |  | 0-1 |  | Interno |
| **40** | cPais | Elemento | 39 - endExt |  | 0-1 |  | Interno |
| **41** | cEndPost | Elemento | 39 - endExt |  | 0-1 |  | Interno |
| **42** | xCidade | Elemento | 39 - endExt |  | 0-1 |  | Interno |
| **43** | xEstProvReg | Elemento | 39 - endExt |  | 0-1 |  | Interno |
| **44** | xLgr | Elemento | 35 - end |  | 0-1 |  | Interno |
| **45** | nro | Elemento | 35 - end |  | 0-1 |  | Interno |
| **46** | xCpl | Elemento | 35 - end |  | 0-1 |  | Interno |
| **47** | xBairro | Elemento | 35 - end |  | 0-1 |  | Interno |
| **48** | fone | Elemento | 27 - prest |  | 0-1 |  | Interno |
| **49** | email | Elemento | 27 - prest |  | 0-1 |  | Interno |
| **50** | regTrib | Grupo | 27 - prest |  | 0-1 |  | Interno |
| **51** | opSimpNac | Elemento | 50 - regTrib | OptSN | 0-1 | Optante Simples Nacional | Externo |
| **52** | regApTribSN | Elemento | 50 - regTrib | regApTribSN | 0-1 | Regime de tributação para empresas Opt. SN ME/EPP que ultrapassam sublimite | Externo |
| **53** | regEspTrib | Elemento | 50 - regTrib | RegEspTrib | 1-1 | Regime Especial de Tributação | Externo |
| **54** | toma | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **55** | CNPJ | Elemento | 54 - toma | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo |
| **56** | CPF | Elemento | 54 - toma | TomaCPF | 0-1 | CPF do Tomador | Externo |
| **57** | NIF | Elemento | 54 - toma | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| **58** | cNaoNIF | Elemento | 54 - toma | TomaExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **59** | CAEPF | Elemento | 54 - toma | TomaCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| **60** | IM | Elemento | 54 - toma | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo |
| **61** | xNome | Elemento | 54 - toma | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo |
| **62** | end | Grupo | 54 - toma |  | 0-1 |  | Interno |
| **63** | endNac | Grupo | 62 - end |  | 0-1 |  | Interno |
| **64** | cMun | Elemento | 63 - endNac | TomacMun | 0-1 | Codigo do Municipio IBGE do Tomador | Externo |
| **65** | CEP | Elemento | 63 - endNac | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| **66** | endExt | Grupo | 62 - end |  | 0-1 |  | Interno |
| **67** | cPais | Elemento | 66 - endExt | TomaPais | 0-1 | País do Tomador no Exterior | Externo |
| **68** | cEndPost | Elemento | 66 - endExt | TomaCEP | 0-1 | CEP do Tomador (exterior) | Externo |
| **69** | xCidade | Elemento | 66 - endExt | TomaXMun | 0-1 | Nome do Município do tomador (Exterior) | Externo |
| **70** | xEstProvReg | Elemento | 66 - endExt | TomaUF | 0-1 | UF (sigla) do estado do Tomador (exterior) | Externo |
| **71** | xLgr | Elemento | 62 - end | TomaEndereco | 0-1 | Endereço do Tomador | Externo |
| **72** | nro | Elemento | 62 - end | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| **73** | xCpl | Elemento | 62 - end | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo |
| **74** | xBairro | Elemento | 62 - end | TomaBairro | 0-1 | Bairro do Tomador | Externo |
| **75** | fone | Elemento | 54 - toma | TomaTelefone | 0-1 | Telefone do Tomador | Externo |
| **76** | email | Elemento | 54 - toma | TomaEmail | 0-1 | Email do Tomador | Externo |
| **77** | interm | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **78** | CNPJ | Elemento | 77 - interm | IntermCNPJ | 0-1 | CNPJ do Intermediário | Externo |
| **79** | CPF | Elemento | 77 - interm | IntermCPF | 0-1 | CPF do Intermediário | Externo |
| **80** | NIF | Elemento | 77 - interm | IntermNIF | 0-1 | Doc intermediário extrangeiro | Externo |
| **81** | cNaoNIF | Elemento | 77 - interm | IntermExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **82** | CAEPF | Elemento | 77 - interm | IntermCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| **83** | IM | Elemento | 77 - interm | IntermIM | 0-1 | Inscrição Municipal do Intermediário | Externo |
| **84** | xNome | Elemento | 77 - interm | IntermRazaoSocial | 0-1 | Razão Social do Intermediário | Externo |
| **85** | end | Grupo | 77 - interm |  | 0-1 |  | Interno |
| **86** | endNac | Grupo | 85 - end |  | 0-1 |  | Interno |
| **87** | cMun | Elemento | 86 - endNac | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo |
| **88** | CEP | Elemento | 86 - endNac | IntermCep | 0-1 | CEP do Intermediário (nacional) | Externo |
| **89** | endExt | Grupo | 85 - end |  | 0-1 |  | Interno |
| **90** | cPais | Elemento | 89 - endExt | IntermPais | 0-1 | Código do País do Intermediário | Externo |
| **91** | cEndPost | Elemento | 89 - endExt | IntermCep | 0-1 | CEP do intermediário (exterior) | Externo |
| **92** | xCidade | Elemento | 89 - endExt | IntermXmun | 0-1 | Nome do município do intermediário (exterior) | Externo |
| **93** | xEstProvReg | Elemento | 89 - endExt | IntermUFEx | 0-1 | UF/província/região do intermediário no exterior | Interno |
| **94** | xLgr | Elemento | 85 - end | IntermEndereco | 0-1 | Logradouro do Endereço do Intermediário | Externo |
| **95** | nro | Elemento | 85 - end | IntermNumero | 0-1 | Número do Endereço do Intermediário | Externo |
| **96** | xCpl | Elemento | 85 - end | IntermComplemento | 0-1 | Complemento do Endereço do Intermediário | Externo |
| **97** | xBairro | Elemento | 85 - end | IntermBairro | 0-1 | Bairro do Intermediário | Externo |
| **98** | fone | Elemento | 85 - end | IntermFone | 0-1 | Telefone do Intermediário | Externo |
| **99** | email | Elemento | 85 - end | IntermEmail | 0-1 | E-mail do Intermediário | Externo |
| **100** | serv | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **101** | locPrest | Grupo | 100 - serv |  | 1-1 |  | Interno |
| **102** | cLocPrestacao | Elemento | 101 - locPrest | SerEndcMun | 0-1 | Local da Prestação do Serviço Cod IBGE Municipio | Externo |
| **103** | cPaisPrestacao | Elemento | 101 - locPrest | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| **104** | opConsumServ | Elemento | 101 - locPrest |  | 0-1 |  | Interno |
| **105** | cServ | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **106** | cTribNac | Elemento | 105 - cServ | codTributNacional | 0-1 | Código de tributação Nacional | Externo |
| **107** | cTribMun | Elemento | 105 - cServ | TributMunicipio | 0-1 | Código de Tributação do Município | Externo |
| **108** | xDescServ | Elemento | 105 - cServ | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| **109** | cNBS | Elemento | 105 - cServ | CodigoNBS | 0-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo |
| **110** | cIntContrib | Elemento | 105 - cServ |  | 0-1 |  | Interno |
| **111** | comExt | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **112** | mdPrestacao | Elemento | 111 - comExt | mdPrestacao | 0-1 | Modo de Prestação | Externo |
| **113** | vincPrest | Elemento | 111 - comExt | vincPrest | 0-1 | Vínculo entre as partes no negócio | Externo |
| **114** | tpMoeda | Elemento | 111 - comExt | tpMoeda | 0-1 | Identifica a moeda da transação comercial | Externo |
| **115** | vServMoeda | Elemento | 111 - comExt | ValServEst | 0-1 | Valor do Serviço em Moeda estrangeira | Externo |
| **116** | mecAFComexP | Elemento | 111 - comExt | mecAFComexP | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo prestador do serviço | Externo |
| **117** | mecAFComexT | Elemento | 111 - comExt | mecAFComexT | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo tomador do serviço | Externo |
| **118** | movTempBens | Elemento | 111 - comExt | movTempBens | 0-1 | Operação está vinculada à Movimentação Temporária de Bens | Externo |
| **119** | nDI | Elemento | 111 - comExt | nDI | 0-1 | Número da Declaração de Importação (DI/DSI/DA/DRI-E) averbado | Externo |
| **120** | nRE | Elemento | 111 - comExt | nRE | 0-1 | Número do Registro de Exportação (RE) averbado | Externo |
| **121** | mdic | Elemento | 111 - comExt | mdic | 0-1 | Compartilhar as informações da NFS-e gerada a partir desta DPS com a Secretaria de Comércio Ext | Externo |
| **122** | lsadppu | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **123** | categ | Elemento | 122 - lsadppu | categServ | 0-1 | Categoria do serviço | Externo |
| **124** | objeto | Elemento | 122 - lsadppu | objetoLocacao | 0-1 | Tipo de objetos da locação, sublocação, arrendamento, direito de passagem ou permissão de uso | Externo |
| **125** | extensao | Elemento | 122 - lsadppu | extensaoFerrovia | 0-1 | Extensão total da ferrovia, rodovia, cabos, dutos ou condutos | Externo |
| **126** | nPostes | Elemento | 122 - lsadppu | nPostes | 0-1 | Número total de postes | Externo |
| **127** | obra | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **128** | inscImobFisc | Elemento | 127 - obra | ObraInscImobFis | 0-1 | Inscrição imobiliária fiscal da obra | Externo |
| **129** | cObra | Elemento | 127 - obra | CodObra | 0-1 | Código da Obra | Externo |
| **130** | cCIB | Elemento | 127 - obra | ObraCIB | 0-1 | CIB da Obra | Externo |
| **131** | end | Grupo | 127 - obra |  | 0-1 |  | Interno |
| **132** | endNac | Grupo | 131 - end |  | 0-1 |  | Interno |
| **133** | cMun | Elemento | 132 - endNac | ObraMun | 0-1 | Código IBGE da Cidade da Obra | Externo |
| **134** | CEP | Elemento | 132 - endNac | ObraCEP | 0-1 | CEP da Obra | Externo |
| **135** | endExt | Grupo | 131 - end |  | 0-1 |  | Interno |
| **136** | cEndPost | Elemento | 135 - endExt | ObraCEP | 0-1 | CEP da Obra (exterior) | Externo |
| **137** | xCidade | Elemento | 135 - endExt | ObraXMunEx | 0-1 | Nome do município da Obra no exterior | Interno |
| **138** | xEstProvReg | Elemento | 135 - endExt | ObraUF | 0-1 | UF da obra do exterior | Externo |
| **139** | xLgr | Elemento | 131 - end | ObraLog | 0-1 | Logradouro da Obra | Externo |
| **140** | nro | Elemento | 131 - end | ObraNumero | 0-1 | Número do End. da Obra | Externo |
| **141** | xCpl | Elemento | 131 - end | ObraCompl | 0-1 | Complemento do End. da Obra | Externo |
| **142** | xBairro | Elemento | 131 - end | ObraBairro | 0-1 | Bairro da Obra | Externo |
| **143** | atvEvento | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **144** | xNome | Elemento | 143 - atvEvento | AtivDesc | 0-1 | Descrição do evento Artístico, Cultural, Esportivo, etc | Externo |
| **145** | dtIni | Elemento | 143 - atvEvento | AtivDataInicial | 0-1 | Data de início da atividade de evento | Externo |
| **146** | dtFim | Elemento | 143 - atvEvento | AtivDataFinal | 0-1 | Data de fim da atividade de evento | Externo |
| **147** | idAtvEvt | Elemento | 143 - atvEvento |  | 0-1 |  | Interno |
| **148** | end | Grupo | 143 - atvEvento |  | 0-1 |  | Interno |
| **149** | CEP | Elemento | 148 - end |  | 0-1 |  | Interno |
| **150** | endNac | Grupo | 148 - end |  | 0-1 |  | Interno |
| **151** | cMun | Elemento | 150 - endNac | AtivEndcMun | 0-1 | Código IBGE do município do endereço do evento | Externo |
| **152** | CEP | Elemento | 150 - endNac | AtivEndCEP | 0-1 | CEP do endereço do evento | Externo |
| **153** | endExt | Grupo | 148 - end |  | 0-1 |  | Interno |
| **154** | cEndPost | Elemento | 153 - endExt | AtivEndCEPEx | 0-1 | CEP do endereço do evento no exterior | Interno |
| **155** | xCidade | Elemento | 153 - endExt | AtivEndxMunEx | 0-1 | Nome do município do endereço do evento do exterior | Interno |
| **156** | xEstProvReg | Elemento | 153 - endExt | AtivEndUfEx | 0-1 | UF/província/Região do evento no exterior | Interno |
| **157** | xLgr | Elemento | 148 - end | AtivEndLogradouro | 0-1 | Logradouro do endereço do evento | Externo |
| **158** | nro | Elemento | 148 - end | AtivEndNumero | 0-1 | Número do endereço do evento | Externo |
| **159** | xCpl | Elemento | 148 - end | AtivEndCompl | 0-1 | Complemento do endereço do evento | Externo |
| **160** | xBairro | Elemento | 148 - end | AtivEndBairro | 0-1 | Bairro do endereço do evento | Externo |
| **161** | explRod | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **162** | categVeic | Elemento | 161 - explRod | PedCategVeiculo | 0-1 | Categorias de veículos para cobrança | Externo |
| **163** | nEixos | Elemento | 161 - explRod | PednEixos | 0-1 | Número de eixos para fins de cobrança | Externo |
| **164** | rodagem | Elemento | 161 - explRod | PedRodagem | 0-1 | Tipo de rodagem | Externo |
| **165** | sentido | Elemento | 161 - explRod | PedSentido | 0-1 | Orientação de passagem do veículo | Externo |
| **166** | placa | Elemento | 161 - explRod | PedPlaca | 0-1 | Placa do veículo | Externo |
| **167** | codAcessoPed | Elemento | 161 - explRod | PedCodAcesso | 0-1 | Código de acesso gerado automaticamente pelo sistema emissor da concessionária | Externo |
| **168** | codContrato | Elemento | 161 - explRod | PedCodContrato | 0-1 | Código de contrato gerado automaticamente pelo sistema nacional no cadastro da concessionária | Externo |
| **169** | infoCompl | Grupo | 100 - serv |  | 0-1 |  | Interno |
| **170** | idDocTec | Elemento | 169 - infoCompl | idDocTec | 0-1 | Identificador de Documento de Responsabilidade Técnica: ART, RRT, DRT, Outros. | Externo |
| **171** | docRef | Elemento | 169 - infoCompl | docRef | 0-1 | Chave da nota fiscal referenciada. | Externo |
| **172** | xInfComp | Elemento | 169 - infoCompl | NFSOutrasinformacoes | 0-1 | Outras Informações / observações | Externo |
| **173** | valores | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **174** | vServPrest | Grupo | 173 - valores |  | 0-1 |  | Interno |
| **175** | vReceb | Elemento | 174 - vServPrest |  | 0-1 |  | Interno |
| **176** | vServ | Elemento | 174 - vServPrest | ValServicos | 0-1 | Valor do Serviço | Externo |
| **177** | vDescCondIncond | Grupo | 173 - valores |  | 0-1 |  | Interno |
| **178** | vDescIncond | Elemento | 177 - vDescCondIncond | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo |
| **179** | vDescCond | Elemento | 177 - vDescCondIncond | ValDescCond | 0-1 | Valor desconto Condicionado | Externo |
| **180** | vDedRed | Grupo | 173 - valores |  | 0-1 |  | Interno |
| **181** | pDR | Elemento | 180 - vDedRed | ValPercDeducoes | 0-1 | Valor percentual padrão para dedução/redução do valor do serviço | Externo |
| **182** | vDR | Elemento | 180 - vDedRed | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo |
| **183** | documentos | Grupo | 180 - vDedRed | Ded | 0-1 | Grupo Dedução (controle) | Interno |
| **184** | docDedRed | Grupo | 183 - documentos |  | 0-1 |  | Interno |
| **185** | chNFSe | Elemento | 184 - docDedRed | DedChAcesNacional | 0-1 | Chave de Acesso da NFS-e (Padrão Nacional) | Externo |
| **186** | chNFe | Elemento | 184 - docDedRed | DedChaveAcesso | 0-1 | Chave de acesso da NF-e de referência | Externo |
| **187** | NFSeMun | Grupo | 184 - docDedRed |  | 0-1 |  | Interno |
| **188** | cMunNFSeMun | Elemento | 187 - NFSeMun | DedcMunNFSe | 0-1 | Código do município da NFS-e de referência | Externo |
| **189** | nNFSeMun | Elemento | 187 - NFSeMun |  | 0-1 |  | Interno |
| **190** | cVerifNFSeMun | Elemento | 187 - NFSeMun | DedcVerificaNFSe | 0-1 | Código de verificação da NFS-e de referência | Externo |
| **191** | NFNFS | Grupo | 184 - docDedRed |  | 0-1 |  | Interno |
| **192** | nNFS | Elemento | 191 - NFNFS | DednNFRef | 0-1 | Número da nota de referência da Dedução | Externo |
| **193** | modNFS | Elemento | 191 - NFNFS |  | 0-1 |  | Interno |
| **194** | serieNFS | Elemento | 191 - NFNFS |  | 0-1 |  | Interno |
| **195** | nDocFisc | Elemento | 184 - docDedRed | DedOutroDocFisc | 0-1 | Número de outro documento fiscal | Externo |
| **196** | nDoc | Elemento | 184 - docDedRed | DedOutroDoc | 0-1 | Código de outro documento de referência | Externo |
| **197** | tpDedRed | Elemento | 184 - docDedRed | DedRedTipo | 0-1 | Tipo de dedução/Redução modelo nacional | Externo |
| **198** | xDescOutDed | Elemento | 184 - docDedRed | DedRetxDesc | 0-1 | Descrição da Dedução/Redução | Externo |
| **199** | dtEmiDoc | Elemento | 184 - docDedRed | DedDEmis | 0-1 | Data de emissão da NF-e de referência | Externo |
| **200** | vDedutivelRedutivel | Elemento | 184 - docDedRed | DedvlTotRef | 0-1 | Valor total da nota de referencia de dedução | Externo |
| **201** | vDeducaoReducao | Elemento | 184 - docDedRed | DedValor | 0-1 | Valor da Dedução da lista de deduções | Externo |
| **202** | fornec | Grupo | 184 - docDedRed |  | 0-1 |  | Interno |
| **203** | CNPJ | Elemento | 202 - fornec | DedCNPJRef | 0-1 | CNPJ do fornecedor ou prestador | Externo |
| **204** | CPF | Elemento | 202 - fornec | DedCPFRef | 0-1 | CPF do fornecedor ou prestador | Externo |
| **205** | NIF | Elemento | 202 - fornec | DedNifFornecedor | 0-1 | NIF do Fornecedor do exterior | Externo |
| **206** | cNaoNIF | Elemento | 202 - fornec | DedForncExSemNIF | 0-1 | Motivo para não informação do NIF do fornecedor | Externo |
| **207** | CAEPF | Elemento | 202 - fornec | DedFornCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| **208** | IM | Elemento | 202 - fornec | DedFornIM | 0-1 | Inscrição Municipal do fornecedor | Externo |
| **209** | xNome | Elemento | 202 - fornec | DedFornNome | 0-1 | Razão Social do fornecedor | Externo |
| **210** | end | Grupo | 202 - fornec |  | 0-1 |  | Interno |
| **211** | endNac | Grupo | 210 - end |  | 0-1 |  | Interno |
| **212** | cMun | Elemento | 211 - endNac | DedForncMun | 0-1 | Código IBGE do fornecedor | Externo |
| **213** | CEP | Elemento | 211 - endNac | DedFornCEP | 0-1 | CEP do fornecedor | Externo |
| **214** | endExt | Grupo | 210 - end |  | 0-1 |  | Interno |
| **215** | cPais | Elemento | 214 - endExt | DedPaisFornecedor | 0-1 | Código do país do Fornecedor do exterior | Externo |
| **216** | cEndPost | Elemento | 214 - endExt | DedFornCEPEx | 0-1 | CEP do fornecedor no exterior | Interno |
| **217** | xCidade | Elemento | 214 - endExt | DedFornxMunEx | 0-1 | Nome do Município do fornecedor no exterior | Interno |
| **218** | xEstProvReg | Elemento | 214 - endExt | DedFornUFEx | 0-1 | UF/Província/região do fornecedor no exterior | Interno |
| **219** | xLgr | Elemento | 210 - end | DedFornLogr | 0-1 | Logradouro do endereço do Fornecedor | Externo |
| **220** | nro | Elemento | 210 - end | DedFornNumero | 0-1 | Número do endereço do Fornecedor | Externo |
| **221** | xCpl | Elemento | 210 - end | DedFornCompl | 0-1 | Complemento do endereço do Fornecedor | Externo |
| **222** | xBairro | Elemento | 210 - end | DedFornBairro | 0-1 | Bairro do endereço do Fornecedor | Externo |
| **223** | fone | Elemento | 202 - fornec | DedFornFone | 0-1 | Telefone do fornecedor com DDD | Externo |
| **224** | email | Elemento | 202 - fornec | DedFornEmail | 0-1 | E-mail do fornecedor | Externo |
| **225** | trib | Grupo | 173 - valores |  | 0-1 |  | Interno |
| **226** | tribMun | Grupo | 225 - trib |  | 0-1 |  | Interno |
| **227** | tribISSQN | Elemento | 226 - tribMun | natOp | 1-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| **228** | cPaisResult | Elemento | 226 - tribMun | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| **229** | BM | Grupo | 226 - tribMun |  | 0-1 |  | Interno |
| **230** | tpBM | Elemento | 229 - BM | benefIdNac | 0-1 | Tipo do Benefício Municipal | Externo |
| **231** | nBM | Elemento | 229 - BM | benefNumero | 0-1 | Identificação da Lei parametrizada pelo município que define o benefício no sistema nacional | Externo |
| **232** | vRedBCBM | Elemento | 229 - BM | ValRedBenef | 0-1 | Valor monetário para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| **233** | pRedBCBM | Elemento | 229 - BM | PercRedBenef | 0-1 | Valor percentual para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| **234** | exigSusp | Grupo | 226 - tribMun |  | 0-1 |  | Interno |
| **235** | tpSusp | Elemento | 234 - exigSusp | tpSusp | 0-1 | Opção para Exigibilidade Suspensa | Externo |
| **236** | nProcesso | Elemento | 234 - exigSusp | NumProcesso | 0-1 | Número do Processo Administrativo | Externo |
| **237** | tpImunidade | Elemento | 226 - tribMun | tpImunidade | 0-1 | Identificação da Imunidade do ISSQN – somente para o caso de Imunidade | Externo |
| **238** | tpRetISSQN | Elemento | 226 - tribMun | ISSRetido | 0-1 | É ISS Retido? | Externo |
| **239** | pAliq | Elemento | 226 - tribMun | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| **240** | tribFed | Grupo | 225 - trib |  | 0-1 |  | Interno |
| **241** | piscofins | Grupo | 240 - tribFed |  | 0-1 |  | Interno |
| **242** | CST | Elemento | 241 - piscofins | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo |
| **243** | vBCPisCofins | Elemento | 241 - piscofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| **244** | pAliqPis | Elemento | 241 - piscofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| **245** | pAliqCofins | Elemento | 241 - piscofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| **246** | vPis | Elemento | 241 - piscofins | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| **247** | vCofins | Elemento | 241 - piscofins | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| **248** | tpRetPisCofins | Elemento | 241 - piscofins | tpRetPISCofins | 0-1 | Tipo de Retenção PIS/COFINS e CSLL | Externo |
| **249** | vRetCP | Elemento | 240 - tribFed | ValINSS | 0-1 | Valor do INSS do serviço | Externo |
| **250** | vRetIRRF | Elemento | 240 - tribFed | ValIR | 0-1 | Valor do IRRF do serviço | Externo |
| **251** | vRetCSLL | Elemento | 240 - tribFed | ValCSLL | 0-1 | Valor do CSLL | Externo |
| **252** | totTrib | Grupo | 225 - trib |  | 1-1 |  | Interno |
| **253** | vTotTrib | Grupo | 252 - totTrib |  | 0-1 |  | Interno |
| **254** | vTotTribFed | Elemento | 253 - vTotTrib | ValRetFederais | 1-1 | Valor total das retenções federais | Externo |
| **255** | vTotTribEst | Elemento | 253 - vTotTrib | ValRetEstaduais | 1-1 | Valor monetário total aproximado dos tributos estaduais | Externo |
| **256** | vTotTribMun | Elemento | 253 - vTotTrib | ValRetMunicipais | 1-1 | Valor monetário total aproximado dos tributos municipais | Externo |
| **257** | pTotTrib | Grupo | 252 - totTrib |  | 0-1 |  | Interno |
| **258** | pTotTribFed | Elemento | 257 - pTotTrib | ValAliqRetFederais | 0-1 | Valor percentual total aproximado dos tributos federais | Externo |
| **259** | pTotTribEst | Elemento | 257 - pTotTrib | ValAliqRetEstaduais | 0-1 | Valor percentual total aproximado dos tributos estaduais | Externo |
| **260** | pTotTribMun | Elemento | 257 - pTotTrib | ValAliqRetMunicipais | 0-1 | Valor percentual total aproximado dos tributos municipais | Externo |
| **261** | indTotTrib | Elemento | 252 - totTrib | indTotTrib | 0-1 | Indicador de informação de valor total de tributos. | Interno |
| **262** | pTotTribSN | Elemento | 252 - totTrib | ValAproxTribAliqSN | 0-1 | Valor percentual aproximado do total dos tributos da alíquota do Simples Nacional (%) | Externo |
| **263** | IBSCBS | Grupo | 13 - infDPS |  | 0-1 |  | Interno |
| **264** | finNFSe | Elemento | 263 - IBSCBS | finNFSe | 0-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| **265** | indFinal | Elemento | 263 - IBSCBS | indFinal | 1-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo |
| **266** | cIndOp | Elemento | 263 - IBSCBS | cIndOp | 0-1 | Código indicador da operação de fornecimento, conforme tabela “código indicador de operação | Externo |
| **267** | tpOper | Elemento | 263 - IBSCBS | tpOper | 0-1 | Tipo de Operação com Entes Governanementais ou outrosserviços sobre bens imóveis | Externo |
| **268** | gRefNFSe | Grupo | 263 - IBSCBS | ListaNFSeRef | 0-1 |  | Interno |
| **269** | refNFSe | Elemento | 268 - gRefNFSe | DocChaveNFSe | 1-N | Chave da NFS-e referenciada | Externo |
| **270** | tpEnteGov | Elemento | 263 - IBSCBS | tpEnteGov | 0-1 | Tipo de ente governamental Para administração pública direta e suas autarquias e fundações: | Externo |
| **271** | indDest | Elemento | 263 - IBSCBS | indDest | 1-1 | A respecto do Destinatário dos serviços:0 – O tomador na NFS-e é o adquirente e o destinatário | Externo |
| **272** | dest | Grupo | 263 - IBSCBS |  | 0-1 |  | Interno |
| **273** | CNPJ | Elemento | 272 - dest | DestCNPJ | 0-1 | Número da inscrição no Cadastro Nacional de Pessoa Jurídica do destinatário do serviço | Externo |
| **274** | CPF | Elemento | 272 - dest | DestCPF | 0-1 | CPF do destinatário | Externo |
| **275** | NIF | Elemento | 272 - dest | DestNIF | 0-1 | Número de identificação fiscal fornecido por órgão de administração trib. do exterior | Externo |
| **276** | cNaoNIF | Elemento | 272 - dest | DestcNaoNIF | 0-1 | Motivo não informe NIF | Externo |
| **277** | xNome | Elemento | 272 - dest | DestNome | 0-1 | Nome / Nome Empresarial do destinatário | Externo |
| **278** | end | Grupo | 272 - dest |  | 0-1 |  | Interno |
| **279** | endNac | Grupo | 278 - end |  | 0-1 |  | Interno |
| **280** | cMun | Elemento | 279 - endNac | DestcMun | 0-1 | Código do município do endereço do destinatário (IBGE) | Externo |
| **281** | CEP | Elemento | 279 - endNac | DestCEP | 0-1 | Código numérico do Endereçamento Postal nacional do destinatário | Externo |
| **282** | endExt | Grupo | 278 - end |  | 0-1 |  | Interno |
| **283** | cPais | Elemento | 282 - endExt | DestPais | 0-1 | País do destinatário | Externo |
| **284** | cEndPost | Elemento | 282 - endExt | DestCEP | 0-1 | Código numérico do Endereçamento Postal nacional do destinatário | Externo |
| **285** | xCidade | Elemento | 282 - endExt | DestxMun | 0-1 | Nome da cidade no exterior do destinatário | Externo |
| **286** | xEstProvReg | Elemento | 282 - endExt | DestEstProvReg | 0-1 | Estado, província ou região da cidade no exterior do destinatário | Externo |
| **287** | xLgr | Elemento | 278 - end | DestEndereco | 0-1 | Tipo e nome do logradouro do endereço do destinatário | Externo |
| **288** | nro | Elemento | 278 - end | DestNumero | 0-1 | Número no logradouro do endereço do destinatário | Externo |
| **289** | xCpl | Elemento | 278 - end | DestComplemento | 0-1 | Complemento do endereço do destinatário | Externo |
| **290** | xBairro | Elemento | 278 - end | DestBairro | 0-1 | Bairro do endereço do destinatário do serviço. | Externo |
| **291** | fone | Elemento | 272 - dest | DestFone | 0-1 | Número do telefone do destinatário | Externo |
| **292** | email | Elemento | 272 - dest | DestEmail | 0-1 | Email do destinatário | Externo |
| **293** | imovel | Grupo | 263 - IBSCBS |  | 0-1 |  | Interno |
| **294** | inscImobFisc | Elemento | 293 - imovel | ImovInscImobFisc | 0-1 |  | Externo |
| **295** | cCIB | Elemento | 293 - imovel | ImovCIB | 0-1 | Código do Cadastro Imobiliário Brasileiro - CIB | Externo |
| **296** | end | Grupo | 293 - imovel |  | 0-1 |  | Interno |
| **297** | endNac | Grupo | 296 - end |  | 0-1 |  | Interno |
| **298** | cMun | Elemento | 297 - endNac | ImovcMun | 0-1 | IBGE da Cidade do imóvel | Externo |
| **299** | CEP | Elemento | 297 - endNac | ImovCEP | 0-1 | Código de Endereçamento Postal numérico doendereço nacional do imóvel. | Externo |
| **300** | endExt | Grupo | 296 - end |  | 0-1 |  | Interno |
| **301** | cEndPost | Elemento | 300 - endExt | ImovCEP | 0-1 | Código de Endereçamento Postal numérico doendereço nacional do imóvel. | Externo |
| **302** | xCidade | Elemento | 300 - endExt | ImovxMun | 0-1 | Nome da cidade no exterior, local do imóvel. | Externo |
| **303** | xEstProvReg | Elemento | 300 - endExt | ImovEstProvReg | 0-1 | Estado, província ou região da cidade no exterior,local do imóvel. | Externo |
| **304** | xLgr | Elemento | 296 - end | ImovEndereco | 0-1 | Tipo e nome do logradouro do endereço do imóve | Externo |
| **305** | nro | Elemento | 296 - end | ImovNumero | 0-1 | Número no logradouro do endereço do imóvel. | Externo |
| **306** | xCpl | Elemento | 296 - end | ImovComplemento | 0-1 | Complemento do endereço do imóvel. | Externo |
| **307** | xBairro | Elemento | 296 - end | ImovBairro | 0-1 | Bairro do endereço do imóvel. | Externo |
| **308** | valores | Grupo | 263 - IBSCBS |  | 0-1 |  | Interno |
| **309** | gReeRepRes | Grupo | 308 - valores |  | 0-1 |  | Interno |
| **310** | documentos | Grupo | 309 - gReeRepRes | ListaDocumentosRef | 1-N |  | Interno |
| **311** | dFeNacional | Grupo | 310 - documentos |  | 0-1 |  | Interno |
| **312** | tipoChaveDFe | Elemento | 311 - dFeNacional | DocTipoChaveDFe | 0-1 | Documento fiscal a que se refere a chaveDfe:1 = NFS-e2 = NF-e 3 = CT-e9 = Outro | Externo |
| **313** | xTipoChaveDFe | Elemento | 311 - dFeNacional | DocDescTipoChaveDFe | 0-1 | Descrição da DF-e a que se refere a chaveDfe que seja um dos documentos do Repositório Nacional | Externo |
| **314** | chaveDFe | Elemento | 311 - dFeNacional | DocChaveDFe | 0-1 | Chave do Documento Fiscal eletrônico do repositório nacional referenciado operações já tributadas. | Externo |
| **315** | docFiscalOutro | Grupo | 310 - documentos |  | 0-1 |  | Interno |
| **316** | cMunDocFiscal | Elemento | 315 - docFiscalOutro | DoccMun | 1-1 | Código do município emissor do documento fiscal que não se encuentra no repositório nacional | Externo |
| **317** | nDocFiscal | Elemento | 315 - docFiscalOutro | DocNumero | 0-1 | Número do documento fiscal | Externo |
| **318** | xDocFiscal | Elemento | 315 - docFiscalOutro | DocDescricao | 0-1 | Descrição do documento fiscal. | Externo |
| **319** | docOutro | Grupo | 310 - documentos |  | 0-1 |  | Interno |
| **320** | nDoc | Elemento | 319 - docOutro | DocNumero | 0-1 | Número do documento fiscal | Externo |
| **321** | xDoc | Elemento | 319 - docOutro | DocDescricao | 0-1 | Descrição do documento fiscal. | Externo |
| **322** | fornec | Grupo | 310 - documentos |  | 0-1 |  | Interno |
| **324** | CNPJ | Elemento | 322 - fornec | DocCNPJ | 0-1 | Número da inscrição federal (CNPJ) do fornecedor do documento | Externo |
| **325** | CPF | Elemento | 322 - fornec | DocCPF | 0-1 | Número da inscrição federal (CPF) do fornecedor. | Externo |
| **326** | NIF | Elemento | 322 - fornec | DocNIF | 0-1 | Este elemento só deverá ser preenchido para fornecedores não residentes no Brasil. | Externo |
| **327** | cNaoNIF | Elemento | 322 - fornec | DoccNaoNIF | 0-1 | Motivo para não informação do NIF | Externo |
| **328** | xNome | Elemento | 322 - fornec | DocRazaoSocial | 0-1 | Nome / Razão Social do fornecedor. | Externo |
| **329** | dtEmiDoc | Elemento | 310 - documentos | DocDataEmissao | 0-1 | Data da emissão do documento dedutível.Ano, mês e dia (AAAA-MM-DD) | Externo |
| **330** | dtCompDoc | Elemento | 310 - documentos | DocDCompetencia | 0-1 | Data da competência do documento dedutível.Ano, mês e dia (AAAA-MM-DD) | Externo |
| **331** | tpReeRepRes | Elemento | 310 - documentos | DocTipoReeRepRes | 0-1 | Tipo de valor incluído neste documento | Externo |
| **332** | xTpReeRepRes | Elemento | 310 - documentos | DocDescTpReeRepRes | 0-1 | Descrição do reembolso ou ressarcimento quando a opção é "99" | Externo |
| **333** | vlrReeRepRes | Elemento | 310 - documentos | DocValorReeRepRes | 0-1 | Valor monetário (total ou parcial, conforme documento informado) | Externo |
| **334** | trib | Grupo | 308 - valores |  | 0-1 |  | Interno |
| **335** | gIBSCBS | Grupo | 334 - trib |  | 0-1 |  | Interno |
| **336** | CST | Elemento | 335 - gIBSCBS | CSTIBSCBS | 0-1 | Código de Situação Tributária do IBS e da CBS | Externo |
| **337** | cClassTrib | Elemento | 335 - gIBSCBS | cClassTrib | 0-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo |
| **338** | cCredPres | Elemento | 335 - gIBSCBS | cCredPres | 0-1 | Código e classificação do crédito presumido: IBS e CBS | Externo |
| **339** | gTribRegular | Grupo | 335 - gIBSCBS |  | 0-1 |  | Interno |
| **340** | CSTReg | Elemento | 339 - gTribRegular | CSTReg | 0-1 | Código de Situação Tributária do | Externo |
| **341** | cClassTribReg | Elemento | 339 - gTribRegular | cClassTribReg | 0-1 | Código da Classificação Tributária do | Externo |
| **342** | gDif | Grupo | 335 - gIBSCBS |  | 0-1 |  | Interno |
| **343** | pDifUF | Elemento | 342 - gDif | ValpDifUF | 0-1 | Percentual de diferimento para o IBS estadual | Externo |
| **344** | pDifMun | Elemento | 342 - gDif | ValpDifMun | 0-1 | Percentual de diferimento para o IBS municipal | Externo |
| **345** | pDifCBS | Elemento | 342 - gDif | ValpDifCBS | 0-1 | Percentual de diferimento para a CBS | Externo |
| **346** | gPgtoVinc | Grupo | 263 - IBSCBS |  | 0-1 |  | Interno |
| **347** | pgto | Grupo | 346 - gPgtoVinc |  | 0-1 |  | Interno |
| **348** | nPag | Elemento | 347 - pgto | PgtoNumero | 0-1 | Numerador unico de cada pagamento | Externo |
| **349** | idTransacao | Elemento | 347 - pgto | PgtoIdTransacao | 0-1 | Identificador especifico da transacao financeira | Externo |
| **350** | tpMeioPgto | Elemento | 347 - pgto | PgtoTpMeioPgto | 0-1 | Codigo do meio de pagamento | Externo |
| **351** | CNPJReceb | Elemento | 347 - pgto | PgtoCNPJReceb | 0-1 | CNPJ completo do recebedor do pagamento | Externo |
| **352** | CNPJBasePSP | Elemento | 347 - pgto | PgtoCNPJBasePSP | 0-1 | CNPJ base da instituicao financeira ou PSP do recebedor | Externo |

*Observação: O sequencial 323 não esteve presente no log original e foi mantido em alinhamento com a extração.*