# Origem da rejeição: prefeitura × Ambiente Nacional

## Resumo

Nos padrões que recebem a DPS e a repassam ao **Ambiente de Dados Nacional (ADN)**, a rejeição pode vir de dois lugares. Saber quem rejeitou define quem resolve.

| Quem rejeitou | Como identificar | Quem resolve |
| :--- | :--- | :--- |
| **Prefeitura / provedor** (sistema tributário municipal) | Códigos próprios do provedor (ex.: `BTH..`, `S/C/P/L` no GESPAM, `21xx` no TIPLAN) ou status `rejeitado` (Sigcorp). | Ajuste de dados no ERP ou cadastro na prefeitura. |
| **Ambiente Nacional (ADN / Emissor Nacional)** | Códigos `E....` / `RNG...` das regras de negócio nacionais, status `recusada` (Sigcorp) ou mensagem iniciada por "Rejeitado pelo Emissor Nacional" (SMARAPDISIL). | Regras do leiaute nacional; ver o anexo de regras da NFS-e Nacional. |

Regras de negócio nacionais: <https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/documentacao-atual>

## Por provedor

- **SMARAPDISIL:** erro iniciado por "Rejeitado pelo Emissor Nacional" foi recusado pelo **Emissor Nacional**; os demais, pela **prefeitura**.
- **Sigcorp:** `rejeitado` = validação do próprio webservice; `recusada` = recusa do ADN.
- **GESPAM:** erros na recepção (HTTP 400) são do ISS Online; erros no processamento aparecem nas consultas como `PROCESSADO_COM_ERROS`.
