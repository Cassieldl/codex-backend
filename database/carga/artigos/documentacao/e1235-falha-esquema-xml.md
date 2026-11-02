# E1235: Falha no esquema XML do DF-e

## Resumo

A rejeição **E1235** indica que o XML **não segue o esquema XSD oficial** do leiaute nacional. É uma rejeição do padrão nacional e aparece em qualquer provedor que valide a DPS contra o XSD.

## Causas mais comuns

**Ordem das tags**
- Tags fora da ordem definida no leiaute nacional.

**Campos obrigatórios**
- Tag obrigatória ausente (ex.: `Bairro`).
- Tag presente, mas **sem valor**.

**Formato do conteúdo**
- Quantidade de caracteres incorreta.
- Valor fora do domínio permitido.
- Data em formato inválido.
- Código fora do tamanho exigido (ex.: RPS sem 13 posições).
- `cNBS` com pontos: use só os **9 dígitos**.
- Valor com **vírgula decimal**: use **ponto** (`10.50`).
- Atributo `versao` diferente de `1.01`.
- Nome de tag grafado errado (ex.: `infFinal` em vez de `indFinal`).

**Estrutura do XML**
- Tags sem fechamento.
- Hierarquia incorreta.
- Caracteres especiais não escapados (`&`, `<`, `>`).

## Como resolver

1. Leia o campo de detalhe do retorno (ex.: `InformacoesComplementares` no GESPAM): ele indica a **posição e o campo** com problema.
2. Confira o campo no [layout do provedor](../../01-layouts/layouts-provedores.md) (tipo, tamanho, ocorrência).
3. Corrija no ERP e reenvie.

Fontes: observações dos padrões SMARAPDISIL e GESPAM.
