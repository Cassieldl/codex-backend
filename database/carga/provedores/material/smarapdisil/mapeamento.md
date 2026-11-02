# Layout InvoiCy - NFS-e (Padrão Nacional)

| Sequencial | TAG da prefeitura | Elemento XML | Dentro do grupo | Nome do campo (tag InvoiCy) | Ocorrência | Descrição | Interno / Externo |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | NFSe | Grupo | 0 - | | 0-1 | | Interno |
| 2 | infNFSe | Grupo | 1 - NFSe | | 0-1 | | Interno |
| 3 | Id | Atributo | 2 - infNFSe | infNFSe | 0-1 | infNFSe | Interno |
| 4 | xLocEmi | Elemento | 2 - infNFSe | EmpnMun | 0-1 | Nome do Município do Prestador | Externo |
| 5 | xLocPrestacao | Elemento | 2 - infNFSe | SerEndXMun | 0-1 | Local da Prestação do Serviço Municipio | Externo |
| 6 | nNFSe | Elemento | 2 - infNFSe | RPSNumero | 0-1 | Número do RPS | Externo |
| 7 | cLocIncid | Elemento | 2 - infNFSe | cMunIncidencia | 0-1 | Codigo do Municipio IBGE de Incidência do imposto | Externo |
| 8 | xLocIncid | Elemento | 2 - infNFSe | cMunIncidencia | 0-1 | Codigo do Municipio IBGE de Incidência do imposto | Externo |
| 10 | xTribNac | Elemento | 2 - infNFSe | codTributNacional | 0-1 | Código de tributação Nacional | Externo |
| 11 | verAplic | Elemento | 2 - infNFSe | | 1-1 | | Interno |
| 12 | ambGer | Elemento | 2 - infNFSe | | 1-1 | | Interno |
| 13 | tpEmis | Elemento | 2 - infNFSe | tpAmb | 0-1 | Ambiente Emissão | Interno |
| 14 | cStat | Elemento | 2 - infNFSe | | 1-1 | | Interno |
| 15 | dhProc | Elemento | 2 - infNFSe | dEmis | 0-1 | Data de Emissão | Externo |
| 16 | nDFSe | Elemento | 2 - infNFSe | RPSNumero | 0-1 | Número do RPS | Externo |
| 17 | emit | Grupo | 2 - infNFSe | | 0-1 | | Interno |
| 18 | CNPJ | Elemento | 17 - emit | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| 19 | IM | Elemento | 17 - emit | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| 20 | xNome | Elemento | 17 - emit | xNome | 0-1 | Razão Social do Prestador | Externo |
| 21 | enderNac | Grupo | 17 - emit | | 0-1 | | Interno |
| 22 | xLgr | Elemento | 21 - enderNac | xLgr | 0-1 | Logradouro do Prestador | Externo |
| 23 | nro | Elemento | 21 - enderNac | nro | 0-1 | Número do Endereço do Prestador | Externo |
| 24 | xCpl | Elemento | 21 - enderNac | xCpl | 0-1 | Complemento do Endereço do Prestador | Externo |
| 25 | xBairro | Elemento | 21 - enderNac | xBairro | 0-1 | Bairro do Prestador | Externo |
| 26 | cMun | Elemento | 21 - enderNac | cMun | 0-1 | Código do Município do prestador | Externo |
| 27 | UF | Elemento | 21 - enderNac | UF | 0-1 | UF (sigla) do estado do Prestador | Externo |
| 28 | CEP | Elemento | 21 - enderNac | CEP | 0-1 | CEP do Prestador | Externo |
| 29 | fone | Elemento | 17 - emit | fone | 0-1 | Fone do Prestador | Externo |
| 30 | email | Elemento | 17 - emit | Email | 0-1 | Email do Prestador | Externo |
| 31 | valores | Grupo | 2 - infNFSe | | 0-1 | | Interno |
| 32 | vBC | Elemento | 31 - valores | ValBaseCalculo | 0-1 | Base de cálculo ISS | Externo |
| 33 | pAliqAplic | Elemento | 31 - valores | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| 34 | vISSQN | Elemento | 31 - valores | ValISS | 0-1 | Valor do ISS | Externo |
| 35 | vTotalRet | Elemento | 31 - valores | ValOutrasRetencoes | 0-1 | Valor de Outras Retenções do serviço | Externo |
| 36 | vLiq | Elemento | 31 - valores | ValLiquido | 0-1 | Valor Líquido do Serviço | Externo |
| 37 | IBSCBS | Grupo | 2 - infNFSe | | 0-1 | | Interno |
| 38 | cLocalidadeIncid | Elemento | 37 - IBSCBS | cLocalidadeIncid | 0-1 | Código IBGE da localidade de incidência do IBS/CBS (local da operação). | Externo |
| 39 | xLocalidadeIncid | Elemento | 37 - IBSCBS | xLocalidadeIncid | 0-1 | | Externo |
| 40 | valores | Grupo | 37 - IBSCBS | | 0-1 | | Interno |
| 41 | vBC | Elemento | 40 - valores | ValBCIBSCBS | 0-1 | Valor da base de cálculo (BC) do IBS/CBS antes das reduções para cálculo do tributo bruto. | Externo |
| 42 | uf | Grupo | 40 - valores | | 0-1 | | Interno |
| 43 | pIBSUF | Elemento | 42 - uf | ValpIBSUF | 0-1 | | Externo |
| 44 | pRedAliqUF | Elemento | 42 - uf | ValRedAliqUF | 0-1 | Percentual de redução de alíquota estadual. | Externo |
| 45 | pAliqEfetUF | Elemento | 42 - uf | ValpAliqEfetUF | 0-1 | pAliqEfetUF = pIBSUF x pRedAliqUF | Externo |
| 46 | mun | Grupo | 40 - valores | | 0-1 | | Interno |
| 47 | pIBSMun | Elemento | 46 - mun | ValpIBSMun | 0-1 | Aliquota do IBS Municipal | Externo |
| 48 | pRedAliqMun | Elemento | 46 - mun | ValpRedAliqMun | 0-1 | Percentual de redução de alíquota Municipal | Externo |
| 49 | pAliqEfetMun | Elemento | 46 - mun | ValpAliqEfetMun | 0-1 | pAliqEfetMun = pIBSMun x pRedAliqMun | Externo |
| 50 | fed | Grupo | 40 - valores | | 0-1 | | Interno |
| 51 | pCBS | Elemento | 50 - fed | ValpCBS | 0-1 | Alíquota da UF para CBS da localidade de incidência | Externo |
| 52 | pRedAliqCBS | Elemento | 50 - fed | ValpRedAliqCBS | 0-1 | Percentual da redução de alíquota CBS | Externo |
| 53 | pAliqEfetCBS | Elemento | 50 - fed | ValpAliqEfetCBS | 0-1 | pAliqEfetCBS = pCBS x pRedAliqCBS | Externo |
| 54 | totCIBS | Grupo | 37 - IBSCBS | | 0-1 | | Interno |
| 55 | vTotNF | Elemento | 54 - totCIBS | ValTotNF | 0-1 | "Valor Total da NF considerando os impostospor fora: IBS e CBS.O IBS e a CBS são por fora" | Externo |
| 56 | gIBS | Grupo | 54 - totCIBS | | 0-1 | | Interno |
| 57 | vIBSTot | Elemento | 56 - gIBS | ValIBSTot | 0-1 | "Valor total do IBS. | Externo |
| 58 | gIBSUFTot | Grupo | 56 - gIBS | | 0-1 | | Interno |
| 59 | vIBSUF | Elemento | 58 - gIBSUFTot | ValIBSUF | 0-1 | "Total valor do IBS estadual. | Externo |
| 60 | gIBSMunTot | Grupo | 56 - gIBS | | 0-1 | | Interno |
| 61 | vIBSMun | Elemento | 60 - gIBSMunTot | ValIBSMun | 0-1 | "Total valor do IBS municipal. | Externo |
| 62 | gCBS | Grupo | 54 - totCIBS | | 0-1 | | Interno |
| 63 | vCBS | Elemento | 62 - gCBS | ValCBS | 0-1 | "Total valor da CBS da União. | Externo |
| 64 | DPS | Grupo | 2 - infNFSe | | 0-1 | | Interno |
| 65 | xmlns | Atributo | 64 - DPS | | 0-1 | | Interno |
| 66 | versao | Atributo | 64 - DPS | | 1-1 | | Interno |
| 67 | infDPS | Grupo | 64 - DPS | | 0-1 | | Interno |
| 68 | Id | Atributo | 67 - infDPS | RPSNumero e RPSSerie | 0-1 | Id do Grupo RPS | Interno |
| 69 | tpAmb | Elemento | 67 - infDPS | tpAmb | 1-1 | Ambiente Emissão | Interno |
| 70 | dhEmi | Elemento | 67 - infDPS | dEmis | 0-1 | Data de Emissão | Externo |
| 71 | verAplic | Elemento | 67 - infDPS | | 0-1 | | Interno |
| 72 | serie | Elemento | 67 - infDPS | RPSSerie | 0-1 | Série do RPS | Externo |
| 73 | nDPS | Elemento | 67 - infDPS | RPSNumero | 0-1 | Número do RPS | Externo |
| 74 | dCompet | Elemento | 67 - infDPS | dCompetencia | 0-1 | data de Competência | Externo |
| 75 | tpEmit | Elemento | 67 - infDPS | | 1-1 | | Interno |
| 76 | cLocEmi | Elemento | 67 - infDPS | cMun | 0-1 | Código do Município do prestador | Externo |
| 77 | subst | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 78 | chSubstda | Elemento | 77 - subst | SubsChave | 0-1 | Chave de Acesso NFSe Substituída | Externo |
| 79 | cMotivo | Elemento | 77 - subst | SubsCodMotivo | 0-1 | Código do Motivo da substituição | Externo |
| 80 | xMotivo | Elemento | 77 - subst | SubsDescMotivo | 0-1 | Descrição do Motivo da substituição | Externo |
| 81 | prest | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 82 | CNPJ | Elemento | 81 - prest | CNPJ_prest | 0-1 | Cnpj do Prestador | Externo |
| 83 | CPF | Elemento | 81 - prest | CNPJ_prest (CPF) | 0-1 | CPF do Prestador | Externo |
| 84 | NIF | Elemento | 81 - prest | | 0-1 | | Interno |
| 85 | cNaoNIF | Elemento | 81 - prest | | 0-1 | | Interno |
| 86 | CAEPF | Elemento | 81 - prest | CAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) do prestador do serviço | Externo |
| 87 | IM | Elemento | 81 - prest | IM | 0-1 | Inscrição Municipal do Prestador | Externo |
| 88 | fone | Elemento | 81 - prest | fone | 0-1 | Fone do Prestador | Externo |
| 89 | email | Elemento | 81 - prest | Email | 0-1 | Email do Prestador | Externo |
| 90 | xNome | Elemento | 81 - prest | | 0-1 | | Interno |
| 91 | end | Grupo | 81 - prest | | 0-1 | | Interno |
| 92 | endNac | Grupo | 91 - end | | 0-1 | | Interno |
| 93 | cMun | Elemento | 92 - endNac | | 0-1 | | Interno |
| 94 | CEP | Elemento | 92 - endNac | | 0-1 | | Interno |
| 95 | endExt | Grupo | 91 - end | | 0-1 | | Interno |
| 96 | cPais | Elemento | 95 - endExt | | 0-1 | | Interno |
| 97 | cEndPost | Elemento | 95 - endExt | | 0-1 | | Interno |
| 98 | xCidade | Elemento | 95 - endExt | | 0-1 | | Interno |
| 99 | xEstProvReg | Elemento | 95 - endExt | | 0-1 | | Interno |
| 100 | xLgr | Elemento | 91 - end | | 0-1 | | Interno |
| 101 | nro | Elemento | 91 - end | | 0-1 | | Interno |
| 102 | xCpl | Elemento | 91 - end | | 0-1 | | Interno |
| 103 | xBairro | Elemento | 91 - end | | 0-1 | | Interno |
| 104 | fone | Elemento | 81 - prest | | 0-1 | | Interno |
| 105 | email | Elemento | 81 - prest | | 0-1 | | Interno |
| 106 | regTrib | Grupo | 81 - prest | | 0-1 | | Interno |
| 107 | opSimpNac | Elemento | 106 - regTrib | OptSN | 0-1 | Optante Simples Nacional | Externo |
| 108 | regApTribSN | Elemento | 106 - regTrib | regApTribSN | 0-1 | Regime de tributação para empresas Opt. SN ME/EPP que ultrapassam sublimite | Externo |
| 109 | regEspTrib | Elemento | 106 - regTrib | RegEspTrib | 1-1 | Regime Especial de Tributação | Externo |
| 110 | toma | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 111 | CNPJ | Elemento | 110 - toma | TomaCNPJ | 0-1 | CNPJ do Tomador | Externo |
| 112 | CPF | Elemento | 110 - toma | TomaCPF | 0-1 | CPF do Tomador | Externo |
| 113 | NIF | Elemento | 110 - toma | DocTomadorEstrangeiro | 0-1 | Descrição documento estrangeiro do Tomador | Externo |
| 114 | cNaoNIF | Elemento | 110 - toma | TomaExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| 115 | CAEPF | Elemento | 110 - toma | TomaCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| 116 | IM | Elemento | 110 - toma | TomaIM | 0-1 | Inscrição Municipal do Tomador | Externo |
| 117 | xNome | Elemento | 110 - toma | TomaRazaoSocial | 0-1 | Razão Social do Tomador | Externo |
| 118 | end | Grupo | 110 - toma | | 0-1 | | Interno |
| 119 | endNac | Grupo | 118 - end | | 0-1 | | Interno |
| 120 | cMun | Elemento | 119 - endNac | TomacMun | 0-1 | Codigo do Municipio IBGE do Tomador | Externo |
| 121 | CEP | Elemento | 119 - endNac | TomaCEP | 0-1 | CEP do Tomador (Nacional) | Externo |
| 122 | endExt | Grupo | 118 - end | | 0-1 | | Interno |
| 123 | cPais | Elemento | 122 - endExt | TomaPais | 0-1 | País do Tomador no Exterior | Externo |
| 124 | cEndPost | Elemento | 122 - endExt | TomaCEP | 0-1 | CEP do Tomador (exterior) | Externo |
| 125 | xCidade | Elemento | 122 - endExt | TomaXMun | 0-1 | Nome do Município do tomador (Exterior) | Externo |
| 126 | xEstProvReg | Elemento | 122 - endExt | TomaUF | 0-1 | UF (sigla) do estado do Tomador (exterior) | Externo |
| 127 | xLgr | Elemento | 118 - end | TomaEndereco | 0-1 | Endereço do Tomador | Externo |
| 128 | nro | Elemento | 118 - end | TomaNumero | 0-1 | Número do Endereço do Tomador | Externo |
| 129 | xCpl | Elemento | 118 - end | TomaComplemento | 0-1 | Complemento do Endereço do Tomador | Externo |
| 130 | xBairro | Elemento | 118 - end | TomaBairro | 0-1 | Bairro do Tomador | Externo |
| 131 | fone | Elemento | 110 - toma | TomaTelefone | 0-1 | Telefone do Tomador | Externo |
| 132 | email | Elemento | 110 - toma | TomaEmail | 0-1 | Email do Tomador | Externo |
| 133 | interm | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 134 | CNPJ | Elemento | 133 - interm | IntermCNPJ | 0-1 | CNPJ do Intermediário | Externo |
| 135 | CPF | Elemento | 133 - interm | IntermCPF | 0-1 | CPF do Intermediário | Externo |
| 136 | NIF | Elemento | 133 - interm | IntermNIF | 0-1 | Doc intermediário extrangeiro | Externo |
| 137 | cNaoNIF | Elemento | 133 - interm | IntermExSemNIF | 0-1 | Motivo para não informação do NIF | Externo |
| 138 | CAEPF | Elemento | 133 - interm | IntermCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| 139 | IM | Elemento | 133 - interm | IntermIM | 0-1 | Inscrição Municipal do Intermediário | Externo |
| 140 | xNome | Elemento | 133 - interm | IntermRazaoSocial | 0-1 | Razão Social do Intermediário | Externo |
| 141 | end | Grupo | 133 - interm | | 0-1 | | Interno |
| 142 | endNac | Grupo | 141 - end | | 0-1 | | Interno |
| 143 | cMun | Elemento | 142 - endNac | IntermCmun | 0-1 | Código IBGE do município do Intermediário | Externo |
| 144 | CEP | Elemento | 142 - endNac | IntermCep | 0-1 | CEP do Intermediário (nacional) | Externo |
| 145 | endExt | Grupo | 141 - end | | 0-1 | | Interno |
| 146 | cPais | Elemento | 145 - endExt | IntermPais | 0-1 | Código do País do Intermediário | Externo |
| 147 | cEndPost | Elemento | 145 - endExt | IntermCep | 0-1 | CEP do intermediário (exterior) | Externo |
| 148 | xCidade | Elemento | 145 - endExt | IntermXmun | 0-1 | Nome do município do intermediário (exterior) | Externo |
| 149 | xEstProvReg | Elemento | 145 - endExt | IntermUFEx | 0-1 | UF/província/região do intermediário no exterior | Interno |
| 150 | xLgr | Elemento | 141 - end | IntermEndereco | 0-1 | Logradouro do Endereço do Intermediário | Externo |
| 151 | nro | Elemento | 141 - end | IntermNumero | 0-1 | Número do Endereço do Intermediário | Externo |
| 152 | xCpl | Elemento | 141 - end | IntermComplemento | 0-1 | Complemento do Endereço do Intermediário | Externo |
| 153 | xBairro | Elemento | 141 - end | IntermBairro | 0-1 | Bairro do Intermediário | Externo |
| 154 | fone | Elemento | 141 - end | IntermFone | 0-1 | Telefone do Intermediário | Externo |
| 155 | email | Elemento | 141 - end | IntermEmail | 0-1 | E-mail do Intermediário | Externo |
| 156 | serv | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 157 | locPrest | Grupo | 156 - serv | | 1-1 | | Interno |
| 158 | cLocPrestacao | Elemento | 157 - locPrest | SerEndcMun | 1-1 | Local da Prestação do Serviço Cod IBGE Municipio | Externo |
| 159 | cPaisPrestacao | Elemento | 157 - locPrest | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| 160 | opConsumServ | Elemento | 157 - locPrest | LocalPrestServ | 0-1 | Local Da Prestação Do Serviço | Externo |
| 161 | cServ | Grupo | 156 - serv | | 0-1 | | Interno |
| 162 | cTribNac | Elemento | 161 - cServ | codTributNacional | 0-1 | Código de tributação Nacional | Externo |
| 163 | cTribMun | Elemento | 161 - cServ | TributMunicipio | 0-1 | Código de Tributação do Município | Externo |
| 164 | xDescServ | Elemento | 161 - cServ | Discriminacao | 0-1 | Descrição do Serviço | Externo |
| 165 | cNBS | Elemento | 161 - cServ | CodigoNBS | 0-1 | Código da Nomenclatura Brasileira de Serviços (NBS) | Externo |
| 166 | cIntContrib | Elemento | 161 - cServ | | 0-1 | | Interno |
| 167 | comExt | Grupo | 156 - serv | | 0-1 | | Interno |
| 168 | mdPrestacao | Elemento | 167 - comExt | mdPrestacao | 0-1 | Modo de Prestação | Externo |
| 169 | vincPrest | Elemento | 167 - comExt | vincPrest | 0-1 | Vínculo entre as partes no negócio | Externo |
| 170 | tpMoeda | Elemento | 167 - comExt | tpMoeda | 0-1 | Identifica a moeda da transação comercial | Externo |
| 171 | vServMoeda | Elemento | 167 - comExt | ValServEst | 0-1 | Valor do Serviço em Moeda estrangeira | Externo |
| 172 | mecAFComexP | Elemento | 167 - comExt | mecAFComexP | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo prestador do serviço | Externo |
| 173 | mecAFComexT | Elemento | 167 - comExt | mecAFComexT | 0-1 | Mecanismo de apoio/fomento ao Comércio Exterior utilizado pelo tomador do serviço | Externo |
| 174 | movTempBens | Elemento | 167 - comExt | movTempBens | 0-1 | Operação está vinculada à Movimentação Temporária de Bens | Externo |
| 175 | nDI | Elemento | 167 - comExt | nroDUIMP | 0-1 | Número da Declaração de Importação (DI/DSI/DA/DRI-E) averbado | Externo |
| 176 | nRE | Elemento | 167 - comExt | nroDUE | 0-1 | Número do Registro de Exportação (RE) averbado | Externo |
| 177 | mdic | Elemento | 167 - comExt | mdic | 0-1 | Compartilhar as informações da NFS-e gerada a partir desta DPS com a Secretaria de Comércio Ext | Externo |
| 178 | lsadppu | Grupo | 156 - serv | | 0-1 | | Interno |
| 179 | categ | Elemento | 178 - lsadppu | categServ | 0-1 | Categoria do serviço | Externo |
| 180 | objeto | Elemento | 178 - lsadppu | objetoLocacao | 0-1 | Tipo de objetos da locação, sublocação, arrendamento, direito de passagem ou permissão de uso | Externo |
| 181 | extensao | Elemento | 178 - lsadppu | extensaoFerrovia | 0-1 | Extensão total da ferrovia, rodovia, cabos, dutos ou condutos | Externo |
| 182 | nPostes | Elemento | 178 - lsadppu | nPostes | 0-1 | Número total de postes | Externo |
| 183 | obra | Grupo | 156 - serv | | 0-1 | | Interno |
| 184 | inscImobFisc | Elemento | 183 - obra | ObraMatricula | 0-1 | Número de Matrícula da Obra | Externo |
| 185 | cObra | Elemento | 183 - obra | CodObra | 0-1 | Código da Obra | Externo |
| 186 | end | Grupo | 183 - obra | | 0-1 | | Interno |
| 187 | CEP | Elemento | 186 - end | ObraCEP | 0-1 | CEP da Obra | Externo |
| 188 | endExt | Grupo | 186 - end | | 0-1 | | Interno |
| 189 | cEndPost | Elemento | 188 - endExt | ObraCEP | 0-1 | CEP da Obra (exterior) | Externo |
| 190 | xCidade | Elemento | 188 - endExt | ObraXMunEx | 0-1 | Nome do município da Obra no exterior | Interno |
| 191 | xEstProvReg | Elemento | 188 - endExt | ObraUF | 0-1 | UF da obra do exterior | Externo |
| 192 | xLgr | Elemento | 186 - end | ObraLog | 0-1 | Logradouro da Obra | Externo |
| 193 | nro | Elemento | 186 - end | ObraNumero | 0-1 | Número do End. da Obra | Externo |
| 194 | xCpl | Elemento | 186 - end | ObraCompl | 0-1 | Complemento do End. da Obra | Externo |
| 195 | xBairro | Elemento | 186 - end | ObraBairro | 0-1 | Bairro da Obra | Externo |
| 196 | atvEvento | Grupo | 156 - serv | | 0-1 | | Interno |
| 197 | xNome | Elemento | 196 - atvEvento | AtivDesc | 0-1 | Descrição do evento Artístico, Cultural, Esportivo, etc | Externo |
| 198 | dtIni | Elemento | 196 - atvEvento | AtivDataInicial | 0-1 | Data de início da atividade de evento | Externo |
| 199 | dtFim | Elemento | 196 - atvEvento | AtivDataFinal | 0-1 | Data de fim da atividade de evento | Externo |
| 200 | idAtvEvt | Elemento | 196 - atvEvento | AtivIdEvento | 0-1 | Identificação da Atividade de Evento | Externo |
| 201 | end | Grupo | 196 - atvEvento | | 0-1 | | Interno |
| 202 | CEP | Elemento | 201 - end | | 0-1 | | Interno |
| 203 | endExt | Grupo | 201 - end | | 0-1 | | Interno |
| 204 | cEndPost | Elemento | 203 - endExt | AtivEndCEPEx | 0-1 | CEP do endereço do evento no exterior | Interno |
| 205 | xCidade | Elemento | 203 - endExt | AtivEndxMunEx | 0-1 | Nome do município do endereço do evento do exterior | Interno |
| 206 | xEstProvReg | Elemento | 203 - endExt | AtivEndUfEx | 0-1 | UF/província/Região do evento no exterior | Interno |
| 207 | xLgr | Elemento | 201 - end | | 0-1 | | Interno |
| 208 | nro | Elemento | 201 - end | | 0-1 | | Interno |
| 209 | xCpl | Elemento | 201 - end | | 0-1 | | Interno |
| 210 | xBairro | Elemento | 201 - end | | 0-1 | | Interno |
| 211 | explRod | Grupo | 156 - serv | | 0-1 | | Interno |
| 212 | categVeic | Elemento | 211 - explRod | PedCategVeiculo | 0-1 | Categorias de veículos para cobrança | Externo |
| 213 | nEixos | Elemento | 211 - explRod | PednEixos | 0-1 | Número de eixos para fins de cobrança | Externo |
| 214 | rodagem | Elemento | 211 - explRod | PedRodagem | 0-1 | Tipo de rodagem | Externo |
| 215 | sentido | Elemento | 211 - explRod | PedSentido | 0-1 | Orientação de passagem do veículo | Externo |
| 216 | placa | Elemento | 211 - explRod | PedPlaca | 0-1 | Placa do veículo | Externo |
| 217 | codAcessoPed | Elemento | 211 - explRod | PedCodAcesso | 0-1 | Código de acesso gerado automaticamente pelo sistema emissor da concessionária | Externo |
| 218 | codContrato | Elemento | 211 - explRod | PedCodContrato | 0-1 | Código de contrato gerado automaticamente pelo sistema nacional no cadastro da concessionária | Externo |
| 219 | infoCompl | Grupo | 156 - serv | | 0-1 | | Interno |
| 220 | idDocTec | Elemento | 219 - infoCompl | idDocTec | 0-1 | Identificador de Documento de Responsabilidade Técnica: ART, RRT, DRT, Outros. | Externo |
| 221 | docRef | Elemento | 219 - infoCompl | docRef | 0-1 | Chave da nota fiscal referenciada. | Externo |
| 222 | xInfComp | Elemento | 219 - infoCompl | NFSOutrasinformacoes | 0-1 | Outras Informações / observações | Externo |
| 223 | valores | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 224 | vServPrest | Grupo | 223 - valores | | 0-1 | | Interno |
| 225 | vReceb | Elemento | 224 - vServPrest | | 0-1 | | Interno |
| 226 | vServ | Elemento | 224 - vServPrest | ValServicos | 0-1 | Valor do Serviço | Externo |
| 227 | vDescCondIncond | Grupo | 223 - valores | | 0-1 | | Interno |
| 228 | vDescIncond | Elemento | 227 - vDescCondIncond | ValDescIncond | 0-1 | Valor do desconto incondicionado | Externo |
| 229 | vDescCond | Elemento | 227 - vDescCondIncond | ValDescCond | 0-1 | Valor desconto Condicionado | Externo |
| 230 | vDedRed | Grupo | 223 - valores | | 0-1 | | Interno |
| 231 | pDR | Elemento | 230 - vDedRed | ValPercDeducoes | 0-1 | Valor percentual padrão para dedução/redução do valor do serviço | Externo |
| 232 | vDR | Elemento | 230 - vDedRed | ValDeducoes | 0-1 | Valor das Deduções do serviço | Externo |
| 233 | documentos | Grupo | 230 - vDedRed | Ded | 0-1 | Grupo Dedução (controle) | Interno |
| 234 | docDedRed | Grupo | 233 - documentos | | 0-1 | | Interno |
| 235 | chNFSe | Elemento | 234 - docDedRed | DedChAcesNacional | 0-1 | Chave de Acesso da NFS-e (Padrão Nacional) | Externo |
| 236 | chNFe | Elemento | 234 - docDedRed | DedChaveAcesso | 0-1 | Chave de acesso da NF-e de referência | Externo |
| 237 | NFSeMun | Grupo | 234 - docDedRed | | 0-1 | | Interno |
| 238 | cMunNFSeMun | Elemento | 237 - NFSeMun | DedcMunNFSe | 0-1 | Código do município da NFS-e de referência | Externo |
| 239 | nNFSeMun | Elemento | 237 - NFSeMun | | 0-1 | | Interno |
| 240 | cVerifNFSeMun | Elemento | 237 - NFSeMun | DedcVerificaNFSe | 0-1 | Código de verificação da NFS-e de referência | Externo |
| 241 | NFNFS | Grupo | 234 - docDedRed | | 0-1 | | Interno |
| 242 | nNFS | Elemento | 241 - NFNFS | DednNFRef | 0-1 | Número da nota de referência da Dedução | Externo |
| 243 | modNFS | Elemento | 241 - NFNFS | | 0-1 | | Interno |
| 244 | serieNFS | Elemento | 241 - NFNFS | | 0-1 | | Interno |
| 245 | nDocFisc | Elemento | 234 - docDedRed | DedOutroDocFisc | 0-1 | Número de outro documento fiscal | Externo |
| 246 | nDoc | Elemento | 234 - docDedRed | DedOutroDoc | 0-1 | Código de outro documento de referência | Externo |
| 247 | tpDedRed | Elemento | 234 - docDedRed | DedRedTipo | 0-1 | Tipo de dedução/Redução modelo nacional | Externo |
| 248 | xDescOutDed | Elemento | 234 - docDedRed | DedRetxDesc | 0-1 | Descrição da Dedução/Redução | Externo |
| 249 | dtEmiDoc | Elemento | 234 - docDedRed | DedDEmis | 0-1 | Data de emissão da NF-e de referência | Externo |
| 250 | vDedutivelRedutivel | Elemento | 234 - docDedRed | DedvlTotRef | 0-1 | Valor total da nota de referencia de dedução | Externo |
| 251 | vDeducaoReducao | Elemento | 234 - docDedRed | DedValor | 0-1 | Valor da Dedução da lista de deduções | Externo |
| 252 | fornec | Grupo | 234 - docDedRed | | 0-1 | | Interno |
| 253 | CNPJ | Elemento | 252 - fornec | DedCNPJRef | 0-1 | CNPJ do fornecedor ou prestador | Externo |
| 254 | CPF | Elemento | 252 - fornec | DedCPFRef | 0-1 | CPF do fornecedor ou prestador | Externo |
| 255 | NIF | Elemento | 252 - fornec | DedNifFornecedor | 0-1 | NIF do Fornecedor do exterior | Externo |
| 256 | cNaoNIF | Elemento | 252 - fornec | DedForncExSemNIF | 0-1 | Motivo para não informação do NIF do fornecedor | Externo |
| 257 | CAEPF | Elemento | 252 - fornec | DedFornCAEPF | 0-1 | Número do Cadastro de Atividade Econômica da Pessoa Física (CAEPF) | Externo |
| 258 | IM | Elemento | 252 - fornec | DedFornIM | 0-1 | Inscrição Municipal do fornecedor | Externo |
| 259 | xNome | Elemento | 252 - fornec | DedFornNome | 0-1 | Razão Social do fornecedor | Externo |
| 260 | end | Grupo | 252 - fornec | | 0-1 | | Interno |
| 261 | endNac | Grupo | 260 - end | | 0-1 | | Interno |
| 262 | cMun | Elemento | 261 - endNac | DedForncMun | 0-1 | Código IBGE do fornecedor | Externo |
| 263 | CEP | Elemento | 261 - endNac | DedFornCEP | 0-1 | CEP do fornecedor | Externo |
| 264 | endExt | Grupo | 260 - end | | 0-1 | | Interno |
| 265 | cPais | Elemento | 264 - endExt | DedPaisFornecedor | 0-1 | Código do país do Fornecedor do exterior | Externo |
| 266 | cEndPost | Elemento | 264 - endExt | DedFornCEPEx | 0-1 | CEP do fornecedor no exterior | Interno |
| 267 | xCidade | Elemento | 264 - endExt | DedFornxMunEx | 0-1 | Nome do Município do fornecedor no exterior | Interno |
| 268 | xEstProvReg | Elemento | 264 - endExt | DedFornUFEx | 0-1 | UF/Província/região do fornecedor no exterior | Interno |
| 269 | xLgr | Elemento | 260 - end | DedFornLogr | 0-1 | Logradouro do endereço do Fornecedor | Externo |
| 270 | nro | Elemento | 260 - end | DedFornNumero | 0-1 | Número do endereço do Fornecedor | Externo |
| 271 | xCpl | Elemento | 260 - end | DedFornCompl | 0-1 | Complemento do endereço do Fornecedor | Externo |
| 272 | xBairro | Elemento | 260 - end | DedFornBairro | 0-1 | Bairro do endereço do Fornecedor | Externo |
| 273 | fone | Elemento | 252 - fornec | DedFornFone | 0-1 | Telefone do fornecedor com DDD | Externo |
| 274 | email | Elemento | 252 - fornec | DedFornEmail | 0-1 | E-mail do fornecedor | Externo |
| 275 | trib | Grupo | 223 - valores | | 0-1 | | Interno |
| 276 | tribMun | Grupo | 275 - trib | | 0-1 | | Interno |
| 277 | tribISSQN | Elemento | 276 - tribMun | natOp | 1-1 | Natureza Operação / Exigibilidade ISS / CFPS | Externo |
| 278 | cPaisResult | Elemento | 276 - tribMun | PaiPreServico | 0-1 | País de Prestação do Serviço | Externo |
| 279 | BM | Grupo | 276 - tribMun | | 0-1 | | Interno |
| 280 | tpBM | Elemento | 279 - BM | benefIdNac | 0-1 | Tipo do Benefício Municipal | Externo |
| 281 | nBM | Elemento | 279 - BM | benefNumero | 0-1 | Identificação da Lei parametrizada pelo município que define o benefício no sistema nacional | Externo |
| 282 | vRedBCBM | Elemento | 279 - BM | ValRedBenef | 0-1 | Valor monetário para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| 283 | pRedBCBM | Elemento | 279 - BM | PercRedBenef | 0-1 | Valor percentual para redução da base de cálculo do ISSQN devido a um Benefício Municipal (BM) | Externo |
| 284 | exigSusp | Grupo | 276 - tribMun | | 0-1 | | Interno |
| 285 | tpSusp | Elemento | 284 - exigSusp | tpSusp | 0-1 | Opção para Exigibilidade Suspensa | Externo |
| 286 | nProcesso | Elemento | 284 - exigSusp | NumProcesso | 0-1 | Número do Processo Administrativo | Externo |
| 287 | tpImunidade | Elemento | 276 - tribMun | tpImunidade | 0-1 | Identificação da Imunidade do ISSQN – somente para o caso de Imunidade | Externo |
| 288 | tpRetISSQN | Elemento | 276 - tribMun | ISSRetido | 0-1 | É ISS Retido? | Externo |
| 289 | pAliq | Elemento | 276 - tribMun | ValAliqISS | 0-1 | Alíquota do ISS | Externo |
| 290 | tribFed | Grupo | 275 - trib | | 0-1 | | Interno |
| 291 | piscofins | Grupo | 290 - tribFed | | 0-1 | | Interno |
| 292 | CST | Elemento | 291 - piscofins | CST | 0-1 | Código de Situação Tributária do PIS/COFINS (CST) | Externo |
| 293 | vBCPisCofins | Elemento | 291 - piscofins | ValBCPIS | 0-1 | Valor base de cálculo do PIS | Externo |
| 294 | pAliqPis | Elemento | 291 - piscofins | ValAliqPIS | 0-1 | Alíquota do PIS em formato decimal | Externo |
| 295 | pAliqCofins | Elemento | 291 - piscofins | ValAliqCOFINS | 0-1 | Alíquota do COFINS em formato decimal | Externo |
| 296 | vPis | Elemento | 291 - piscofins | ValPIS | 0-1 | Valor do PIS do serviço | Externo |
| 297 | vCofins | Elemento | 291 - piscofins | ValCOFINS | 0-1 | Valor do COFINS | Externo |
| 298 | tpRetPisCofins | Elemento | 291 - piscofins | tpRetPISCofins | 0-1 | Tipo de Retenção PIS/COFINS e CSLL | Externo |
| 299 | vRetCP | Elemento | 290 - tribFed | ValINSS | 0-1 | Valor do INSS do serviço | Externo |
| 300 | vRetIRRF | Elemento | 290 - tribFed | ValIR | 0-1 | Valor do IRRF do serviço | Externo |
| 301 | vRetCSLL | Elemento | 290 - tribFed | ValCSLL | 0-1 | Valor do CSLL | Externo |
| 302 | totTrib | Grupo | 275 - trib | | 1-1 | | Interno |
| 303 | vTotTrib | Grupo | 302 - totTrib | | 0-1 | | Interno |
| 304 | vTotTribFed | Elemento | 303 - vTotTrib | ValRetFederais | 0-1 | Valor total das retenções federais | Externo |
| 305 | vTotTribEst | Elemento | 303 - vTotTrib | ValRetEstaduais | 0-1 | Valor monetário total aproximado dos tributos estaduais | Externo |
| 306 | vTotTribMun | Elemento | 303 - vTotTrib | ValRetMunicipais | 0-1 | Valor monetário total aproximado dos tributos municipais | Externo |
| 307 | pTotTrib | Grupo | 302 - totTrib | | 0-1 | | Interno |
| 308 | pTotTribFed | Elemento | 307 - pTotTrib | ValAliqRetFederais | 0-1 | Valor percentual total aproximado dos tributos federais | Externo |
| 309 | pTotTribEst | Elemento | 307 - pTotTrib | ValAliqRetEstaduais | 0-1 | Valor percentual total aproximado dos tributos estaduais | Externo |
| 310 | pTotTribMun | Elemento | 307 - pTotTrib | ValAliqRetMunicipais | 0-1 | Valor percentual total aproximado dos tributos municipais | Externo |
| 311 | indTotTrib | Elemento | 302 - totTrib | indTotTrib | 0-1 | Indicador de informação de valor total de tributos. | Interno |
| 312 | pTotTribSN | Elemento | 302 - totTrib | ValAproxTribAliqSN | 0-1 | Valor percentual aproximado do total dos tributos da alíquota do Simples Nacional (%) | Externo |
| 313 | IBSCBS | Grupo | 67 - infDPS | | 0-1 | | Interno |
| 314 | finNFSe | Elemento | 313 - IBSCBS | finNFSe | 0-1 | Indicador da finalidade da emissão de NFS-e | Externo |
| 315 | indFinal | Elemento | 313 - IBSCBS | indFinal | 0-1 | Indica operação de uso ou consumo pessoal. 0=Não 1=Sim | Externo |
| 316 | cIndOp | Elemento | 313 - IBSCBS | cIndOp | 0-1 | Código indicador da operação de fornecimento, conforme tabela "código indicador de operação" | Externo |
| 317 | indDest | Elemento | 313 - IBSCBS | indDest | 0-1 | A respeito do Destinatário dos serviços: 0 – O tomador na NFS-e é o adquirente e o destinatário | Externo |
| 318 | valores | Grupo | 313 - IBSCBS | | 0-1 | | Interno |
| 319 | trib | Grupo | 318 - valores | | 0-1 | | Interno |
| 320 | gIBSCBS | Grupo | 319 - trib | | 0-1 | | Interno |
| 321 | CST | Elemento | 320 - gIBSCBS | CSTIBSCBS | 0-1 | Código de Situação Tributária do IBS e da CBS | Externo |
| 322 | cClassTrib | Elemento | 320 - gIBSCBS | cClassTrib | 0-1 | Descrição do Código de Classificação Tributária do IBS/CBS | Externo |