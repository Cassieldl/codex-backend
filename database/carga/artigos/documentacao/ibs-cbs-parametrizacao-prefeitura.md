# IBS/CBS rejeitado por falta de parametrização da prefeitura

## Resumo

O padrão pode estar atualizado para a Reforma Tributária (grupo `IBSCBS`), mas a emissão com IBS/CBS **depende de a prefeitura parametrizar** o sistema. Enquanto isso não acontece, as notas com o grupo `IBSCBS` são rejeitadas.

## Sintoma

Exemplo no padrão Betha:

> **E999** - Alíquota efetiva da União para CBS incorreta. `ValorInformado == 0.90 && ValorCalculado == 0`

O valor calculado zerado indica que a prefeitura ainda não tem as alíquotas configuradas.

## Como resolver

- **Não envie** o campo `VersaoEsquema=RTC`. Assim o InvoiCy não gera o grupo `<IBSCBS>` e a nota segue no modelo anterior.
- Quando a prefeitura concluir a parametrização, volte a enviar `VersaoEsquema=RTC`.

## Outras regras de IBS/CBS que geram rejeição

- Grupo `IBSCBS` enviado por **optante do Simples Nacional** (Sigcorp).
- Grupo `cNBS` ausente quando o grupo `IBSCBS` é enviado (Sigcorp).
- `cClassTrib` não encontrado (GESPAM C0014) ou combinação de classificação tributária fora da tabela municipal (Sigcorp).
- Destinatário obrigatório após a Reforma (GLC Consultoria).
