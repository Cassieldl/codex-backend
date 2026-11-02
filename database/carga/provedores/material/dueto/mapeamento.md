# Layout InvoiCy - NFS-e (Estrutura de Dados)

| Sequencial | TAG da prefeitura | Elemento XML | Dentro do grupo | Nome do campo (tag InvoiCy) | Ocorrência | Descrição | Interno / Externo |
|:---:|---|---|---|---|:---:|---|---|
| 1 | DPS | Grupo | 0 - | | 0-1 | | Interno |
| 2 | infDPS | Grupo | 1 - DPS | | 0-1 | | Interno |
| 3 | Id | Atributo | 2 - infDPS | RPSNumero e RPSSerie | 0-1 | Id do Grupo RPS | Interno |
| 4 | tpAmb | Elemento | 2 - infDPS | tpAmb | 1-1 | Ambiente Emissão | Interno |
| 5 | dhEmi | Elemento | 2 - infDPS | dEmis | 0-1 | Data de Emissão | Externo |
| 6 | verAplic | Elemento | 2 - infDPS | | 0-1 | | Interno |
| 7 | serie | Elemento | 2 - infDPS | RPSSerie | 0-1 | Série do RPS | Externo |
| 8 | nDPS | Elemento | 2 - infDPS | RPSNumero | 0-1 | Número do RPS | Externo |
| 9 | dCompet | Elemento | 2 - infDPS | dCompetencia | 0-1 | data de Competência | Externo |
| 10 | tpEmit | Elemento | 2 - infDPS | | 1-1 | | Interno |
| 11 | cLocEmi | Elemento | 2 - infDPS | cMun | 0-1 | Código do Município do prestador | Externo |
| 12 | subst | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 13 | chSubstda | Elemento | 12 - subst | SubsChave | 0-1 | Chave de Acesso NFSe Substituída | Externo |
| 14 | cMotivo | Elemento | 12 - subst | SubsCodMotivo | 0-1 | Código do Motivo da substituição | Externo |
| 15 | xMotivo | Elemento | 12 - subst | SubsDescMotivo | 0-1 | Descrição do Motivo da substituição | Externo |
| 16 | prest | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 17 | CNPJ | Elemento | 16 - prest | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| 18 | CPF | Elemento | 16 - prest | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| 19 | NIF | Elemento | 16 - prest | | 0-1 | | Interno |
| 20 | cNaoNIF | Elemento | 16 - prest | | 0-1 | | Interno |
| 21 | CAEPF | Elemento | 16 - prest | CAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) do prestador do serviço | Externo |
| 22 | IM | Elemento | 16 - prest | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| 23 | xNome | Elemento | 16 - prest | | 0-1 | | Interno |
| 24 | end | Grupo | 16 - prest | | 0-1 | | Interno |
| 25 | endNac | Grupo | 24 - end | | 0-1 | | Interno |
| 26 | cMun | Elemento | 25 - endNac | | 0-1 | | Interno |
| 27 | CEP | Elemento | 25 - endNac | | 0-1 | | Interno |
| 28 | endExt | Grupo | 24 - end | | 0-1 | | Interno |
| 29 | cPais | Elemento | 28 - endExt | | 0-1 | | Interno |
| 30 | cEndPost | Elemento | 28 - endExt | | 0-1 | | Interno |
| 31 | xCidade | Elemento | 28 - endExt | | 0-1 | | Interno |
| 32 | xEstProvReg | Elemento | 28 - endExt | | 0-1 | | Interno |
| 33 | xLgr | Elemento | 24 - end | | 0-1 | | Interno |
| 34 | nro | Elemento | 24 - end | | 0-1 | | Interno |
| 35 | xCpl | Elemento | 24 - end | | 0-1 | | Interno |
| 36 | xBairro | Elemento | 24 - end | | 0-1 | | Interno |
| 37 | fone | Elemento | 16 - prest | | 0-1 | | Interno |
| 38 | email | Elemento | 16 - prest | | 0-1 | | Interno |
| 39 | regTrib | Grupo | 16 - prest | | 0-1 | | Interno |
| 40 | opSimpNac | Elemento | 39 - regTrib | OptSN | 0-1 | Optante Simples Nacional | Externo |
| 41 | regApTribSN | Elemento | 39 - regTrib | regApTribSN | 0-1 | Regime de tributação para empresas Opt. SN ME/EPP que ultrapassam sublimite | Externo |
| 42 | regEspTrib | Elemento | 39 - regTrib | RegEspTrib | 0-1 | Regime Especial de Tributação | Externo |
| 43 | toma | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 44 | CNPJ | Elemento | 43 - toma | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo |
| 45 | CPF | Elemento | 43 - toma | TomaCPF | 0-1 | CPF do Tomador | Externo |
| 46 | NIF | Elemento | 43 - toma | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| 47 | cNaoNIF | Elemento | 43 - toma | TomaExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| 48 | CAEPF | Elemento | 43 - toma | TomaCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| 49 | IM | Elemento | 43 - toma | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo |
| 50 | xNome | Elemento | 43 - toma | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo |
| 51 | end | Grupo | 43 - toma | | 0-1 | | Interno |
| 52 | endNac | Grupo | 51 - end | | 0-1 | | Interno |
| 53 | cMun | Elemento | 52 - endNac | TomacMun | 0-1 | Codigo do Municipio IBGE do Tomador | Externo |
| 54 | CEP | Elemento | 52 - endNac | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| 55 | endExt | Grupo | 51 - end | | 0-1 | | Interno |
| 56 | cPais | Elemento | 55 - endExt | TomaPais | 0-1 | País do Tomador no Exterior | Externo |
| 57 | cEndPost | Elemento | 55 - endExt | TomaCEP | 0-1 | CEP do Tomador (exterior) | Externo |
| 58 | xCidade | Elemento | 55 - endExt | TomaXMun | 0-1 | Nome do Município do tomador (Exterior) | Externo |
| 59 | xEstProvReg | Elemento | 55 - endExt | TomaUF | 0-1 | UF (sigla) do estado do Tomador (exterior) | Externo |
| 60 | xLgr | Elemento | 51 - end | TomaEndereco | 0-1 | Endereço do Tomador | Externo |
| 61 | nro | Elemento | 51 - end | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| 62 | xCpl | Elemento | 51 - end | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo |
| 63 | xBairro | Elemento | 51 - end | TomaBairro | 0-1 | Bairro do Tomador | Externo |
| 64 | fone | Elemento | 43 - toma | TomaTelefone | 0-1 | Telefone do Tomador | Externo |
| 65 | email | Elemento | 43 - toma | TomaEmail | 0-1 | Email do Tomador | Externo |
| 66 | interm | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 67 | CNPJ | Elemento | 66 - interm | IntermCNPJ | 0-1 | CNPJ do Intermediário | Externo |
| 68 | CPF | Elemento | 66 - interm | IntermCPF | 0-1 | CPF do Intermediário | Externo |
| 69 | NIF | Elemento | 66 - interm | IntermNIF | 0-1 | Doc intermediário extrangeiro | Externo |
| 70 | cNaoNIF | Elemento | 66 - interm | IntermExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| 71 | CAEPF | Elemento | 66 - interm | IntermCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| 72 | IM | Elemento | 66 - interm | IntermIM | 0-1 | Inscrição Municipal do Intermediário | Externo |
| 73 | xNome | Elemento | 66 - interm | IntermRazaoSocial | 0-1 | Razão Social do Intermediário | Externo |
| 74 | end | Grupo | 66 - interm | | 0-1 | | Interno |
| 75 | endNac | Grupo | 74 - end | | 0-1 | | Interno |
| 76 | cMun | Elemento | 75 - endNac | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo |
| 77 | CEP | Elemento | 75 - endNac | IntermCep | 0-1 | CEP do Intermediário (nacional) | Externo |
| 78 | endExt | Grupo | 74 - end | | 0-1 | | Interno |
| 79 | cPais | Elemento | 78 - endExt | IntermPais | 0-1 | Código do País do Intermediário | Externo |
| 80 | cEndPost | Elemento | 78 - endExt | IntermCep | 0-1 | CEP do intermediário (exterior) | Externo |
| 81 | xCidade | Elemento | 78 - endExt | IntermXmun | 0-1 | Nome do município do intermediário (exterior) | Externo |
| 82 | xEstProvReg | Elemento | 78 - endExt | IntermUFEx | 0-1 | UF/província/região do intermediário no exterior | Interno |
| 83 | xLgr | Elemento | 74 - end | IntermEndereco | 0-1 | Logradouro do Endereço do Intermediário | Externo |
| 84 | nro | Elemento | 74 - end | IntermNumero | 0-1 | Número do Endereço do Intermediário | Externo |
| 85 | xCpl | Elemento | 74 - end | IntermComplemento | 0-1 | Complemento do Endereço do Intermediário | Externo |
| 86 | xBairro | Elemento | 74 - end | IntermBairro | 0-1 | Bairro do Intermediário | Externo |
| 87 | fone | Elemento | 74 - end | IntermFone | 0-1 | Telefone do Intermediário | Externo |
| 88 | email | Elemento | 74 - end | IntermEmail | 0-1 | E-mail do Intermediário | Externo |
| 89 | serv | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 90 | locPrest | Grupo | 89 - serv | | 1-1 | | Interno |
| 91 | cLocPrestacao | Elemento | 90 - locPrest | SerEndcMun | 0-1 | Local da Prestação do Serviço Cod IBGE Municipio | Externo |
| 92 | cPaisPrestacao | Elemento | 90 - locPrest | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| 93 | opConsumServ | Elemento | 90 - locPrest | LocalPrestServ | 0-1 | Local Da Prestação Do Serviço | Externo |
| 94 | cServ | Grupo | 89 - serv | | 0-1 | | Interno |
| 95 | cTribNac | Elemento | 94 - cServ | codTributNacional | 0-1 | Código de tributação Nacional | Externo |
| 96 | cTribMun | Elemento | 94 - cServ | TributMunicipio | 0-1 | Código de Tributação do Município | Externo |
| 97 | xDescServ | Elemento | 94 - cServ | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| 98 | cNBS | Elemento | 94 - cServ | CodigoNBS | 0-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo |
| 99 | cIntContrib | Elemento | 94 - cServ | Cnae | 0-1 | CNAE - Classificação Nacional de Atividades Econômicas | Externo |
| 100 | comExt | Grupo | 89 - serv | | 0-1 | | Interno |
| 101 | mdPrestacao | Elemento | 100 - comExt | mdPrestacao | 0-1 | Modo de Prestação | Externo |
| 102 | vincPrest | Elemento | 100 - comExt | vincPrest | 0-1 | Vínculo entre as partes no negócio | Externo |
| 103 | tpMoeda | Elemento | 100 - comExt | tpMoeda | 0-1 | Identifica a moeda da transação comercial | Externo |
| 104 | vServMoeda | Elemento | 100 - comExt | ValServEst | 0-1 | Valor do Serviço em Moeda estrangeira | Externo |
| 105 | mecAFComexP | Elemento | 100 - comExt | mecAFComexP | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo prestador do serviço | Externo |
| 106 | mecAFComexT | Elemento | 100 - comExt | mecAFComexT | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo tomador do serviço | Externo |
| 107 | movTempBens | Elemento | 100 - comExt | movTempBens | 0-1 | Operação está vinculada à Movimentação Temporária de Bens | Externo |
| 108 | nDI | Elemento | 100 - comExt | nroDUIMP | 0-1 | Número da Declaração de Importação (DI/DSI/DA/DRI-E) averbado | Externo |
| 109 | nRE | Elemento | 100 - comExt | nroDUE | 0-1 | Número do Registro de Exportação (RE) averbado | Externo |
| 110 | mdic | Elemento | 100 - comExt | mdic | 0-1 | Compartilhar as informações da NFS-e gerada a partir desta DPS com a Secretaria de Comércio Ext | Externo |
| 111 | lsadppu | Grupo | 89 - serv | | 0-1 | | Interno |
| 112 | categ | Elemento | 111 - lsadppu | categServ | 0-1 | Categoria do serviço | Externo |
| 113 | objeto | Elemento | 111 - lsadppu | objetoLocacao | 0-1 | Tipo de objetos da locação, sublocação, arrendamento, direito de passagem ou permissão de uso | Externo |
| 114 | extensao | Elemento | 111 - lsadppu | extensaoFerrovia | 0-1 | Extensão total da ferrovia, rodovia, cabos, dutos ou condutos | Externo |
| 115 | nPostes | Elemento | 111 - lsadppu | nPostes | 0-1 | Número total de postes | Externo |
| 116 | obra | Grupo | 89 - serv | | 0-1 | | Interno |
| 117 | inscImobFisc | Elemento | 116 - obra | ObraMatricula | 0-1 | Número de Matrícula da Obra | Externo |
| 118 | cObra | Elemento | 116 - obra | CodObra | 0-1 | Código da Obra | Externo |
| 119 | end | Grupo | 116 - obra | | 0-1 | | Interno |
| 120 | CEP | Elemento | 119 - end | ObraCEP | 0-1 | CEP da Obra | Externo |
| 121 | endExt | Grupo | 119 - end | | 0-1 | | Interno |
| 122 | cEndPost | Elemento | 121 - endExt | ObraCEP | 0-1 | CEP da Obra (exterior) | Externo |
| 123 | xCidade | Elemento | 121 - endExt | ObraXMunEx | 0-1 | Nome do município da Obra no exterior | Interno |
| 124 | xEstProvReg | Elemento | 121 - endExt | ObraUF | 0-1 | UF da obra do exterior | Externo |
| 125 | xLgr | Elemento | 119 - end | ObraLog | 0-1 | Logradouro da Obra | Externo |
| 126 | nro | Elemento | 119 - end | ObraNumero | 0-1 | Número do End. da Obra | Externo |
| 127 | xCpl | Elemento | 119 - end | ObraCompl | 0-1 | Complemento do End. da Obra | Externo |
| 128 | xBairro | Elemento | 119 - end | ObraBairro | 0-1 | Bairro da Obra | Externo |
| 129 | atvEvento | Grupo | 89 - serv | | 0-1 | | Interno |
| 130 | xNome | Elemento | 129 - atvEvento | AtivDesc | 0-1 | Descrição do evento Artístico, Cultural, Esportivo, etc | Externo |
| 131 | dtIni | Elemento | 129 - atvEvento | AtivDataInicial | 0-1 | Data de início da atividade de evento | Externo |
| 132 | dtFim | Elemento | 129 - atvEvento | AtivDataFinal | 0-1 | Data de fim da atividade de evento | Externo |
| 133 | idAtvEvt | Elemento | 129 - atvEvento | AtivIdEvento | 0-1 | Identificação da Atividade de Evento | Externo |
| 134 | end | Grupo | 129 - atvEvento | | 0-1 | | Interno |
| 135 | CEP | Elemento | 134 - end | | 0-1 | | Interno |
| 136 | endExt | Grupo | 134 - end | | 0-1 | | Interno |
| 137 | cEndPost | Elemento | 136 - endExt | AtivEndCEPEx | 0-1 | CEP do endereço do evento no exterior | Interno |
| 138 | xCidade | Elemento | 136 - endExt | AtivEndxMunEx | 0-1 | Nome do município do endereço do evento do exterior | Interno |
| 139 | xEstProvReg | Elemento | 136 - endExt | AtivEndUfEx | 0-1 | UF/província/Região do evento no exterior | Interno |
| 140 | xLgr | Elemento | 134 - end | | 0-1 | | Interno |
| 141 | nro | Elemento | 134 - end | | 0-1 | | Interno |
| 142 | xCpl | Elemento | 134 - end | | 0-1 | | Interno |
| 143 | xBairro | Elemento | 134 - end | | 0-1 | | Interno |
| 144 | explRod | Grupo | 89 - serv | | 0-1 | | Interno |
| 145 | categVeic | Elemento | 144 - explRod | PedCategVeiculo | 0-1 | Categorias de veículos para cobrança | Externo |
| 146 | nEixos | Elemento | 144 - explRod | PednEixos | 0-1 | Número de eixos para fins de cobrança | Externo |
| 147 | rodagem | Elemento | 144 - explRod | PedRodagem | 0-1 | Tipo de rodagem | Externo |
| 148 | sentido | Elemento | 144 - explRod | PedSentido | 0-1 | Orientação de passagem do veículo | Externo |
| 149 | placa | Elemento | 144 - explRod | PedPlaca | 0-1 | Placa do veículo | Externo |
| 150 | codAcessoPed | Elemento | 144 - explRod | PedCodAcesso | 0-1 | Código de acesso gerado automaticamente pelo sistema emissor da concessionária | Externo |
| 151 | codContrato | Elemento | 144 - explRod | PedCodContrato | 0-1 | Código de contrato gerado automaticamente pelo sistema nacional no cadastro da concessionária | Externo |
| 152 | infoCompl | Grupo | 89 - serv | | 0-1 | | Interno |
| 153 | idDocTec | Elemento | 152 - infoCompl | idDocTec | 0-1 | Identificador de Documento de Responsabilidade Técnica: ART, RRT, DRT, Outros. | Externo |
| 154 | docRef | Elemento | 152 - infoCompl | docRef | 0-1 | Chave da nota fiscal referenciada. | Externo |
| 155 | xInfComp | Elemento | 152 - infoCompl | NFSOutrasinformacoes | 0-1 | Outras Informações / observações | Externo |
| 156 | valores | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 157 | vServPrest | Grupo | 156 - valores | | 0-1 | | Interno |
| 158 | vReceb | Elemento | 157 - vServPrest | | 0-1 | | Interno |
| 159 | vServ | Elemento | 157 - vServPrest | ValServicos | 0-1 | Valor do Serviço | Externo |
| 160 | vDescCondIncond | Grupo | 156 - valores | | 0-1 | | Interno |
| 161 | vDescIncond | Elemento | 160 - vDescCondIncond | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo |
| 162 | vDescCond | Elemento | 160 - vDescCondIncond | ValDescCond | 0-1 | Valor desconto Condicionado | Externo |
| 163 | vDedRed | Grupo | 156 - valores | | 0-1 | | Interno |
| 164 | pDR | Elemento | 163 - vDedRed | ValPercDeducoes | 0-1 | Valor percentual padrão para dedução/redução do valor do serviço | Externo |
| 165 | vDR | Elemento | 163 - vDedRed | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo |
| 166 | documentos | Grupo | 163 - vDedRed | Ded | 0-1 | Grupo Dedução (controle) | Interno |
| 167 | docDedRed | Grupo | 166 - documentos | | 0-1 | | Interno |
| 168 | chNFSe | Elemento | 167 - docDedRed | DedChAcesNacional | 0-1 | Chave de Acesso da NFS-e (Padrão Nacional) | Externo |
| 169 | chNFe | Elemento | 167 - docDedRed | DedChaveAcesso | 0-1 | Chave de acesso da NF-e de referência | Externo |
| 170 | NFSeMun | Grupo | 167 - docDedRed | | 0-1 | | Interno |
| 171 | cMunNFSeMun | Elemento | 170 - NFSeMun | DedcMunNFSe | 0-1 | Código do município da NFS-e de referência | Externo |
| 172 | nNFSeMun | Elemento | 170 - NFSeMun | | 0-1 | | Interno |
| 173 | cVerifNFSeMun | Elemento | 170 - NFSeMun | DedcVerificaNFSe | 0-1 | Código de verificação da NFS-e de referência | Externo |
| 174 | NFNFS | Grupo | 167 - docDedRed | | 0-1 | | Interno |
| 175 | nNFS | Elemento | 174 - NFNFS | DednNFRef | 0-1 | Número da nota de referência da Dedução | Externo |
| 176 | modNFS | Elemento | 174 - NFNFS | | 0-1 | | Interno |
| 177 | serieNFS | Elemento | 174 - NFNFS | | 0-1 | | Interno |
| 178 | nDocFisc | Elemento | 167 - docDedRed | DedOutroDocFisc | 0-1 | Número de outro documento fiscal | Externo |
| 179 | nDoc | Elemento | 167 - docDedRed | DedOutroDoc | 0-1 | Código de outro documento de referência | Externo |
| 180 | tpDedRed | Elemento | 167 - docDedRed | DedRedTipo | 0-1 | Tipo de dedução/Redução modelo nacional | Externo |
| 181 | xDescOutDed | Elemento | 167 - docDedRed | DedRetxDesc | 0-1 | Descrição da Dedução/Redução | Externo |
| 182 | dtEmiDoc | Elemento | 167 - docDedRed | DedDEmis | 0-1 | Data de emissão da NF-e de referência | Externo |
| 183 | vDedutivelRedutivel | Elemento | 167 - docDedRed | DedvlTotRef | 0-1 | Valor total da nota de referencia de dedução | Externo |
| 184 | vDeducaoReducao | Elemento | 167 - docDedRed | DedValor | 0-1 | Valor da Dedução da lista de deduções | Externo |
| 185 | fornec | Grupo | 167 - docDedRed | | 0-1 | | Interno |
| 186 | CNPJ | Elemento | 185 - fornec | DedCNPJRef | 0-1 | CNPJ do fornecedor ou prestador | Externo |
| 187 | CPF | Elemento | 185 - fornec | DedCPFRef | 0-1 | CPF do fornecedor ou prestador | Externo |
| 188 | NIF | Elemento | 185 - fornec | DedNifFornecedor | 0-1 | NIF do Fornecedor do exterior | Externo |
| 189 | cNaoNIF | Elemento | 185 - fornec | DedForncExSemNIF | 0-1 | Motivo para não informação do NIF do fornecedor | Externo |
| 190 | CAEPF | Elemento | 185 - fornec | DedFornCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| 191 | IM | Elemento | 185 - fornec | DedFornIM | 0-1 | Inscrição Municipal do fornecedor | Externo |
| 192 | xNome | Elemento | 185 - fornec | DedFornNome | 0-1 | Razão Social do fornecedor | Externo |
| 193 | end | Grupo | 185 - fornec | | 0-1 | | Interno |
| 194 | endNac | Grupo | 193 - end | | 0-1 | | Interno |
| 195 | cMun | Elemento | 194 - endNac | DedForncMun | 0-1 | Código IBGE do fornecedor | Externo |
| 196 | CEP | Elemento | 194 - endNac | DedFornCEP | 0-1 | CEP do fornecedor | Externo |
| 197 | endExt | Grupo | 193 - end | | 0-1 | | Interno |
| 198 | cPais | Elemento | 197 - endExt | DedPaisFornecedor | 0-1 | Código do país do Fornecedor do exterior | Externo |
| 199 | cEndPost | Elemento | 197 - endExt | DedFornCEPEx | 0-1 | CEP do fornecedor no exterior | Interno |
| 200 | xCidade | Elemento | 197 - endExt | DedFornxMunEx | 0-1 | Nome do Município do fornecedor no exterior | Interno |
| 201 | xEstProvReg | Elemento | 197 - endExt | DedFornUFEx | 0-1 | UF/Província/região do fornecedor no exterior | Interno |
| 202 | xLgr | Elemento | 193 - end | DedFornLogr | 0-1 | Logradouro do endereço do Fornecedor | Externo |
| 203 | nro | Elemento | 193 - end | DedFornNumero | 0-1 | Número do endereço do Fornecedor | Externo |
| 204 | xCpl | Elemento | 193 - end | DedFornCompl | 0-1 | Complemento do endereço do Fornecedor | Externo |
| 205 | xBairro | Elemento | 193 - end | DedFornBairro | 0-1 | Bairro do endereço do Fornecedor | Externo |
| 206 | fone | Elemento | 185 - fornec | DedFornFone | 0-1 | Telefone do fornecedor com DDD | Externo |
| 207 | email | Elemento | 185 - fornec | DedFornEmail | 0-1 | E-mail do fornecedor | Externo |
| 208 | trib | Grupo | 156 - valores | | 0-1 | | Interno |
| 209 | tribMun | Grupo | 208 - trib | | 0-1 | | Interno |
| 210 | tribISSQN | Elemento | 209 - tribMun | natOp | 1-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| 211 | cPaisResult | Elemento | 209 - tribMun | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| 212 | BM | Grupo | 209 - tribMun | | 0-1 | | Interno |
| 213 | tpBM | Elemento | 212 - BM | benefIdNac | 0-1 | Tipo do Benefício Municipal | Externo |
| 214 | nBM | Elemento | 212 - BM | benefNumero | 0-1 | Identificação da Lei parametrizada pelo município que define o benefício no sistema nacional | Externo |
| 215 | vRedBCBM | Elemento | 212 - BM | ValRedBenef | 0-1 | Valor monetário para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| 216 | pRedBCBM | Elemento | 212 - BM | PercRedBenef | 0-1 | Valor percentual para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| 217 | exigSusp | Grupo | 209 - tribMun | | 0-1 | | Interno |
| 218 | tpSusp | Elemento | 217 - exigSusp | tpSusp | 0-1 | Opção para Exigibilidade Suspensa | Externo |
| 219 | nProcesso | Elemento | 217 - exigSusp | NumProcesso | 0-1 | Número do Processo Administrativo | Externo |
| 220 | tpImunidade | Elemento | 209 - tribMun | tpImunidade | 0-1 | Identificação da Imunidade do ISSQN – somente para o caso de Imunidade | Externo |
| 221 | tpRetISSQN | Elemento | 209 - tribMun | ISSRetido | 0-1 | É ISS Retido? | Externo |
| 222 | pAliq | Elemento | 209 - tribMun | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| 223 | tribFed | Grupo | 208 - trib | | 0-1 | | Interno |
| 224 | piscofins | Grupo | 223 - tribFed | | 0-1 | | Interno |
| 225 | CST | Elemento | 224 - piscofins | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo |
| 226 | vBCPisCofins | Elemento | 224 - piscofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| 227 | pAliqPis | Elemento | 224 - piscofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| 228 | pAliqCofins | Elemento | 224 - piscofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| 229 | vPis | Elemento | 224 - piscofins | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| 230 | vCofins | Elemento | 224 - piscofins | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| 231 | tpRetPisCofins | Elemento | 224 - piscofins | tpRetPISCofins | 0-1 | Tipo de Retenção PIS/COFINS e CSLL | Externo |
| 232 | vRetCP | Elemento | 223 - tribFed | ValINSS | 0-1 | Valor do INSS do serviço | Externo |
| 233 | vRetIRRF | Elemento | 223 - tribFed | ValIR | 0-1 | Valor do IRRF do serviço | Externo |
| 234 | vRetCSLL | Elemento | 223 - tribFed | ValCSLL | 0-1 | Valor do CSLL | Externo |
| 235 | totTrib | Grupo | 208 - trib | | 1-1 | | Interno |
| 236 | vTotTrib | Grupo | 235 - totTrib | | 0-1 | | Interno |
| 237 | vTotTribFed | Elemento | 236 - vTotTrib | ValRetFederais | 0-1 | Valor total das retenções federais | Externo |
| 238 | vTotTribEst | Elemento | 236 - vTotTrib | ValRetEstaduais | 0-1 | Valor monetário total aproximado dos tributos estaduais | Externo |
| 239 | vTotTribMun | Elemento | 236 - vTotTrib | ValRetMunicipais | 0-1 | Valor monetário total aproximado dos tributos municipais | Externo |
| 240 | pTotTrib | Grupo | 235 - totTrib | | 0-1 | | Interno |
| 241 | pTotTribFed | Elemento | 240 - pTotTrib | ValAliqRetFederais | 0-1 | Valor percentual total aproximado dos tributos federais | Externo |
| 242 | pTotTribEst | Elemento | 240 - pTotTrib | ValAliqRetEstaduais | 0-1 | Valor percentual total aproximado dos tributos estaduais | Externo |
| 243 | pTotTribMun | Elemento | 240 - pTotTrib | ValAliqRetMunicipais | 0-1 | Valor percentual total aproximado dos tributos municipais | Externo |
| 244 | indTotTrib | Elemento | 235 - totTrib | indTotTrib | 0-1 | Indicador de informação de valor total de tributos. | Interno |
| 245 | pTotTribSN | Elemento | 235 - totTrib | ValAproxTribAliqSN | 0-1 | Valor percentual aproximado do total dos tributos da alíquota do Simples Nacional (%) | Externo |
| 246 | IBSCBS | Grupo | 2 - infDPS | | 0-1 | | Interno |
| 247 | finNFSe | Elemento | 246 - IBSCBS | finNFSe | 0-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| 248 | indFinal | Elemento | 246 - IBSCBS | indFinal | 1-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo |
| 249 | cIndOp | Elemento | 246 - IBSCBS | cIndOp | 0-1 | Código indicador da operação de fornecimento, conforme tabela “código indicador de operação | Externo |
| 250 | tpOper | Elemento | 246 - IBSCBS | tpOper | 0-1 | Tipo de Operação com Entes Governanementais ou outrosserviços sobre bens imóveis | Externo |
| 251 | gRefNFSe | Grupo | 246 - IBSCBS | ListaNFSeRef | 0-1 | | Interno |
| 252 | refNFSe | Elemento | 251 - gRefNFSe | DocChaveNFSe | 1-N | Chave da NFS-e referenciada | Externo |
| 253 | tpEnteGov | Elemento | 246 - IBSCBS | tpEnteGov | 0-1 | Tipo de ente governamental Para administração pública direta e suas autarquias e fundações: | Externo |
| 254 | indDest | Elemento | 246 - IBSCBS | indDest | 1-1 | A respeito do Destinatário dos serviços:0 – O tomador na NFS-e é o adquirente e o destinatário | Externo |
| 255 | dest | Grupo | 246 - IBSCBS | | 0-1 | | Interno |
| 256 | CNPJ | Elemento | 255 - dest | DestCNPJ | 0-1 | Número da inscrição no Cadastro Nacional de Pessoa Jurídica do destinatário do serviço | Externo |
| 257 | CPF | Elemento | 255 - dest | DestCPF | 0-1 | CPF do destinatário | Externo |
| 258 | NIF | Elemento | 255 - dest | DestNIF | 0-1 | Número de identificação fiscal fornecido por órgão de administração trib. do exterior | Externo |
| 259 | cNaoNIF | Elemento | 255 - dest | DestcNaoNIF | 0-1 | Motivo não informe NIF | Externo |
| 260 | xNome | Elemento | 255 - dest | DestNome | 0-1 | Nome / Nome Empresarial do destinatário | Externo |
| 261 | end | Grupo | 255 - dest | | 0-1 | | Interno |
| 262 | endNac | Grupo | 261 - end | | 0-1 | | Interno |
| 263 | cMun | Elemento | 262 - endNac | DestcMun | 0-1 | Código do município do endereço do destinatário (IBGE) | Externo |
| 264 | CEP | Elemento | 262 - endNac | DestCEP | 0-1 | Código numérico do Endereçamento Postal nacional do destinatário | Externo |
| 265 | endExt | Grupo | 261 - end | | 0-1 | | Interno |
| 266 | cPais | Elemento | 265 - endExt | DestPais | 0-1 | País do destinatário | Externo |
| 267 | cEndPost | Elemento | 265 - endExt | DestCEP | 0-1 | Código numérico do Endereçamento Postal nacional do destinatário | Externo |
| 268 | xCidade | Elemento | 265 - endExt | DestxMun | 0-1 | Nome da cidade no exterior do destinatário | Externo |
| 269 | xEstProvReg | Elemento | 265 - endExt | DestEstProvReg | 0-1 | Estado, província ou região da cidade no exterior do destinatário | Externo |
| 270 | xLgr | Elemento | 261 - end | DestEndereco | 0-1 | Tipo e nome do logradouro do endereço do destinatário | Externo |
| 271 | nro | Elemento | 261 - end | DestNumero | 0-1 | Número no logradouro do endereço do destinatário | Externo |
| 272 | xCpl | Elemento | 261 - end | DestComplemento | 0-1 | Complemento do endereço do destinatário | Externo |
| 273 | xBairro | Elemento | 261 - end | DestBairro | 0-1 | Bairro do endereço do destinatário do serviço. | Externo |
| 274 | fone | Elemento | 255 - dest | DestFone | 0-1 | Número do telefone do destinatário | Externo |
| 275 | email | Elemento | 255 - dest | DestEmail | 0-1 | Email do destinatário | Externo |
| 276 | imovel | Grupo | 246 - IBSCBS | | 0-1 | | Interno |
| 277 | inscImobFisc | Elemento | 276 - imovel | ImovInscImobFisc | 0-1 | | Externo |
| 278 | cCIB | Elemento | 276 - imovel | ImovCIB | 0-1 | Código do Cadastro Imobiliário Brasileiro - CIB | Externo |
| 279 | end | Grupo | 276 - imovel | | 0-1 | | Interno |
| 280 | CEP | Elemento | 279 - end | ImovCEP | 0-1 | Código de Endereçamento Postal numérico doendereço nacional do imóvel. | Externo |
| 281 | endExt | Grupo | 279 - end | | 0-1 | | Interno |
| 282 | cEndPost | Elemento | 281 - endExt | ImovCEP | 0-1 | Código de Endereçamento Postal numérico doendereço nacional do imóvel. | Externo |
| 283 | xCidade | Elemento | 281 - endExt | ImovxMun | 0-1 | Nome da cidade no exterior, local do imóvel. | Externo |
| 284 | xEstProvReg | Elemento | 281 - endExt | ImovEstProvReg | 0-1 | Estado, província ou região da cidade no exterior,local do imóvel. | Externo |
| 285 | xLgr | Elemento | 279 - end | ImovEndereco | 0-1 | Tipo e nome do logradouro do endereço do imóve | Externo |
| 286 | nro | Elemento | 279 - end | ImovNumero | 0-1 | Número no logradouro do endereço do imóvel. | Externo |
| 287 | xCpl | Elemento | 279 - end | ImovComplemento | 0-1 | Complemento do endereço do imóvel. | Externo |
| 288 | xBairro | Elemento | 279 - end | ImovBairro | 0-1 | Bairro do endereço do imóvel. | Externo |
| 289 | valores | Grupo | 246 - IBSCBS | | 0-1 | | Interno |
| 290 | gReeRepRes | Grupo | 289 - valores | | 0-1 | | Interno |
| 291 | documentos | Grupo | 290 - gReeRepRes | ListaDocumentosRef | 1-N | | Interno |
| 292 | dFeNacional | Grupo | 291 - documentos | | 0-1 | | Interno |
| 293 | tipoChaveDFe | Elemento | 292 - dFeNacional | DocTipoChaveDFe | 0-1 | Documento fiscal a que se refere a chaveDfe:1 = NFS-e2 = NF-e 3 = CT-e9 = Outro | Externo |
| 294 | xTipoChaveDFe | Elemento | 292 - dFeNacional | DocDescTipoChaveDFe | 0-1 | Descrição da DF-e a que se refere a chaveDfe que seja um dos documentos do Repositório Nacional | Externo |
| 295 | chaveDFe | Elemento | 292 - dFeNacional | DocChaveDFe | 0-1 | Chave do Documento Fiscal eletrônico do repositório nacional referenciado operações já tributadas. | Externo |
| 296 | docFiscalOutro | Grupo | 291 - documentos | | 0-1 | | Interno |
| 297 | cMunDocFiscal | Elemento | 296 - docFiscalOutro | DoccMun | 1-1 | Código do município emissor do documento fiscal que não se encontra no repositório nacional | Externo |
| 298 | nDocFiscal | Elemento | 296 - docFiscalOutro | DocNumero | 0-1 | Número do documento fiscal | Externo |
| 299 | xDocFiscal | Elemento | 296 - docFiscalOutro | DocDescricao | 0-1 | Descrição do documento fiscal. | Externo |
| 300 | docOutro | Grupo | 291 - documentos | | 0-1 | | Interno |
| 301 | nDoc | Elemento | 300 - docOutro | DocNumero | 0-1 | Número do documento fiscal | Externo |
| 302 | xDoc | Elemento | 300 - docOutro | DocDescricao | 0-1 | Descrição do documento fiscal. | Externo |
| 303 | fornec | Grupo | 291 - documentos | | 0-1 | | Interno |
| 305 | CNPJ | Elemento | 303 - fornec | DocCNPJ | 0-1 | Número da inscrição federal (CNPJ) do fornecedor do documento | Externo |
| 306 | CPF | Elemento | 303 - fornec | DocCPF | 0-1 | Número da inscrição federal (CPF) do fornecedor. | Externo |
| 307 | NIF | Elemento | 303 - fornec | DocNIF | 0-1 | Este elemento só deverá ser preenchido para fornecedores não residentes no Brasil. | Externo |
| 308 | cNaoNIF | Elemento | 303 - fornec | DoccNaoNIF | 0-1 | "Motivo para não informação do NIF | Externo |
| 309 | xNome | Elemento | 303 - fornec | DocRazaoSocial | 0-1 | Nome / Razão Social do fornecedor. | Externo |
| 310 | dtEmiDoc | Elemento | 291 - documentos | DocDataEmissao | 0-1 | "Data da emissão do documento dedutível.Ano, mês e dia (AAAA-MM-DD)" | Externo |
| 311 | dtCompDoc | Elemento | 291 - documentos | DocDCompetencia | 0-1 | "Data da competência do documento dedutível.Ano, mês e dia (AAAA-MM-DD)" | Externo |
| 312 | tpReeRepRes | Elemento | 291 - documentos | DocTipoReeRepRes | 0-1 | Tipo de valor incluído neste documento | Externo |
| 313 | xTpReeRepRes | Elemento | 291 - documentos | DocDescTpReeRepRes | 0-1 | "Descrição do reembolso ou ressarcimento quando a opção é "99" | Externo |
| 314 | vlrReeRepRes | Elemento | 291 - documentos | DocValorReeRepRes | 0-1 | Valor monetário (total ou parcial, conforme documento informado) | Externo |
| 315 | trib | Grupo | 289 - valores | | 0-1 | | Interno |
| 316 | gIBSCBS | Grupo | 315 - trib | | 0-1 | | Interno |
| 317 | CST | Elemento | 316 - gIBSCBS | CSTIBSCBS | 0-1 | Código de Situação Tributária do IBS e da CBS | Externo |
| 318 | cClassTrib | Elemento | 316 - gIBSCBS | cClassTrib | 0-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo |
| 319 | cCredPres | Elemento | 316 - gIBSCBS | cCredPres | 0-1 | Código e classificação do crédito presumido: IBS e CBS | Externo |
| 320 | gTribRegular | Grupo | 316 - gIBSCBS | | 0-1 | | Interno |
| 321 | CSTReg | Elemento | 320 - gTribRegular | CSTReg | 0-1 | "Código de Situação Tributária do | Externo |
| 322 | cClassTribReg | Elemento | 320 - gTribRegular | cClassTribReg | 0-1 | "Código da Classificação Tributária do | Externo |
| 323 | gDif | Grupo | 316 - gIBSCBS | | 0-1 | | Interno |
| 324 | pDifUF | Elemento | 323 - gDif | ValpDifUF | 0-1 | Percentual de diferimento para o IBS estadual | Externo |
| 325 | pDifMun | Elemento | 323 - gDif | ValpDifMun | 0-1 | Percentual de diferimento para o IBS municipal | Externo |
| 326 | pDifCBS | Elemento | 323 - gDif | ValpDifCBS | 0-1 | Percentual de diferimento para a CBS | Externo |