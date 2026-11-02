# E0037: Produção Restrita não habilitada no município

## Mensagem

> O código do município emissor informado na DPS é inexistente no cadastro de convênio municipal do sistema nacional.

## Causa

Apesar da mensagem, o município **existe**. O erro ocorre quando ele ainda **não configurou a Produção Restrita** (ambiente de testes) na Plataforma Nacional da NFS-e. Para funcionar, o município precisa:

- Replicar as configurações do ambiente de produção para o de produção restrita.
- Habilitar o módulo de Produção Restrita na Plataforma Nacional.
- Concluir as configurações de convênio exigidas pelo ambiente nacional.

## Como resolver

Não há ajuste no ERP nem no InvoiCy. O **contribuinte** deve contatar a prefeitura (ou o órgão responsável) e pedir a habilitação da Produção Restrita na Plataforma Nacional da NFS-e. Depois disso, reenvie a DPS.

Ocorrência registrada no padrão E&L, mas a regra é do ambiente nacional e pode aparecer em outros padrões que repassam a DPS ao ADN.
