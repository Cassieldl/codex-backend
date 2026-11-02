# Webhook de NFS-e

> Fonte: [Webhook - Configurações](https://desenvolvedores.migrate.info/Integracoes/Webhook/Configuracoes/) (12/08/2026).

## Resumo

O Webhook avisa o ERP automaticamente quando algo muda no documento, sem precisar consultar repetidamente. Configure em *Painel de Controle → Webhooks → Novo*, escolhendo no campo **Uso** a opção **Nota Fiscal de Serviço**.

## Notificações de NFS-e

| Notificação | Quando dispara |
| :--- | :--- |
| **Atualização de Status** | Qualquer mudança de status **diferente de 105** (pendente). |
| **Evento e Documento Inutilizado** | Mudança entre status finais (ex.: rejeitado → autorizado, autorizado → cancelado) e emissão de evento. |
| **Atualização de Dados** | Atualização de informações do documento (pode incluir status). |
| **Documento Recebido** | Nota importada (tela, webservice, leitura de e-mail ou consulta automática). Traz o XML em `DocCompleto`, no formato do parâmetro "XML InvoiCy no retorno da requisição"; se esse parâmetro estiver "Não", vem no layout InvoiCy. |
| **Inclusão na Fila para Consulta** | A empresa entrou na fila de consulta de notas recebidas na prefeitura. |
| **Novo Documento não Encontrado** | A consulta na prefeitura terminou sem achar documentos. |
| **Novo Documento Encontrado** | A consulta achou documentos ainda não importados (só informa a **quantidade**; a importação segue o fluxo normal). |
| **Emissão Parada** | Um documento **não entrou na fila de emissão** porque há pendência em RPS anteriores (comum em municípios sequenciais). Serve para identificar e corrigir o RPS que travou a fila. |
| **Documento Compartilhado** | O documento foi emitido/importado por uma parte (prestador ou tomador) e a outra parte também usa a plataforma. |

As três notificações de fila de consulta trazem **só mensagem**, sem estrutura de documento.

Exemplos de payload: [Notificacao_Documento_Emitido_Recebido.json](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/webhook/Exemplos%20JSON/Notificacao_Documento_Emitido_Recebido.json).

## Síncrono × assíncrono

- **Síncrono:** o RPS vai à prefeitura na hora e o retorno volta na mesma conexão. Pode voltar pendente/em processamento se a prefeitura for assíncrona; aí o Webhook ou a consulta trazem o status final.
- **Assíncrono:** o InvoiCy devolve **105 – pendente** e coloca o RPS na fila. O status final chega pelo Webhook.

Ver [Processamento síncrono × assíncrono](processamento-sincrono-assincrono.md) e [Emissão sequencial](emissao-sequencial-rps.md).

## Certificado digital

Há também a notificação **Vencimento Certificado** (uso "Certificado Digital"), que avisa quando é preciso importar um novo certificado.
