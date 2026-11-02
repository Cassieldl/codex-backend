# Cancelamento em padrões sem webservice de cancelamento

## Resumo

Alguns padrões não oferecem cancelamento via webservice. Nesses casos, o InvoiCy apenas **marca o documento como cancelado internamente** (mensagem **inv0349**): a nota continua válida na prefeitura até ser cancelada no **portal da prefeitura**.

## Como cancelar

1. Cancele a nota no **portal da prefeitura**.
2. No InvoiCy, **consulte o documento** para trazer o status cancelado.
   - Se o padrão não tiver consulta, peça o cancelamento no InvoiCy só para atualizar o status.

> Se o padrão não permite consulta, ou o retorno não traz o cancelamento (Caucaia, TINUS), o status no InvoiCy não será atualizado pela prefeitura. Confira no portal.

## Provedores e municípios sem cancelamento via webservice

| Provedor | Situação |
| :--- | :--- |
| Barueri | Sem cancelamento em homologação. |
| Betha | Maracás/BA. |
| Caucaia | Só pelo portal; o retorno não traz o cancelamento. |
| Dueto | Cândido Mota/SP (prazo de cancelamento zero; contate a prefeitura) e Catanduva/SP. |
| GISS | Ananindeua/PA, Jacareí/SP, Franca/SP: só por processo administrativo. |
| ISSNET | Cruz Alta/RS (declaração na prefeitura); Cuiabá/MT e Novo Hamburgo/RS (portal + pedido no InvoiCy). |
| Nota Salvador | Sem webservice; cancele no portal e consulte no InvoiCy. |
| RLZ | Alguns municípios. |
| SMARAPDISIL | Nova Odessa/SP. |
| TINUS / Tinus Upload | Cancelamento só no sistema da prefeitura; canceladas não retornam XML. |
| TIPLAN 2.0 | Sem cancelamento via webservice. |
| WebISS | Alguns municípios; o 2.0 exige e-mail do tomador na emissão para cancelar via webservice. |

## Bloqueios de cancelamento

Mesmo com webservice, o cancelamento pode ser recusado:

| Motivo | Exemplos |
| :--- | :--- |
| Nota em **guia de pagamento** | Betha EB1/L1, GESPAM P0006/P0009 |
| **Prazo expirado** | GESPAM P0008; CITTA (5–15 dias úteis); Tributos Municipais WS (até o dia 5 do mês seguinte) |
| Prefeitura configurada para **não cancelar** | Betha BTH09 |
| **Última nota** em padrão que reaproveita o número | CONAM: o InvoiCy bloqueia. Emita a próxima antes. Ver [Emissão sequencial](emissao-sequencial-rps.md). |
| Documento **não autorizado** | inv0320 / inv0350: não é preciso cancelar; corrija e reenvie. |
| Nota **sem tomador** | Elotech |
