# NT 007: retenções de PIS, COFINS e CSLL

> Fontes: [obs] SMARAPDISIL e TIPLAN; [FAQ - Padrão Nacional](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/FAQ/); [Configurações para Emissão: NFS-e](https://desenvolvedores.migrate.info/Sistema/PaineldeControle/ConfiguracoesparaEmissao/NFSe/).

## Resumo

Desde **09/02/2026** vigora a **Nota Técnica 007/2026** da NFS-e Nacional. Os campos individuais de **PIS e COFINS retidos deixam de existir**: quando houver retenção, PIS, COFINS e CSLL retidos são **somados** e informados num único campo, `vRetCSLL` (Contribuições Sociais Retidas).

## De-para no layout InvoiCy

| Campo nacional | Campo InvoiCy | Uso após a NT 007 |
| :--- | :--- | :--- |
| `vRetCSLL` | **`ValCSLL`** | **Soma** de PIS, COFINS e CSLL **retidos**. |
| `vPis` | `ValPIS` | Só **débito próprio** do prestador (destaque). Nunca retenção. |
| `vCofins` | `ValCOFINS` | Só **débito próprio** do prestador (destaque). Nunca retenção. |
| - | `ValAliqPIS`, `ValAliqCOFINS` | Alíquotas de **destaque**. |
| `tpRetPisCofins` | `tpRetPisCofins` | Indica quais tributos foram retidos. |

> Usar `ValPIS`/`ValCOFINS` para retenção pode **reduzir indevidamente a base do IBS/CBS** e gerar inconsistência na apuração (orientação do suporte do Sistema Nacional).

## Retenção e destaque ao mesmo tempo

Um mesmo documento pode ter um percentual de PIS/COFINS **retido** e outro **destacado** (débito próprio):

- Destaque → `ValAliqPIS`, `ValAliqCOFINS`, `ValPIS`, `ValCOFINS`.
- Retenção → somada em `ValCSLL`.

## Opção "Adequado à NT007?" no InvoiCy

Em *Configurações para emissão → Particularidades*:

| Valor | Comportamento |
| :--- | :--- |
| **Sim** | O InvoiCy **soma** `ValCSLL` + `ValPISRetido` + `ValCOFINSRetido` e envia o total no campo de CSLL retida da prefeitura. Considera só os tributos marcados como retidos em `tpRetPisCofins`; sem essa tag, usa `PISRetido`, `COFINSRetido` e `CSLLRetido`. Se nenhum for retido, o campo não é enviado. |
| **Não** | Repassa os valores exatamente como o cliente enviou. |

## Outras mudanças

| Item | Regra |
| :--- | :--- |
| Código da Situação Tributária (CST) | Acrescentados os códigos **49 a 99**. |
| Tipo de retenção PIS/COFINS e CSLL | Acrescentados os códigos **0 e 3 a 9**. Os códigos **1 e 2** saem após a obrigatoriedade do grupo IBSCBS. |
| "Outras Retenções Federais" | Não usar. Informe cada retenção no campo específico. |

## Exemplo

PIS retido R$ 6,50 + COFINS retido R$ 30,00 + CSLL retida R$ 10,00:

- `ValCSLL` = **46.50**.
- `ValPIS` / `ValCOFINS`: só se houver débito próprio a destacar.

## Rejeições relacionadas

- **TIPLAN 2120:** valor em "Outras Retenções Federais".
- **TIPLAN 2116 / 2117:** CST do PIS/COFINS ausente ou incompatível.

## Ver também

[Retenções federais: como informar e preencher](retencoes-federais.md) cobre IRRF e INSS, os provedores que não seguem a NT 007 e os cenários de preenchimento.

Sempre use a versão mais recente do [layout InvoiCy](https://github.com/migrate-company/integracao-InvoiCy-BR/tree/main/Layout%20Integra%C3%A7%C3%A3o%20-%20Reforma%20Tribut%C3%A1ria/NFSe).
