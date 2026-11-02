# NFS-e recebidas e emitidas fora do InvoiCy (Importação de Documentos)

> Fonte: [Importação de Documentos](https://desenvolvedores.migrate.info/Extensoes/ImportacaodeDocumentos/) (16/10/2026) e páginas dos padrões.

## Resumo

A extensão **Importação de Documentos** traz para o InvoiCy as NFS-e emitidas **contra** o CNPJ da empresa (recebidas) e as emitidas **por** ela em outros sistemas (ex.: direto no portal da prefeitura). Ative em *Extensões → Importação de Documentos*.

## Parâmetros de NFS-e

| Parâmetro | O que faz |
| :--- | :--- |
| Tipo de arquivos para importar no módulo NFS-e | Emitidos, recebidos ou ambos. |
| NSU de NFS-e | Ponto de partida da consulta automática no **ADN**. Sem NSU, começa em `000000000000000`. |
| Número de consultas diárias na Prefeitura | 1, 2, 3, 4, 8 ou 12 por dia. |
| Cadastrar prestador para busca de NFS-e recebidas | Para prefeituras que exigem informar o prestador na consulta. |
| Cadastrar municípios para busca | Municípios onde a consulta automática será feita. |
| Abrangência da consulta | Municípios cadastrados + município da empresa · só cadastrados · todos os municípios do InvoiCy. |
| Consultar NFS-e recebidas no ambiente nacional | Busca as recebidas direto no ADN. |
| Limite de dias retroativos | 7 dias, 15 dias, 30 dias, 3 meses, 6 meses ou 1 ano (**padrão: 7 dias**). Notas pela data de emissão; eventos pela data de processamento. |
| Consulta retroativa | Frequência das consultas retroativas; também via SOAP ou API REST. |

Os parâmetros podem ser **replicados** para outras empresas, exceto o NSU (é de cada empresa).

## Onde aparecem

- **Emitidas:** tela *Documentos Emitidos*.
- **Recebidas:** tela *Documentos Recebidos* (download de XML/PDF e detalhes).

## Pontos de atenção

- As notas podem levar **dias** para aparecer, conforme o intervalo de consultas e o tempo de sincronização da prefeitura.
- Em municípios do **Padrão Nacional (ADN)** e do **ISS.NET**, a consulta de recebidas exige certificado **do CNPJ específico**; o da matriz não serve.
- Alguns municípios só consultam se a empresa tiver **inscrição municipal** lá, ou se os **prestadores** forem cadastrados antes. A consulta de municípios integrados indica isso na tag `importaNotasRecebidas`. Ver [Consultas sobre o município](consultas-municipio.md).
- **IPM:** o tomador cadastra a IM no município, recebe login/senha e cria um **novo endereço** no InvoiCy com essas credenciais. Ver [observações do IPM](../../02-observacoes/observacoes-provedores.md#ipm).
- Notificações por Webhook: [Webhook de NFS-e](webhook-nfse.md).

## Leitura de e-mail e OCR

- **Leitura de e-mail:** XML recebido numa caixa cadastrada é importado se a extensão estiver ativa para o prestador ou o tomador.
- **InvoiCy OCR:** importa a DANFSE em PDF de padrões treinados. A leitura **não é 100% precisa**.
