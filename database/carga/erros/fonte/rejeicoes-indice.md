# Rejeições por provedor

Códigos de erro e alerta devolvidos por cada provedor, com causa e solução quando a documentação de origem traz.

## Por onde começar

1. **Mensagem com código `[#inv03xx]`?** É do próprio InvoiCy: [invoicy-inv03xx.md](invoicy-inv03xx.md) (48 códigos, todos os provedores).
2. **"Mensagem retornada pela prefeitura" (inv0328)?** Procure o texto da prefeitura no arquivo do provedor abaixo.
3. **Município no Padrão Nacional (DPS/ADN)?** Veja [padrao-nacional.md](padrao-nacional.md).

## Gerais

| Arquivo | Conteúdo |
| :--- | :--- |
| [invoicy-inv03xx.md](invoicy-inv03xx.md) | Mensagens do InvoiCy: município não integrado, sem homologação, RPS efetivado/cancelado, fila assíncrona, falhas de comunicação. |
| [reforma-tributaria-nfse.md](reforma-tributaria-nfse.md) | **Em revisão.** 80 rejeições de IBS/CBS (E0854–E1607) com descrição, regra e solução. Páginas de dez/2025 a fev/2026: validar antes de publicar ([pendências](../pendencias-revisao.md)). |
| [padrao-nacional.md](padrao-nacional.md) | Rejeições do Emissor Nacional/ADN com solução: série com letra, `regApTribSN`, `totTrib`, `cIndOp`, `TributMunicipio`, migração para a versão 0.20, exportação (E0330/E0333). |

## Por provedor

| Provedor | Arquivo | Conteúdo |
| :--- | :--- | :--- |
| Barueri | [barueri.md](barueri.md) | Erros do arquivo texto RPS (layout PMB004), por tipo de registro. |
| Betha | [betha.md](betha.md) | Erros do webservice (RPS) e da DPS nacional, com causa/solução para E146 e E999. |
| E&L | [e-l.md](e-l.md) | E0037 (Produção Restrita não habilitada). |
| Elotech | [elotech.md](elotech.md) | Erros e alertas do webservice, com motivo/solução. |
| GESPAM | [gespam.md](gespam.md) | Códigos S, C, P e L do ISS Online + problemas comuns (HTTP 401, 415, 429). |
| GISS | [giss.md](giss.md) | Erros mais comuns com causa + lista completa de erros e alertas. |
| IPM | [ipm.md](ipm.md) | 291 rejeições com campo InvoiCy, causa, correção e responsável + natOp × situação tributária IPM. |
| TIPLAN | [tiplan.md](tiplan.md) | Códigos 2100–2125 do município. |

Explicações mais longas de rejeições do leiaute nacional estão na [base de conhecimento](../04-base-conhecimento/README.md) (ex.: E1235, E0037).

### Erros citados nas observações dos provedores

| Provedor | Código | Mensagem | Solução |
| :--- | :--- | :--- | :--- |
| CONAM | - | Usuário/Contribuinte Não Identificado. | Usuário = Código do Usuário; Senha = Código do Contribuinte (caminho nas [observações do CONAM](../02-observacoes/observacoes-provedores.md#conam)). |
| Coplan | E138 | Usuário não autorizado a realizar o serviço. | Pedir liberação a equipetributario@coplanmt.com.br (Sinop/MT: pela prefeitura). |
| Betha | E43 | Inscrição Municipal do prestador não encontrada na base de dados do município. | Preencher a IM no cadastro da empresa na prefeitura (se bloqueado, pedir ao suporte da prefeitura). |
| DSFNET | L0022 | Série fora da faixa em produção. | Usar série entre 10001 e 49999. |
| Equiplano | 8001 | Identificação da entidade inválida. | Informar ao suporte o ID da entidade exibido após o login em esnfs.com.br. |
| ISSNET | E157 | Usuário não está autorizado a utilizar esse serviço para esse contribuinte. | Pedir liberação à Nota Control (homologação paga; produção por e-mail a suporte@notaeletronica.com.br). |
| ISSNET | E160 | Arquivo enviado fora da estrutura do arquivo XML de entrada. | `RegEspTrib` só aceita 1, 2, 3 ou 4. |
| ISSNET | L042 | IM do tomador consta como baixada. | Não informar a IM de tomador baixado; não reter ISS. |
| ISSNET | L044 | O item LC 116/2003 informado não corresponde ao CNAE informado. | Usar item correlacionado ao CNAE do cadastro (tabela em *Downloads* do ISS.net). |
| Sigcorp | E405 | Método em utilização. Por favor repita a operação. | Geralmente é dado inválido no XML: revisar tag a tag. |

> [ipm-pendencias-internas.md](ipm-pendencias-internas.md) é de **uso interno**: pendências de mapeamento a confirmar com Produto/Dev. Não publicar no FAQ.
