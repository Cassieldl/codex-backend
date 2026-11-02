# Reforma Tributária do Consumo: visão geral para NFS-e

> Fontes: [Reforma Tributária do Consumo](https://desenvolvedores.migrate.info/ReformaTributaria/Contextualizacao/) (11/12/2025) e [RTC: Impacto nos Módulos Fiscais](https://desenvolvedores.migrate.info/ReformaTributaria/Modulos/) (02/03/2026). Base legal: EC 132/2023 e LC 214/2025.

## O que muda

A Reforma substitui tributos sobre o consumo por um **IVA Dual**:

| Novo tributo | Competência | Substitui |
| :--- | :--- | :--- |
| **CBS** - Contribuição sobre Bens e Serviços | Federal | PIS e Cofins |
| **IBS** - Imposto sobre Bens e Serviços | Estados e municípios | ICMS e **ISS** |
| **IS** - Imposto Seletivo | Federal | (novo; desestimula bens e serviços prejudiciais à saúde ou ao meio ambiente) |

Princípios: **não cumulatividade plena** (crédito amplo), **tributação no destino** (onde o serviço é consumido), **transparência** (tributo destacado na nota) e **cashback** para baixa renda.

## Cronograma (EC 132/2023)

| Período | O que acontece |
| :--- | :--- |
| 2024–2025 | Regulamentação por leis complementares. |
| **2026** | Ano de teste: CBS **0,9%** e IBS **0,1%**. |
| 2027 | Cobrança plena da CBS e do IS; extinção de PIS e Cofins. |
| 2029–2032 | Transição gradual do ICMS e do ISS para o IBS. |
| 2033 | Modelo integral: fim de ICMS, ISS, IPI e Cofins. |

## Impacto na NFS-e

- O **Ambiente de Dados Nacional (ADN)** passa a ser o repositório obrigatório das NFS-e.
- Dois fluxos no InvoiCy:
  - **Emissão direta nacional:** municípios do Emissor Nacional; o InvoiCy fala direto com o ambiente da Receita.
  - **Emissão híbrida:** municípios com sistema próprio; o InvoiCy envia à prefeitura, que sincroniza com o ADN.
- **Novos conceitos no XML:** `cClassTrib` (classificação tributária), CST do IBS/CBS, `cIndOp` (indicador da operação), NBS, **destinatário** (local de consumo) e os valores de IBS/CBS.
- A combinação **NBS + `cClassTrib` + CST + `cIndOp`** precisa estar na **tabela de correlação** oficial.

## Tabelas e ferramentas oficiais

- [Classificação tributária (cClassTrib)](https://dfe-portal.svrs.rs.gov.br/DFE/ClassificacaoTributaria)
- [Tabela de crédito presumido (cCredPres)](https://dfe-portal.svrs.rs.gov.br/CFF/TabelaCreditoPresumido)
- [Correlação item × NBS × indOp × cClassTrib](https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/rtc/anexoviii-correlacaoitemnbsindopcclasstrib_ibscbs_v1-00-00.xlsx/view)
- [Calculadora de tributos da Receita Federal](https://piloto-cbs.tributos.gov.br/servico/calculadora-consumo/calculadora)
- [Notas técnicas da NFS-e (gov.br)](https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica)

## Artigos relacionados

- [NT 007 - retenções de PIS, COFINS e CSLL](nt007-retencoes-federais.md)
- [IBS/CBS e parametrização da prefeitura](ibs-cbs-parametrizacao-prefeitura.md)
- [Rejeições da Reforma Tributária (IBS/CBS)](../../03-rejeicoes/reforma-tributaria-nfse.md) - **em revisão**

> Regras de preenchimento de IBS/CBS (campos obrigatórios, faixas de série, prazos de obrigatoriedade) mudam a cada nota técnica. Ficaram na [lista de revisão](../../pendencias-revisao.md) até serem validadas.
