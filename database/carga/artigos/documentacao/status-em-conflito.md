# Status em conflito

## Resumo

O documento fica **em conflito** quando o InvoiCy **não consegue confirmar** se a prefeitura autorizou a nota. Acontece quando a comunicação falha (timeout, queda do webservice) e o padrão **não tem serviço de consulta**, ou a consulta exige um dado (protocolo, código de verificação) que não voltou.

## Risco

Reenviar o RPS sem conferir pode gerar **duas NFS-e para o mesmo RPS** (duplicidade).

## O que fazer

1. **Não reenvie** de imediato.
2. Consulte no **portal da prefeitura** se existe NFS-e para aquele número de RPS (normalmente pelo CNPJ do prestador, número do RPS e CPF/CNPJ do tomador).
3. **Se a nota existe:** atualize o registro no InvoiCy com os dados da NFS-e (número e código de verificação), pelo webservice de correção de notas em conflito ou pela tela de suporte nos detalhes do documento.
4. **Se não existe:** reenvie o RPS.

## Padrões em que é comum

| Padrão | Motivo |
| :--- | :--- |
| IPM | A consulta exige o código de verificação, que não volta se a emissão falhar. |
| RLZ | Algumas versões não têm serviço de consulta. |
| E&L | Algumas versões não têm serviço de consulta. |
| SMARAPDISIL (WS) | Timeouts frequentes impedem capturar o protocolo. |

Prints do SMARAPD: [consultar nota](../../assets/imagens/smarapdisil/consultar-nota-fiscal-smarapd.png) · [nota localizada](../../assets/imagens/smarapdisil/nota-fiscal-localizada.png).

## Mensagens do InvoiCy relacionadas

- **inv0304** - exceção na comunicação com a prefeitura.
- **inv0339** - nenhum retorno da prefeitura para o serviço.

Ver [invoicy-inv03xx.md](../../03-rejeicoes/invoicy-inv03xx.md).
