# Exemplos da DPS Nacional 1.01

Fonte dos exemplos de campo (`exemplo`) do Emissor Nacional 0.20 e do E&L 1.01. A carga lê estes
arquivos por `database/carga/exemplos-xml.mjs` (fonte `dps-nacional`, raiz `<DPS>`).

## Origem

`documentacao/Provedores/E&L/Layout_EL_DPS_Nacional/Layout_EL_DPS_Nacional/`:

| Cópia | Original |
|-------|----------|
| `exemplo_dps.xml` | `exemplo_dps.xml` |
| `exemplo_dps_ibscbs.xml` | `exemplo_dps_ibscbs.xml` |

## O que foi removido

Cópias saneadas na origem, para o repositório não carregar dado que o portal omite:

- O bloco `<Signature>` inteiro. O certificado X.509 traz e-mail e CNPJ reais do signatário.
- O **texto** das folhas de identificação, que ficam como tag vazia (`<CNPJ></CNPJ>`): documento e
  inscrição (`CNPJ`, `CPF`, `IM`), nome (`xNome`), telefone (`fone`) e endereço (`CEP`, `xLgr`,
  `nro`, `xBairro`). A lista exata é `ehFolhaDeIdentificacao` em `exemplos-xml.mjs`.

O atributo `Id` de `<infDPS>` permanece (atributo nunca é exemplo, e o número é anonimizado).
Todo o resto é cópia fiel do original, inclusive indentação (tabs) e quebras de linha.

## Fora da fonte

`exemplo_evento_dps.xml` (raiz `<pedRegEvento>`, cancelamento) não é copiado e não é fonte: a
leitura recusa qualquer raiz diferente de `<DPS>`.
