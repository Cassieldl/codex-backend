# Natureza da operação (natOp)

> Fonte: [Natureza da Operação](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Funcionalidades/NaturezadaOperacao/) (20/01/2026).

## O que é

Indica a finalidade/tributação do serviço: tributado no município ou fora, isento, imune, exportação, etc.

## Tabela unificada do InvoiCy

Cada prefeitura usa códigos próprios. O InvoiCy usa uma **tabela única** no layout de recepção e converte para o código de cada padrão. Assim, "tributado no município do prestador" tem sempre o mesmo valor para quem emite.

Mesmo assim, **nem toda opção é aceita por todo padrão**. Antes de configurar o cliente, consulte quais valores o município aceita.

A conversão por padrão está na aba *Natureza de Operação* do layout oficial (`_origem/Padrões/Layout InvoiCy/Layout_Recepcao_NFSe_Reforma_Tributaria - versão 9.xlsx`).

## Como consultar

**Pela tela:** *Painel de Controle → NFS-e → Natureza da Operação*. Mostra código InvoiCy, prefeitura, descrição e valor na prefeitura, ou avisa se o município não tem naturezas.

![Natureza da operação no painel](../../assets/imagens/natureza-da-operacao-natop/natureza-da-operacao-no-painel.png)
![Município sem natureza da operação](../../assets/imagens/natureza-da-operacao-natop/municipio-sem-natureza-da-operacao.png)

**Pelo webservice (SOAP):**

- WSDL homologação: <https://homolog.invoicy.com.br/arecepcao.aspx?wsdl>
- WSDL produção: <https://app.invoicy.com.br/arecepcao.aspx?wsdl>
- Exemplo de envio: <https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Consulta%20Naturezas%20de%20Opera%C3%A7%C3%A3o%20NFSe/Exemplos%20XML/Consultar%20naturezas%20envio.xml>
- Informe **CNPJ** e **tipo de ambiente**. `IMEmissor` é opcional, só para empresas com o mesmo CNPJ em mais de um município.

## Cuidados

- Só funciona para CNPJ **já cadastrado** no InvoiCy.
- Use **só para atualizar o cadastro do cliente**, nunca a cada emissão: há limite de chamadas por hora e a empresa pode ser **bloqueada**.
- Se o município **trocar de padrão**, as naturezas podem mudar.

## Tabelas por padrão (prints)

| Padrão | Tabela |
| :--- | :--- |
| Barueri | Sem tabela: o layout é em arquivo texto. |
| Betha 2.0 (legado, ABRASF) | [imagem](../../assets/imagens/betha/natureza-da-operacao-abrasf.jpg). O Betha atual é leiaute nacional: use a [tabela do Padrão Nacional](#padrão-nacional-e-provedores-de-leiaute-nacional). |
| Caucaia WS | [tabela abaixo](#caucaia-ws) |
| CONAM (e-transparência) | [tabela abaixo](#conam-e-transparência) |
| CENTI 2 | [imagem](../../assets/imagens/centi/natureza-da-operacao-centi.jpg) |
| CITTA | [imagem](../../assets/imagens/citta/natureza-da-operacao-citta.png) |
| Equiplano | [tabela abaixo](#equiplano) |
| Elotech (Tributos Web 2.03) | [tabela abaixo](#tributos-web---elotech-203) |
| e-Governe ISS | [imagem](../../assets/imagens/e-governe-iss/natureza-da-operacao-e-governe.jpg) |
| GISS | [tabela abaixo](#giss) |
| GLC Consultoria WS (SIGISSWEB) | [tabela abaixo](#glc-consultoria-ws-sigissweb) |
| Governo Digital | [imagem](../../assets/imagens/governo-digital/natureza-da-operacao-governo-digital.png) |
| H2M (HM2 Soluções WS) | [tabela abaixo](#h2m-hm2-soluções-ws) |
| INFISC | [tabela abaixo](#infisc) |
| IPM | Tabela natOp × IPM em [ipm.md](../../03-rejeicoes/ipm.md#situação-tributária-natop-invoicy--ipm) |
| Nota Salvador | [tabela abaixo](#nota-salvador) |
| Padrão Nacional | [imagem](../../assets/imagens/emissor-nacional/natureza-da-operacao-no-padrao-nacional.png) |
| RLZ ABRASF (cidadão online) | [imagem](../../assets/imagens/rlz/natureza-da-operacao-rlz-abrasf.jpg) e [tabela abaixo](#rlz-abrasf-cidadão-online) |
| Sigcorp ABRASF | [imagem](../../assets/imagens/sigcorp/natureza-da-operacao-sigcorp-abrasf.jpg) |
| SMARAPDSil WS2 | [imagem](../../assets/imagens/smarapdisil/natureza-da-operacao-smarapdsil-ws2.jpg) |
| TINUS WS | [tabela abaixo](#tinus-ws) |
| TIPLAN 2.0 (layout próprio, não nacional) | [imagem](../../assets/imagens/tiplan/natureza-da-operacao-tiplan-2-0.jpg) |
| Tributos Municipais WS | [imagem](../../assets/imagens/tributos-municipais-es/natureza-da-operacao.jpg) |
| WebISS 2.0 | [imagem](../../assets/imagens/webiss/natureza-da-operacao-webiss-2-0.png) |

No Padrão Nacional, `natOp = 4` (imunidade) exige `tpImunidade = 2`.

## Caucaia WS

Conversão do código InvoiCy para o valor enviado ao Caucaia WS, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Tributação no município | 1 |
| 2 | Tributação fora do município | 2 |
| 3 | Isenção | 3 |
| 4 | Imune | 4 |
| 5 | Exigibilidade suspensa por decisão judicial | 5 |
| 6 | Exigibilidade suspensa por procedimento administrativo | 6 |

## Tributos Web - Elotech 2.03

Conversão do código InvoiCy para o valor enviado ao Elotech, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 3 | Isenção | 3 |
| 4 | Imunidade | 5 |
| 5 | Exigibilidade suspensa por decisão judicial | 6 |
| 6 | Exigibilidade suspensa por procedimento administrativo | 7 |
| 7 | Exigível | 1 |
| 8 | Não incidência | 2 |
| 9 | Exportação | 3 |

## Equiplano

Conversão do código InvoiCy para o valor enviado ao Equiplano, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Tributação no município | 1 |
| 2 | Tributação fora do município | 1 |
| 3 | Isenção | 3 |
| 4 | Imune | 3 |
| 5 | Exigibilidade suspensa por decisão judicial | 4 |
| 7 | Tributação no município | 1 |

## CONAM (e-transparência)

Conversão do código InvoiCy para o valor enviado ao CONAM, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Tributado | 1 |
| 3 | Isenção / Imunidade | 2 |
| 5 | Suspensão | 3 |
| 6 | Suspensão | 3 |
| 7 | Tributado | 1 |
| 10 | ISS Fixo | 5 |
| 12 | Simples Nacional | 4 |
| 13 | Isenção parcial | 6 |

## GISS

Conversão do código InvoiCy para o valor enviado ao GISS, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Tributação no município | 1 |
| 3 | Isenção | 3 |
| 4 | Imunidade | 5 |
| 5 | Exigibilidade suspensa por decisão judicial | 6 |
| 6 | Exigibilidade suspensa por processo administrativo | 7 |
| 7 | Exigível | 1 |
| 8 | Não incidência | 2 |
| 9 | Exportação | 4 |
| 15 | Exigível | 1 |

## GLC Consultoria WS (SIGISSWEB)

Conversão do código InvoiCy para o valor enviado ao GLC, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Incidência IBS/CBS | 0 |
| 7 | Incidência IBS/CBS | 0 |
| 9 | Exportação | 1 |

## H2M (HM2 Soluções WS)

Conversão do código InvoiCy para o valor enviado ao H2M, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Tributação no município | 1 |
| 2 | Tributação fora do município | 2 |
| 3 | Isenção | 3 |
| 4 | Imune | 4 |
| 5 | Exigibilidade suspensa por decisão judicial | 5 |
| 6 | Exigibilidade suspensa por procedimento administrativo | 6 |

## INFISC

Conversão do código InvoiCy para o valor enviado ao INFISC, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Tributação no município | 1 |
| 2 | Tributação fora do município | 2 |
| 3 | Isenção | 3 |
| 4 | Imune | 4 |
| 5 | Exigibilidade suspensa por decisão judicial | 5 |
| 6 | Exigibilidade suspensa por procedimento administrativo | 6 |

## Nota Salvador

Conversão do código InvoiCy para o valor enviado ao Nota Salvador. A coluna Prefeitura indica onde o código vale:

| Código no InvoiCy | Prefeitura | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- | :--- |
| 1 | Todos | Tributação no município | 1 |
| 2 | Todos | Tributação fora do município | 2 |
| 3 | Todos | Isenção | 3 |
| 4 | Todos | Imune | 4 |
| 5 | Todos | Exigibilidade suspensa por decisão judicial | 5 |
| 6 | Todos | Exigibilidade suspensa por procedimento administrativo | 6 |
| 15 | Salvador | Operação tributável a prefeitura | 1 |

## TINUS WS

Conversão do código InvoiCy para o valor enviado ao TINUS WS, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Operação tributável | 1 |
| 4 | Imunidade | 2 |
| 7 | Operação tributável | 1 |
| 8 | Não incidência | 4 |
| 9 | Exportação de serviço | 3 |

## RLZ ABRASF (cidadão online)

Conversão do código InvoiCy para o valor enviado ao RLZ na versão ABRASF:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 3 | Isenção | 3 |
| 4 | Imunidade | 5 |
| 7 | Exigível | 1 |
| 9 | Exportação | 4 |

## Padrão Nacional e provedores de leiaute nacional

Conversão do código InvoiCy para o valor enviado em `tribISSQN`, igual para todas as prefeituras:

| Código no InvoiCy | Descrição da natureza da operação | Valor na prefeitura |
| :--- | :--- | :--- |
| 1 | Operação tributável | 1 |
| 4 | Imunidade | 2 |
| 7 | Operação tributável | 1 |
| 8 | Não incidência | 4 |
| 9 | Exportação de serviço | 3 |

Usam essa tabela o **Emissor Nacional** e os provedores que recebem a DPS no leiaute nacional (De/Para com `tribISSQN` no grupo `tribMun`): Betha, Coplan, DSFNET, Dueto, E&L, Fiorilli, GESPAM, ISSNET, João Pessoa, RLZ, Sigcorp e SMARAPDISIL. Nos que também têm versão ABRASF (RLZ, Sigcorp e SMARAPDISIL), a versão ABRASF segue a tabela própria da lista acima.

**Critério:** é "nacional" o layout cujos campos têm nomes muito parecidos com os do Emissor Nacional (às vezes com um campo ou outro diferente). O que foge disso, inclusive o ABRASF, é layout próprio, com tabela própria (ex.: Tributos Municipais ES, GISS, Elotech).

Códigos fora dessa tabela não têm conversão no leiaute nacional. Para imunidade, ver `tpImunidade` acima.
