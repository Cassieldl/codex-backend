# Processamento síncrono × assíncrono

## Resumo

Há **dois níveis** de "assíncrono", e é comum confundi-los:

| Nível | Síncrono | Assíncrono |
| :--- | :--- | :--- |
| **Prefeitura** (como o município responde) | Devolve a NFS-e ou a rejeição na mesma requisição. | Devolve um **protocolo** e processa depois. |
| **InvoiCy** (Tipo de processamento da empresa) | O InvoiCy envia na hora; o ERP controla a ordem. | O RPS entra numa **fila** do InvoiCy, que controla envio, sequência e consultas. |

O Tipo de processamento fica em *Configurações para emissão → Ambientes*. Ver [Configurações de emissão](configuracoes-de-emissao.md).

## Quando a prefeitura é assíncrona

- Aguarde alguns segundos e **consulte o documento**, ou use **Webhook** para receber o status.
- Na maioria dos padrões **não é preciso** mudar o Tipo de processamento da empresa para Assíncrono.
- **Exceção (WebISS):** para receber o status por **Webhook**, o Tipo de processamento precisa ser **Assíncrono**.
- **Não reenvie** antes de confirmar o status: o reenvio gera novos lotes, dificulta obter o retorno do primeiro envio, aumenta a fila e pode causar duplicidade ou "RPS já informado".
- **Início e fim do mês** concentram volume: em prefeituras assíncronas (ex.: Fortaleza, Recife, Belo Horizonte), a autorização pode levar **horas**. Exemplos de síncronas: Rio de Janeiro, São Paulo, Brasília. [[Perguntas Frequentes](https://desenvolvedores.migrate.info/Geral/PerguntasFrequentes/), 22/09/2026]
- Para não depender de consultas, use o [Webhook de NFS-e](webhook-nfse.md).

## Quando usar Assíncrono no InvoiCy

- Padrões **sequenciais** (CONAM, DSFNET, alguns IPM/WebISS): a fila do InvoiCy garante a ordem. Ver [Emissão sequencial](emissao-sequencial-rps.md).
- **Grandes volumes** faturados de uma vez (evita **inv0318**, limite de documentos por conexão).
- Padrões que exigem por regra (ex.: Equiplano, pelo portal antigo).

## Sinais no retorno

| Sinal | Significado |
| :--- | :--- |
| **inv0346** - inserido na fila assíncrona (PENDENTE ENVIO/CONSULTA/CANCELAMENTO) | O InvoiCy recebeu e vai enviar. Consulte em intervalos de minutos. |
| **inv0337** - Em Processamento | A prefeitura recebeu e ainda processa. Se continuar assim por horas, acione o suporte. |
| **inv0354** - saiu da fila | Processado, ou rejeitado após 3 tentativas. |
| Protocolo sem número de NFS-e | Ainda não autorizado. |
| "DPS recebida com sucesso" | O **município** recebeu; o ADN ainda não autorizou. |
| `em_processamento`, `AGUARDANDO_PROCESSAMENTO` | Em processamento (segundos, em geral). |

Ver [invoicy-inv03xx.md](../../03-rejeicoes/invoicy-inv03xx.md).

## Por provedor

| Provedor | Comportamento |
| :--- | :--- |
| Betha | Assíncrono: lote com protocolo. |
| DSFNET | Assíncrono em alguns municípios; Belém/PA pode levar minutos. |
| E&L | Assíncrono em algumas prefeituras; sucesso = DPS recebida pelo município. |
| e-Governe ISS | Síncrono; não há serviço de consulta. |
| Elotech, Governo Digital, IPM, RLZ | Tempo real. |
| Equiplano, GISS, Nota Salvador | Assíncrono. |
| Fiorilli | Varia; Holambra/SP sempre assíncrono. |
| GESPAM | Recepção síncrona + processamento assíncrono. |
| Sigcorp | Assíncrono, com status próprios. |
| TINUS (Jaboatão/PE) | Lotes processados a cada meia hora. |
| TIPLAN | Síncrono; se o ADN falhar, cai para assíncrono com protocolo. |
| WebISS | Algumas prefeituras assíncronas. |

## Lotes

- No **InvoiCy**, cada RPS do lote é independente: um rejeitado não invalida os demais. Ver [Emissão em lote](emissao-em-lote.md).
- Na **prefeitura**, alguns lotes são **transacionais** (TIPLAN: um RPS inválido rejeita o lote inteiro). Limite comum: **50 RPS**.

Detalhes em [observações por provedor](../../02-observacoes/observacoes-provedores.md).
