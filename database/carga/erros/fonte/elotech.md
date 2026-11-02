# Rejeições: Elotech

> Fonte: Elotech Informática e Sistemas Ltda, "Lista de Erros e Alertas gerados pelo Web Service", versão 2.0.0 (23/08/2012). Arquivo original em `_origem/Padrões/Elotech/document.pdf`.

Códigos `A` são alertas (a NFS-e é emitida mesmo assim) e códigos `E` são erros. O documento não classifica a série `S`; ela aparece aqui como Erro.

## Lista de erros e alertas

| Código | Tipo | Mensagem | Motivo / Solução |
| :--- | :--- | :--- | :--- |
| A005 | Alerta | Endereço do tomador do serviço difere do constante na base de dados do município. | A NFS-e foi emitida considerando o endereço do tomador do serviço informado no RPS. |
| A007 | Alerta | A inscrição municipal do tomador do serviço está paralisada ou cancelada ou desativada ou baixada. | A inscrição municipal do tomador do serviço informada no RPS está paralisada ou cancelada ou desativada ou baixada no cadastro municipal. A NFS-e foi emitida considerando que o tomador do serviço não está inscrito na Prefeitura. |
| E002 | Erro | Data de competência superior a de emissão do RPS. | Informe uma data de competência inferior ou igual à de emissão do RPS. |
| E010 | Erro | RPS já informado. | Para essa Inscrição Municipal/CNPJ já existe um RPS informado com o mesmo número, série e tipo. |
| E016 | Erro | A data da emissão do RPS não poderá ser superior à data de hoje. | Informe uma data de emissão de RPS válida. |
| E017 | Erro | A data da emissão do RPS não poderá ser inferior à data de habilitação do prestador do serviço para emissão da NFS-e. | A geração de NFS-e só é permitida para competência posterior à data de habilitação da empresa. |
| E018 | Erro | O valor dos serviços deverá ser superior a R$ 0,00 (zero). | Não é permitido envio de valor de serviços igual à zero. |
| E019 | Erro | O valor das deduções deverá ser inferior ou igual ao valor dos serviços. | Não é permitido valor de dedução superior ao valor de serviços. |
| E020 | Erro | O valor das deduções deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de dedução inferior à zero (negativo). |
| E021 | Erro | O valor dos descontos deverá ser inferior ou igual ao valor dos serviços. | Não é permitido valor de desconto superior ao valor de serviços. |
| E022 | Erro | O valor dos descontos deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de desconto inferior à zero (negativo). |
| E023 | Erro | O valor do PIS deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior à zero (negativo). |
| E024 | Erro | O valor da COFINS deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior à zero (negativo). |
| E025 | Erro | O valor do INSS deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior à zero (negativo). |
| E026 | Erro | O valor do IR deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior à zero (negativo). |
| E027 | Erro | O valor da CSLL deverá ser superior ou igual a R$ 0,00 (zero). | Não é permitido valor de retenção inferior à zero (negativo). |
| E028 | Erro | Item da lista de serviço incompatível com opção SIMPLES Nacional. | Consulte a legislação vigente para saber se o item da lista de serviço informado permite a opção pelo simples nacional. |
| E029 | Erro | O código de serviço prestado não permite retenção de ISSQN. | O tipo do serviço prestado não permite a retenção do ISS devido. Altere o indicador de retenção do ISSQN devido para 2 - sem retenção. |
| E030 | Erro | Item da lista de serviço inexistente. | Consulte a legislação vigente para saber o item da lista de serviço que deverá ser informado. |
| E032 | Erro | Código CNAE incompatível com opção SIMPLES Nacional. | Consulte a legislação vigente para saber se o código informado permite a opção pelo simples nacional. |
| E033 | Erro | Código CNAE inexistente. | Consulte a legislação vigente para saber o código CNAE que deverá ser informado. |
| E039 | Erro | Apenas empresas inscritas neste município podem efetuar retenção de ISSQN. | O tomador do serviço não foi encontrado na base de dados do município, não sendo permitida a retenção. Acerte o CNPJ e/ou Inscrição Municipal ou altere o campo ISSQN Retido para 2 (Sem retenção de ISSQN). |
| E047 | Erro | CPF/CNPJ do tomador do serviço incorreto. | Informe corretamente o CPF/CNPJ do tomador do serviço. |
| E052 | Erro | O tomador do serviço informado é o próprio prestador do serviço. | Na emissão da NFS-e não é permitido que o prestador do serviço seja igual ao tomador do serviço. |
| E053 | Erro | O campo Inscrição Municipal do tomador do serviço só deverá ser preenchido para tomadores estabelecidos neste município. | Para tomador de serviço estabelecido fora deste município não deve ser preenchido o campo Inscrição municipal. |
| E065 | Erro | Inscrição Municipal do intermediário do serviço não esta vinculada ao CNPJ/CPF informado. | A inscrição municipal informada não está vinculada ao intermediário do serviço identificado pelo CNPJ ou CPF. |
| E069 | Erro | Quantidade de RPS incorreta. | A quantidade de RPS informada difere da quantidade de RPS do lote. |
| E078 | Erro | Número da NFS-e inexistente na base de dados para o prestador do serviço pesquisado. | Informe corretamente o número da NFS-e. |
| E087 | Erro | Número de lote inexistente na base de dados. | Confira se o lote foi enviado e informe o número correto. |
| E089 | Erro | Não existe na base de dados uma NFS-e emitida para o número de RPS informado. | Informe corretamente o número do RPS. |
| E091 | Erro | Esse RPS não consta em nossa base de dados. | Solicite do prestador do serviço a emissão da NFS-e. |
| E092 | Erro | RPS ainda não convertido em NFS-e. | Efetue nova consulta mais tarde. |
| E099 | Erro | Valor da retenção deverá ser inferior ou igual ao valor dos serviços. | Não é permitido valor de retenção superior ao valor de serviços. |
| E133 | Erro | Data final posterior a data de hoje. | Informe uma data final igual ou anterior a data de hoje. |
| E134 | Erro | Data final anterior à data inicial. | Informe uma data final igual ou superior a data inicial da pesquisa. |
| E138 | Erro | Usuário não autorizado a realizar o serviço. | Informe os dados de um usuário autorizado a executar o serviço. |
| E167 | Erro | Município informado não pertence à Unidade Federativa. | Acerte o município ou a Unidade Federativa. |
| E168 | Erro | CEP do tomador do serviço informado indevidamente. | Não informe o CEP do tomador do serviço quando ele for domiciliado no exterior ou não for identificado. |
| E169 | Erro | CNPJ/CPF do tomador do serviço informado indevidamente. | Não informe o CNPJ/CPF do tomador do serviço quando ele for domiciliado no exterior ou não for identificado. |
| E175 | Erro | O somatório das deduções e do desconto incondicionado está maior que o valor dos serviços | O preço do serviço deve ser maior ou igual à soma das deduções com o desconto incondicionado. |
| E176 | Erro | O somatório dos descontos, retenções federais e ISSQN retido na fonte estão maiores que o valor dos serviços. | O preço do serviço deve ser maior ou igual à soma dos tributos federais, o ISSQN devido e os descontos. |
| E177 | Erro | Pessoa Física não pode efetuar a retenção do ISSQN | Tomador de serviço pessoa física não pode reter o ISSQN. Altere o indicador de retenção do ISSQN para 2 - sem retenção. |
| E178 | Erro | Lote aguardando processamento. | O lote de RPS correspondente ao protocolo informado foi recebido pela Prefeitura, mas ainda não foi processado. |
| E179 | Erro | Erro na autenticação. | O usuário correspondente ao documento utilizado para autenticação não possui autorização para realizar o serviço para a empresa parametrizada via CNPJ e inscrição municipal do arquivo. |
| E191 | Erro | Tipo do RPS inválido. | Neste município utilizar somente o Tipo 1 - RPS |
| E206 | Erro | Cancelamento da NFS-e por erro na emissão não pode ser feito por esse serviço. | Utilize o serviço de substituição de NFS-e. |
| E208 | Erro | Cancelamento de NFS-e com tomador do serviço não informado não pode ser feito por esse serviço. | O cancelamento de uma NFS-e com tomador do serviço não informado deverá ser feito por meio de processo administrativo aberto na repartição fazendária. |
| E213 | Erro | Código de cancelamento informado de uso exclusivo da Prefeitura | Código de motivo de cancelamento \<3-Erro de assinatura\> e \<5-Erro de processamento\> é de uso exclusivo da Prefeitura. |
| E216 | Erro | Não é permitida a substituição de RPS. | O campo RpsSubstituido não pode ser preenchido para este município. |
| E220 | Erro | Valor do ISSQN informado indevidamente. | O valor do ISSQN será calculado pela Prefeitura e não deve ser informado pelo contribuinte. |
| E221 | Erro | Alíquota informada indevidamente. | A alíquota do ISSQN só deve ser informada quando: o ISSQN for devido a outro município ou o prestador do serviço for optante pelo Simples Nacional e houver retenção do ISSQN. Em outras situações a alíquota a ser aplicada será determinada pela Prefeitura. |
| E227 | Erro | Alíquota Serviços fora do intervalo de 2% e 5% | Quando o local da incidência for diferente deste município esse campo deve ser maior ou igual a 2% e menor ou igual a 5%. |
| E228 | Erro | Para esta prestação de serviços a alíquota do ISSQN é obrigatória. | Informe um valor percentual da alíquota de ISSQN, que deve ser maior ou igual a 2% e menor ou igual a 5%. |
| E229 | Erro | A Inscrição Municipal do tomador do serviço não existe na base de dados da Prefeitura. | Informe corretamente a Inscrição Municipal do tomador do serviço. |
| E233 | Erro | Lote já processado. | Atribua um novo número ao lote de RPS, caso os RPS nele contidos ainda não tenham sidos processados. |
| E241 | Erro | Retenções de Tributos Federais só podem ser efetuadas por tomador de serviços pessoa jurídica. | Preencher os campos de retenções federais com zeros. |
| E243 | Erro | Esta NFS-e não deverá ter o ISSQN Retido pelo tomador do serviço. | Consulte a legislação vigente para saber as regras de retenção do ISSQN na fonte e selecione ISS Retido = 2 (NÃO). |
| E249 | Erro | Código do município de incidência do ISSQN incorreto. | Contribuintes com regime especial de tributação "Microempresário Individual" (MEI) não pode emitir NFS-e com município de incidência diferente do município gerador. |
| E251 | Erro | Para o regime especial \<Estimativa\> não é permitida a emissão de NFS-e | Consulte a legislação aplicável e verifique as situações de emissão de NFS-e. |
| E252 | Erro | Para o regime especial \<Cooperativa\> não é permitida a emissão de NFS-e | Consulte a legislação aplicável e verifique as situações de emissão de NFS-e. |
| E253 | Erro | A data da nova competência não deve ser inferior à competência de criação da NFS-e. | Altere a data da competência. |
| E255 | Erro | O status atual desta nota não permite alteração | A NFS-e não pode ser alterada. |
| E262 | Erro | Inscrição municipal paralisada, cancelada ou desativada. | Prestador do serviço com inscrição municipal paralisada, cancelada ou desativada não pode utilizar NFS-e. |
| E273 | Erro | PIS retido incorreto. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E274 | Erro | COFINS retida incorreta. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E275 | Erro | Contribuição ao INSS retida incorreta. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E276 | Erro | Imposto de renda retido incorreto. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E277 | Erro | CSLL retida incorreta. | Apenas pessoas jurídicas podem reter tributos. Se o tomador do serviço é pessoa física, o valor do tributo retido deve ser igual a zero. |
| E278 | Erro | Indicador de ISSQN retido incorreto. | Empresa com inscrição municipal paralisada, cancelada ou desativada não pode reter ISSQN. Altere o indicador de retenção do ISSQN para 2 - sem retenção. |
| E282 | Erro | Responsável/Retentor informado indevido. | O campo Responsável/Retentor somente deverá ser preenchido quando o ISSQN for retido na fonte. |
| E285 | Erro | País da prestação do serviço não informado. | Quando a exigibilidade do ISSQN for \<Exportação\> informar o país da prestação do serviço. |
| E287 | Erro | País da prestação do serviço indevido | O código do país da prestação do serviço somente deverá ser informado quando a exigibilidade do ISSQN for \<Exportação\>. |
| E290 | Erro | País do tomador do serviço não informado. | Quando a exigibilidade do ISSQN for \<Exportação\> informar o país do tomador do serviço. |
| E292 | Erro | País do tomador do serviço indevido | O código do país do tomador do serviço somente deverá ser informado quando o município for igual a \<9999999\>. |
| E293 | Erro | Intermediário do serviço não informado. | Quando o campo Responsável/Retenção for igual a \<2\> a qualificação do intermediário do serviço é informação obrigatória. |
| E294 | Erro | CPF do intermediário do serviço informado indevidamente. | O intermediário do serviço deve ser pessoa jurídica. |
| E296 | Erro | CNPJ/CPF do intermediário do serviço não informado. | Informe o CNPJ/CPF do intermediário do serviço. |
| E297 | Erro | CNPJ/CPF do intermediário do serviço não consta da base de dados | O intermediário do serviço não foi encontrado na base de dados do município, |
| E298 | Erro | CNPJ/CPF do intermediário do serviço incorreto | Informe corretamente o CNPJ/CPF do intermediário do serviço. |
| E299 | Erro | Inscrição municipal do intermediário do serviço não informada. | Quando o CPF/CNPJ do intermediário do serviço estiver vinculado a mais de uma inscrição municipal esta informação é obrigatória. |
| E300 | Erro | Inscrição municipal do intermediário do serviço não consta da base de dados | O intermediário do serviço não foi encontrado na base de dados do município, |
| E302 | Erro | Código da obra não informado. | Para o serviço informado o código da obra é informação obrigatória. |
| E303 | Erro | Código da obra informada indevidamente. | O serviço informado não permite a informação do código da obra. |
| E304 | Erro | ART informada indevidamente. | O serviço informado não permite indicação de ART. |
| E305 | Erro | ART não informada. | Para o serviço informado a ART - Anotação de Responsabilidade Técnica - é informação obrigatória. |
| E311 | Erro | Código do município de incidência do ISSQN não informado | A informação do código do município da incidência do ISSQN é obrigatória quando \<Exigibilidade do ISS\> for tipo \<1 - Exigível\>, \<6 - Exigibilidade Suspensa por Decisão Judicial\> ou \<7 - Exigibilidade Suspensa por Processo Administrativo\>. Consulte a Tabela de Municípios do IBGE para preenchimento do campo. |
| E313 | Erro | Número do processo informado indevidamente | Somente deverá ser informado o número do processo quando \<Exigibilidade do ISS\> for tipo \<6 - Exigibilidade Suspensa por Decisão Judicial\> ou \<7 - Exigibilidade Suspensa por Processo Administrativo\>. |
| E314 | Erro | Número do processo não informado. | A informação do número do processo é obrigatória quando \<Exigibilidade do ISS\> for tipo \<6 - Exigibilidade Suspensa por Decisão Judicial\> ou \<7 - Exigibilidade Suspensa por Processo Administrativo\>. |
| E327 | Erro | O regime de tributação informado difere do registrado na Prefeitura. | Procure a Prefeitura para atualizar o regime de tributação no cadastro do contribuinte. |
| E329 | Erro | Apenas serviços tributados podem sofrer retenção de ISSQN. | A retenção na fonte é permitida somente se o campo \<Exigibilidade do ISS\> for igual a \<1 - Exigível\>. |
| E330 | Erro | Contribuintes enquadrados como MEI do simples nacional não podem sofrer retenção de ISSQN. | Empresas enquadradas como MEI do simples nacional não devem sofrer retenção na fonte do ISSQN. |
| E331 | Erro | Contribuintes enquadrados como Microempresa Municipal não podem sofrer retenção de ISSQN. | Empresas enquadradas como Microempresa Municipal não devem sofrer retenção na fonte do ISSQN. |
| E332 | Erro | Contribuintes enquadrados como Estimativa não podem sofrer retenção de ISSQN. | Empresas enquadradas como Estimativa não devem sofrer retenção na fonte do ISSQN. |
| E333 | Erro | Contribuintes enquadrados como Sociedade de Profissionais não podem sofrer retenção de ISSQN. | Empresas enquadradas como Sociedade de Profissionais não devem sofrer retenção na fonte do ISSQN. |
| E334 | Erro | Contribuintes enquadrados como Incentivador Fiscal não podem sofrer retenção de ISSQN. | Empresas enquadradas como Incentivador Fiscal não devem sofrer retenção na fonte do ISSQN. |
| E335 | Erro | Empresas cadastradas com CPF não podem sofrer retenção de ISSQN. | Empresas cadastradas com CPF não devem sofrer retenção na fonte do ISSQN. |
| E336 | Erro | Inscrição municipal do tomador do serviço indevida. | Não informe o campo inscrição municipal do tomador do serviço quando este for do exterior |
| E341 | Erro | Alíquota do ISSQN não informado. | Quando o ISSQN é devido a outro município, alíquota deve ser informada pelo prestador do serviço. |
| E342 | Erro | Informação de Incentivador Fiscal não confere com o cadastro da prefeitura. | Acerte o campo Incentivador Fiscal |
| E344 | Erro | Item da lista de serviço informado para a operação não permite tributação fora do município | Consulte a legislação vigente para saber se o item informado permite a tributação fora do município. |
| S001 | Erro | Nenhum resultado encontrado | Alterar o filtro. |
| S002 | Erro | Requerente inválido | Informe um requerente válido. |
| S003 | Erro | Somente serviço de homologação | Para este requerente somente o ambiente de homologação está habilitado, ative o flag de homologação. |
| S004 | Erro | Código do Município %1$d informado para %2$s não existe na Tabela de Municípios do IBGE. Informe um novo código existente. | Informe o código do município conforme Tabela de Municípios do IBGE. |
| S005 | Erro | Exigibilidade não encontrada: | Informe um código de exigibilidade válido. |
| S006 | Erro | Regime fiscal não encontrado: | Informe um código de regime fiscal válido. |
| S007 | Erro | Nota fiscal com itens de alíquotas diferentes | Informe apenas itens com a mesma alíquota. |
| S008 | Erro | Valor deduções não deve ser superior ao valor dos serviços | Corrija o valor das deduções ou dos serviços |
| S009 | Erro | Valor deduções não deve ser superior a %1$.2f%% do valor dos serviços. | Corrija o valor das deduções ou dos serviços |
| S010 | Erro | Valor deduções não confere com a soma do valor líquido dos itens de dedução | Corrija o valor das deduções ou itens de dedução |
| S011 | Erro | Valor NFSE deve ser superior a R$ 0,00 (zero) | Por favor, verifique os valores da NFSe, a soma dos valores líquidos dos itens, menos os descontos e as deduções estão gerando um valor negativo. |
| S012 | Erro | O valor liquido do item `\"%1$d\"` não corresponde com o calculo de quantidade * valor unitário - desconto | Corrija o valor liquido do item ou um de seus componentes. |
| S013 | Erro | O valor do desconto incondicionado não confere com a soma dos descontos lançados nos itens | Corrija o valor do desconto incondicionado ou os valores de desconto nos itens. |
| S014 | Erro | Código CNAE incompatível com opção Micro Empreendedor Individual. | Consulte a legislação vigente para saber se o código informado permite a opção pelo micro empreendedor individual. |
| S015 | Erro | Código lista serviços incompatível com opção Micro Empreendedor Individual. | Consulte a legislação vigente para saber se o código informado permite a opção pelo micro empreendedor individual. |
| S016 | Erro | País não encontrado: | Informe o código do país conforme Tabela de Países do BACEN. |
| S017 | Erro | Requerente não credenciado. | Informe um requerente credenciado. |
| S018 | Erro | Inscrição Municipal Incorreta. | Inscrição Municipal do prestador está diferente da Inscrição do Requerente os dois devem ser iguais. |
| S019 | Erro | O serviço %1$s é divergente do outros itens. | Item da Lista de Serviço divergente em algum dos itens do RPS. |
| S020 | Erro | O CNAE %1$s é divergente do outros itens. | O CNAE está divergente em algum dos itens do RPS. |
| S020 | Erro | CNAE Informado Incompatível. | O CNAE está divergente em algum dos itens do RPS. |
| S021 | Erro | Município de incidência não permitido. | Altere o município de incidência para o município válido. |
| S022 | Erro | Dedução não permitida. | O item da Lista de serviços não permite dedução. |
| S023 | Erro | Valor total não corresponde a soma dos itens. | Corrija o valor liquido dos itens. |
| S025 | Erro | Município de incidência não permitido. | O item da lista de serviços não permite ISSQN retido para o município de incidência indicado. |
| S027 | Erro | Serviço não habilitado para utilização. | Por favor, entre em contato com o setor de tributação. |
| S999 | Erro | Erro Interno | Erro inesperado entre em contato com o suporte técnico. |
