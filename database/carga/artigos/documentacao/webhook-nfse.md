# Webhook de NFS-e

> [!RESUMO]
> O Webhook avisa o ERP quando algo muda na NFS-e, sem consultas repetidas, com 9 notificações sobre emissão, recebimento e consulta na prefeitura.

## Como configurar

> [!PASSOS]
> 1. Abra *Painel de Controle → Webhooks → Novo*.
> 2. No campo **Uso**, escolha **Nota Fiscal de Serviço**.

## Notificações

### Documento emitido

| Notificação | Quando dispara |
| :--- | :--- |
| **Atualização de Status** | Qualquer mudança de status **diferente de 105** (pendente). |
| **Evento e Documento Inutilizado** | Mudança entre status finais (ex.: rejeitado → autorizado, autorizado → cancelado) e emissão de evento. |
| **Atualização de Dados** | Atualização de informações do documento (pode incluir status). |
| **Emissão Parada** | Um documento **não entrou na fila de emissão** porque há pendência em RPS anteriores. |

> [!DICA]
> A **Emissão Parada** é comum em municípios sequenciais e serve para achar e corrigir o RPS que travou a fila. Ver [Emissão sequencial](emissao-sequencial-rps.md).

### Documento recebido ou compartilhado

| Notificação | Quando dispara |
| :--- | :--- |
| **Documento Recebido** | Nota importada por tela, webservice, leitura de e-mail ou consulta automática. |
| **Documento Compartilhado** | Uma parte (prestador ou tomador) emitiu ou importou o documento e a outra parte também usa a plataforma. |

O **Documento Recebido** traz o XML em `DocCompleto`, no formato do parâmetro "XML InvoiCy no retorno da requisição". Com esse parâmetro em "Não", o XML vem no layout InvoiCy.

### Consulta na prefeitura

| Notificação | Quando dispara |
| :--- | :--- |
| **Inclusão na Fila para Consulta** | A empresa entrou na fila de consulta de notas recebidas na prefeitura. |
| **Novo Documento não Encontrado** | A consulta na prefeitura terminou sem achar documentos. |
| **Novo Documento Encontrado** | A consulta achou documentos ainda não importados. |

> [!IMPORTANTE]
> As três trazem **só mensagem**, sem estrutura de documento. A **Novo Documento Encontrado** informa só a **quantidade**; a importação segue o fluxo normal.

Exemplo de payload: [Notificacao_Documento_Emitido_Recebido.json](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/webhook/Exemplos%20JSON/Notificacao_Documento_Emitido_Recebido.json).

## Síncrono × assíncrono

| Processamento | Envio | Status final |
| :--- | :--- | :--- |
| **Síncrono** | O RPS vai à prefeitura na hora e o retorno volta na mesma conexão. | Vem no retorno. Se a prefeitura for assíncrona, pode voltar pendente ou em processamento; aí o Webhook ou a consulta trazem o status final. |
| **Assíncrono** | O InvoiCy devolve **105 - pendente** e coloca o RPS na fila. | Chega pelo Webhook. |

Ver [Processamento síncrono × assíncrono](processamento-sincrono-assincrono.md).

## Certificado digital

No uso **Certificado Digital**, a notificação **Vencimento Certificado** avisa quando é preciso importar um novo certificado.

Fonte: [Webhook - Configurações](https://desenvolvedores.migrate.info/Integracoes/Webhook/Configuracoes/) (12/08/2026).
