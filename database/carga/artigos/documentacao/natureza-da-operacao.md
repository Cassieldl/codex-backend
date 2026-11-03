# Natureza da operação (natOp)

> [!RESUMO]
> A natureza da operação diz como o serviço é tributado: no município, fora dele, isento, imune, exportação etc.
> Você envia sempre o código da **tabela unificada do InvoiCy**, e o InvoiCy converte para o código de cada padrão.
> Nem todo código vale em todo padrão: confira a tabela do padrão do município antes de configurar o cliente.

## O que é

Indica a finalidade e a tributação do serviço: tributado no município ou fora, isento, imune, exportação etc.

## Tabela unificada do InvoiCy

Cada prefeitura usa códigos próprios. O InvoiCy usa uma **tabela única** no layout de recepção e converte para o código de cada padrão. Assim, "tributado no município do prestador" tem sempre o mesmo valor para quem emite.

> [!ATENCAO]
> **Nem toda opção é aceita por todo padrão.** Antes de configurar o cliente, consulte quais valores o município aceita.

A conversão por padrão está na aba *Natureza de Operação* do layout oficial (`_origem/Padrões/Layout InvoiCy/Layout_Recepcao_NFSe_Reforma_Tributaria - versão 9.xlsx`).

## Como consultar

### Pela tela

*Painel de Controle → NFS-e → Natureza da Operação*. A tela mostra código InvoiCy, prefeitura, descrição e valor na prefeitura, ou avisa se o município não tem naturezas.

![Grupo NFS-e do Painel de Controle com o ícone Natureza da Operação destacado](../../assets/imagens/natureza-da-operacao-natop/natureza-da-operacao-no-painel.png "Natureza da Operação no Painel de Controle")

![Tela Natureza da Operação com o município selecionado e a tabela Código no InvoiCy, Prefeitura, Descrição e Valor na Prefeitura](../../assets/imagens/natureza-da-operacao-natop/municipio-sem-natureza-da-operacao.png "Consulta por município")

### Pelo webservice (SOAP)

- WSDL homologação: <https://homolog.invoicy.com.br/arecepcao.aspx?wsdl>
- WSDL produção: <https://app.invoicy.com.br/arecepcao.aspx?wsdl>
- Exemplo de envio: <https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Consulta%20Naturezas%20de%20Opera%C3%A7%C3%A3o%20NFSe/Exemplos%20XML/Consultar%20naturezas%20envio.xml>
- Informe **CNPJ** e **tipo de ambiente**. `IMEmissor` é opcional, só para empresas com o mesmo CNPJ em mais de um município.

## Cuidados

- Só funciona para CNPJ **já cadastrado** no InvoiCy.
- Se o município **trocar de padrão**, as naturezas podem mudar.

> [!IMPORTANTE]
> Use a consulta **só para atualizar o cadastro do cliente**, nunca a cada emissão. Há limite de chamadas por hora e a empresa pode ser **bloqueada**.

## Tabelas por padrão

| Padrão | Tabela |
| :--- | :--- |
| Barueri | Sem tabela: o layout é em arquivo texto. |
| Betha 2.0 (legado, ABRASF) | O Betha atual usa o leiaute nacional: use a [tabela do Padrão Nacional](#padrão-nacional-e-provedores-de-leiaute-nacional). |
| Caucaia WS | [Caucaia WS](#caucaia-ws) |
| CENTI 2 | [CENTI 2](#centi-2) |
| CITTA | [CITTA](#citta) |
| CONAM (e-transparência) | [CONAM](#conam-e-transparência) |
| e-Governe ISS | [e-Governe ISS](#e-governe-iss) |
| Elotech (Tributos Web 2.03) | [Elotech](#tributos-web---elotech-203) |
| Equiplano | [Equiplano](#equiplano) |
| GISS | [GISS](#giss) |
| GLC Consultoria WS (SIGISSWEB) | [GLC](#glc-consultoria-ws-sigissweb) |
| Governo Digital | [Governo Digital](#governo-digital) |
| H2M (HM2 Soluções WS) | [H2M](#h2m-hm2-soluções-ws) |
| INFISC | [INFISC](#infisc) |
| IPM | Tabela natOp × IPM em [ipm.md](../../03-rejeicoes/ipm.md#situação-tributária-natop-invoicy--ipm) |
| Nota Salvador | [Nota Salvador](#nota-salvador) |
| Padrão Nacional | [Padrão Nacional](#padrão-nacional-e-provedores-de-leiaute-nacional) |
| RLZ ABRASF (cidadão online) | [RLZ ABRASF](#rlz-abrasf-cidadão-online) |
| Sigcorp ABRASF | [Sigcorp ABRASF](#sigcorp-abrasf) |
| SMARAPDSil WS2 | [SMARAPDSil WS2](#smarapdsil-ws2) |
| TINUS WS | [TINUS WS](#tinus-ws) |
| TIPLAN 2.0 (layout próprio, não nacional) | [TIPLAN 2.0](#tiplan-20) |
| Tributos Municipais WS | [Tributos Municipais WS](#tributos-municipais-ws) |
| WebISS 2.0 | [WebISS 2.0](#webiss-20) |

## Padrão Nacional e provedores de leiaute nacional

Conversão do código InvoiCy para o valor enviado em `tribISSQN`, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Operação tributável | 1 |
| 4 | Imunidade | 2 |
| 7 | Operação tributável | 1 |
| 8 | Não incidência | 4 |
| 9 | Exportação de serviço | 3 |

> [!IMPORTANTE]
> No Padrão Nacional, `natOp = 4` (imunidade) exige `tpImunidade = 2`.

Códigos fora dessa tabela não têm conversão no leiaute nacional.

Usam essa tabela o **Emissor Nacional** e os provedores que recebem a DPS no leiaute nacional (De/Para com `tribISSQN` no grupo `tribMun`): Betha, Coplan, DSFNET, Dueto, E&L, Fiorilli, GESPAM, ISSNET, João Pessoa, RLZ, Sigcorp e SMARAPDISIL. Nos que também têm versão ABRASF (RLZ, Sigcorp e SMARAPDISIL), a versão ABRASF segue a tabela própria, nas seções abaixo.

**Critério:** é "nacional" o layout cujos campos têm nomes muito parecidos com os do Emissor Nacional (às vezes com um campo ou outro diferente). O que foge disso, inclusive o ABRASF, é layout próprio, com tabela própria (ex.: Tributos Municipais ES, GISS, Elotech).

![Tela Natureza da Operação: NFS-e Nacional com os códigos 1, 4, 7, 8 e 9 e o valor na prefeitura de cada um](../../assets/imagens/emissor-nacional/natureza-da-operacao-no-padrao-nacional.png "Natureza da operação no Padrão Nacional")

## Tabelas por provedor

Cada tabela mostra o código que você envia ao InvoiCy e o valor que o InvoiCy envia à prefeitura. Abra a do seu padrão.

### Caucaia WS

> [!DETALHES] Tabela do Caucaia WS (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributação no município | 1 |
> | 2 | Tributação fora do município | 2 |
> | 3 | Isenção | 3 |
> | 4 | Imune | 4 |
> | 5 | Exigibilidade suspensa por decisão judicial | 5 |
> | 6 | Exigibilidade suspensa por procedimento administrativo | 6 |

### CENTI 2

> [!DETALHES] Tabela do CENTI 2
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributação no município | 1 |
> | 2 | Tributação fora do município | 2 |
> | 3 | Isenção | 3 |
> | 4 | Imune | 4 |
> | 5 | Exigibilidade suspensa por decisão judicial | 5 |
> | 6 | Exigibilidade suspensa por processo administrativo | 6 |
>
> ![Tabela de natureza da operação do CENTI 2 com as colunas Valor a ser informado, Informação trazida pela prefeitura e Descrição](../../assets/imagens/centi/natureza-da-operacao-centi.jpg "Print original do CENTI 2")

### CITTA

> [!DETALHES] Tabela do CITTA
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 3 | Isenção | 3 |
> | 9 | Exportação | 4 |
> | 4 | Imunidade | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por processo administrativo | 7 |
>
> ![Tabela de natureza da operação do CITTA com as colunas Código Tabela Unificada, Valor na prefeitura e Descrição](../../assets/imagens/citta/natureza-da-operacao-citta.png "Print original do CITTA")

### CONAM (e-transparência)

> [!DETALHES] Tabela do CONAM (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributado | 1 |
> | 3 | Isenção / Imunidade | 2 |
> | 5 | Suspensão | 3 |
> | 6 | Suspensão | 3 |
> | 7 | Tributado | 1 |
> | 10 | ISS Fixo | 5 |
> | 12 | Simples Nacional | 4 |
> | 13 | Isenção parcial | 6 |

### e-Governe ISS

O print traz só o código e a descrição, sem a coluna de valor na prefeitura.

> [!DETALHES] Tabela do e-Governe ISS
> | Código | Descrição |
> | :--- | :--- |
> | 1 | Tributação no município |
> | 2 | Tributação fora do município |
> | 3 | Isenção |
> | 4 | Imune |
> | 5 | Exigibilidade suspensa por decisão judicial |
> | 6 | Exigibilidade suspensa por procedimento administrativo |
>
> ![Tabela de natureza da operação do e-Governe ISS com as colunas Código e InvoiCy NFS-e](../../assets/imagens/e-governe-iss/natureza-da-operacao-e-governe.jpg "Print original do e-Governe ISS")

### Tributos Web - Elotech 2.03

> [!DETALHES] Tabela do Elotech (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 3 | Isenção | 3 |
> | 4 | Imunidade | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por procedimento administrativo | 7 |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 9 | Exportação | 3 |

### Equiplano

> [!DETALHES] Tabela do Equiplano (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributação no município | 1 |
> | 2 | Tributação fora do município | 1 |
> | 3 | Isenção | 3 |
> | 4 | Imune | 3 |
> | 5 | Exigibilidade suspensa por decisão judicial | 4 |
> | 7 | Tributação no município | 1 |

### GISS

> [!DETALHES] Tabela do GISS (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributação no município | 1 |
> | 3 | Isenção | 3 |
> | 4 | Imunidade | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por processo administrativo | 7 |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 9 | Exportação | 4 |
> | 15 | Exigível | 1 |

### GLC Consultoria WS (SIGISSWEB)

> [!DETALHES] Tabela do GLC (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Incidência IBS/CBS | 0 |
> | 7 | Incidência IBS/CBS | 0 |
> | 9 | Exportação | 1 |

### Governo Digital

O print traz só o código e a descrição, sem a coluna de valor na prefeitura.

> [!DETALHES] Tabela do Governo Digital
> | Código | Descrição |
> | :--- | :--- |
> | 3 | Isenção |
> | 4 | Imune |
> | 5 | Exigibilidade suspensa por decisão judicial |
> | 6 | Exigibilidade suspensa por procedimento administrativo |
> | 7 | Exigível |
> | 8 | Não incidência |
> | 9 | Exportação |
>
> ![Tabela de natureza da operação do Governo Digital com as colunas Código e Descrição](../../assets/imagens/governo-digital/natureza-da-operacao-governo-digital.png "Print original do Governo Digital")

### H2M (HM2 Soluções WS)

> [!DETALHES] Tabela do H2M (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributação no município | 1 |
> | 2 | Tributação fora do município | 2 |
> | 3 | Isenção | 3 |
> | 4 | Imune | 4 |
> | 5 | Exigibilidade suspensa por decisão judicial | 5 |
> | 6 | Exigibilidade suspensa por procedimento administrativo | 6 |

### INFISC

> [!DETALHES] Tabela do INFISC (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Tributação no município | 1 |
> | 2 | Tributação fora do município | 2 |
> | 3 | Isenção | 3 |
> | 4 | Imune | 4 |
> | 5 | Exigibilidade suspensa por decisão judicial | 5 |
> | 6 | Exigibilidade suspensa por procedimento administrativo | 6 |

### Nota Salvador

A coluna Prefeitura indica onde o código vale.

> [!DETALHES] Tabela do Nota Salvador
> | Código no InvoiCy | Prefeitura | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- | :--- |
> | 1 | Todos | Tributação no município | 1 |
> | 2 | Todos | Tributação fora do município | 2 |
> | 3 | Todos | Isenção | 3 |
> | 4 | Todos | Imune | 4 |
> | 5 | Todos | Exigibilidade suspensa por decisão judicial | 5 |
> | 6 | Todos | Exigibilidade suspensa por procedimento administrativo | 6 |
> | 15 | Salvador | Operação tributável a prefeitura | 1 |

### RLZ ABRASF (cidadão online)

Conversão do código InvoiCy para o valor enviado ao RLZ na versão ABRASF:

> [!DETALHES] Tabela do RLZ ABRASF
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 3 | Isenção | 3 |
> | 4 | Imunidade | 5 |
> | 7 | Exigível | 1 |
> | 9 | Exportação | 4 |
>
> O print do RLZ ABRASF traz três linhas a mais que a tabela acima: código 8 (Não incidência) vira 2, código 5 (Exigibilidade suspensa por decisão judicial) vira 6 e código 6 (Exigibilidade suspensa por processo administrativo) vira 7. Qual das duas vale está a confirmar.
>
> ![Tabela de natureza da operação do RLZ ABRASF com as colunas Código Tabela Unificada, Valor na prefeitura e Descrição](../../assets/imagens/rlz/natureza-da-operacao-rlz-abrasf.jpg "Print original do RLZ ABRASF")

### Sigcorp ABRASF

> [!DETALHES] Tabela do Sigcorp ABRASF
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 3 | Isenção | 3 |
> | 9 | Exportação | 4 |
> | 4 | Imunidade | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por processo administrativo | 7 |
>
> ![Tabela de natureza da operação do Sigcorp ABRASF com as colunas Código Tabela Unificada, Valor na prefeitura e Descrição](../../assets/imagens/sigcorp/natureza-da-operacao-sigcorp-abrasf.jpg "Print original do Sigcorp ABRASF")

### SMARAPDSil WS2

> [!DETALHES] Tabela do SMARAPDSil WS2
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 3 | Isenção | 3 |
> | 9 | Exportação | 4 |
> | 4 | Imunidade | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por processo administrativo | 7 |
>
> ![Tabela de natureza da operação do SMARAPDSil WS2 com as colunas Código Tabela Unificada, Valor na prefeitura e Descrição](../../assets/imagens/smarapdisil/natureza-da-operacao-smarapdsil-ws2.jpg "Print original do SMARAPDSil WS2")

### TINUS WS

> [!DETALHES] Tabela do TINUS WS (igual para todas as prefeituras)
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 1 | Operação tributável | 1 |
> | 4 | Imunidade | 2 |
> | 7 | Operação tributável | 1 |
> | 8 | Não incidência | 4 |
> | 9 | Exportação de serviço | 3 |

### TIPLAN 2.0

Layout próprio, não nacional. O print traz só o código da tabela unificada e a descrição, sem a coluna de valor na prefeitura.

> [!DETALHES] Tabela do TIPLAN 2.0
> | Código no InvoiCy | Descrição |
> | :--- | :--- |
> | 7 | Exigível |
> | 8 | Não incidência |
> | 3 | Isenção |
> | 9 | Exportação |
> | 4 | Imune |
> | 5 | Exigibilidade suspensa por decisão judicial |
> | 6 | Exigibilidade suspensa por processo administrativo |
>
> ![Tabela de natureza da operação do TIPLAN 2.0 com as colunas Código Tabela Unificada e Descrição](../../assets/imagens/tiplan/natureza-da-operacao-tiplan-2-0.jpg "Print original do TIPLAN 2.0")

### Tributos Municipais WS

> [!DETALHES] Tabela do Tributos Municipais WS
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 3 | Isenção | 3 |
> | 9 | Exportação | 4 |
> | 4 | Imunidade | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por processo administrativo | 7 |
>
> ![Tabela de natureza da operação do Tributos Municipais WS com as colunas Valor a ser informado, Informação trazida pela prefeitura e Descrição](../../assets/imagens/tributos-municipais-es/natureza-da-operacao.jpg "Print original do Tributos Municipais WS")

### WebISS 2.0

> [!DETALHES] Tabela do WebISS 2.0
> | Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
> | :--- | :--- | :--- |
> | 7 | Exigível | 1 |
> | 8 | Não incidência | 2 |
> | 3 | Isenção | 3 |
> | 9 | Exportação | 4 |
> | 4 | Imune | 5 |
> | 5 | Exigibilidade suspensa por decisão judicial | 6 |
> | 6 | Exigibilidade suspensa por processo administrativo | 7 |
>
> ![Tabela de natureza da operação do WebISS 2.0 com as colunas Código da prefeitura, Código Tabela Unificada e Descrição](../../assets/imagens/webiss/natureza-da-operacao-webiss-2-0.png "Print original do WebISS 2.0")

## Fontes

- [Natureza da Operação](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Funcionalidades/NaturezadaOperacao/) (20/01/2026)
