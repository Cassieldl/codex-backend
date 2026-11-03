# Processamento síncrono × assíncrono

> [!RESUMO]
> Existem dois "assíncronos": o da **prefeitura** (devolve protocolo e processa depois) e o do **InvoiCy** (o RPS entra numa fila).
> Prefeitura assíncrona: aguarde e consulte, ou use Webhook. Não reenvie antes de saber o status.
> Use Assíncrono no InvoiCy para padrões sequenciais e grandes volumes.

## Os dois níveis

É comum confundir os dois:

| Nível | Síncrono | Assíncrono |
| :--- | :--- | :--- |
| **Prefeitura** (como o município responde) | Devolve a NFS-e ou a rejeição na mesma requisição. | Devolve um **protocolo** e processa depois. |
| **InvoiCy** (Tipo de processamento da empresa) | O InvoiCy envia na hora; o ERP controla a ordem. | O RPS entra numa **fila** do InvoiCy, que controla envio, sequência e consultas. |

O Tipo de processamento fica em *Configurações para emissão → Ambientes*. Ver [Configurações de emissão](configuracoes-de-emissao.md).

### Prefeitura síncrona

```fluxo
Seu sistema: envia o RPS ao InvoiCy
InvoiCy: repassa à prefeitura
Prefeitura: devolve a NFS-e ou a rejeição na mesma requisição
```

### Prefeitura assíncrona

```fluxo
Seu sistema: envia o RPS ao InvoiCy
InvoiCy: repassa à prefeitura
Prefeitura: devolve um protocolo e processa depois
Seu sistema: aguarda alguns segundos e consulta o documento, ou recebe o status por Webhook
```

## Quando a prefeitura é assíncrona

- Aguarde alguns segundos e **consulte o documento**, ou use **Webhook** para receber o status.
- Na maioria dos padrões **não é preciso** mudar o Tipo de processamento da empresa para Assíncrono.
- Para não depender de consultas, use o [Webhook de NFS-e](webhook-nfse.md).

> [!ATENCAO]
> **Não reenvie** antes de confirmar o status. O reenvio gera novos lotes, dificulta obter o retorno do primeiro envio, aumenta a fila e pode causar duplicidade ou "RPS já informado".

> [!IMPORTANTE]
> **Exceção (WebISS):** para receber o status por **Webhook**, o Tipo de processamento precisa ser **Assíncrono**.

> [!DICA]
> **Início e fim do mês** concentram volume. Em prefeituras assíncronas (ex.: Fortaleza, Recife, Belo Horizonte), a autorização pode levar **horas**. Exemplos de síncronas: Rio de Janeiro, São Paulo, Brasília. [[Perguntas Frequentes](https://desenvolvedores.migrate.info/Geral/PerguntasFrequentes/), 22/09/2026]

## Quando usar Assíncrono no InvoiCy

- Padrões **sequenciais** (CONAM, DSFNET, alguns IPM/WebISS): a fila do InvoiCy garante a ordem. Ver [Emissão sequencial](emissao-sequencial-rps.md).
- **Grandes volumes** faturados de uma vez (evita **inv0318**, limite de documentos por conexão).
- Padrões que exigem por regra (ex.: Equiplano, pelo portal antigo).

Com Assíncrono, o caminho do RPS fica assim:

```fluxo
Seu sistema: envia o RPS ao InvoiCy
Fila do InvoiCy: recebe o RPS (inv0346) e controla envio, sequência e consultas
Prefeitura: recebe e processa (inv0337, Em Processamento)
Fila do InvoiCy: retira o RPS (inv0354), processado ou rejeitado após 3 tentativas
```

## Sinais no retorno

| Sinal | Significado |
| :--- | :--- |
| **inv0346**: inserido na fila assíncrona (PENDENTE ENVIO/CONSULTA/CANCELAMENTO) | O InvoiCy recebeu e vai enviar. Consulte em intervalos de minutos. |
| **inv0337**: Em Processamento | A prefeitura recebeu e ainda processa. Se continuar assim por horas, acione o suporte. |
| **inv0354**: saiu da fila | Processado, ou rejeitado após 3 tentativas. |
| Protocolo sem número de NFS-e | Ainda não autorizado. |
| "DPS recebida com sucesso" | O **município** recebeu; o ADN ainda não autorizou. |
| `em_processamento`, `AGUARDANDO_PROCESSAMENTO` | Em processamento (segundos, em geral). |

Ver [invoicy-inv03xx.md](../../03-rejeicoes/invoicy-inv03xx.md).

## Por provedor

> [!DETALHES] Comportamento de cada provedor
> | Provedor | Comportamento |
> | :--- | :--- |
> | Betha | Assíncrono: lote com protocolo. |
> | DSFNET | Assíncrono em alguns municípios; Belém/PA pode levar minutos. |
> | E&L | Assíncrono em algumas prefeituras; sucesso = DPS recebida pelo município. |
> | e-Governe ISS | Síncrono; não há serviço de consulta. |
> | Elotech, Governo Digital, IPM, RLZ | Tempo real. |
> | Equiplano, GISS, Nota Salvador | Assíncrono. |
> | Fiorilli | Varia; Holambra/SP sempre assíncrono. |
> | GESPAM | Recepção síncrona + processamento assíncrono. |
> | Sigcorp | Assíncrono, com status próprios. |
> | TINUS (Jaboatão/PE) | Lotes processados a cada meia hora. |
> | TIPLAN | Síncrono; se o ADN falhar, cai para assíncrono com protocolo. |
> | WebISS | Algumas prefeituras assíncronas. |

## Lotes

- No **InvoiCy**, cada RPS do lote é independente: um rejeitado não invalida os demais. Ver [Emissão em lote](emissao-em-lote.md).
- Na **prefeitura**, alguns lotes são **transacionais** (TIPLAN: um RPS inválido rejeita o lote inteiro). Limite comum: **50 RPS**.

Detalhes em [observações por provedor](../../02-observacoes/observacoes-provedores.md).

## Fontes

- [Perguntas Frequentes](https://desenvolvedores.migrate.info/Geral/PerguntasFrequentes/) (22/09/2026)
