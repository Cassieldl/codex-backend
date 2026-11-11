# Simples Nacional: NFS-e pelo Emissor Nacional a partir de 01/11/2026

> Fonte: [NFS-e informa: diretrizes sobre a Obrigatoriedade da Emissão da NFS-e Nacional para Optantes do Simples Nacional](https://www.gov.br/nfse/pt-br/noticias/nfs-e-informa-diretrizes-sobre-a-obrigatoriedade-da-emissao-da-nfs-e-nacional-para-optantes-do-simples-nacional), Portal da NFS-e (gov.br), publicada em 29/10/2026.

> [!RESUMO]
> A partir de 01/11/2026, as Microempresas (ME) e Empresas de Pequeno Porte (EPP) optantes do Simples Nacional emitem a NFS-e **exclusivamente pelo Emissor Nacional** (Web ou API), mesmo em município com emissor próprio.
> O que decide onde a nota é emitida é a **data de competência** do serviço, não a data de emissão.

## Quem é obrigado

- ME e EPP optantes do Simples Nacional.
- A regra vale **independentemente** de o município ter emissor próprio e de o contribuinte constar ou não no cadastro municipal.
- Contribuinte de município que já usava a emissão nacional: o fluxo **não muda**.

## Onde emitir, pela competência

| Competência do serviço | Onde emitir | O que acontece |
| :--- | :--- | :--- |
| **A partir de 01/11/2026** | Emissor Nacional (Web ou API) | O sistema do município com emissor próprio deve **bloquear** a emissão. Se não bloquear e enviar a nota ao Ambiente de Dados Nacional (ADN), o arquivo é **rejeitado automaticamente** por uma regra de layout. |
| **Anterior a 01/11/2026** | Sistema do município | O município continua autorizando a emissão e enviando os dados ao ADN. O Emissor Nacional **barra emissão retroativa** para município com sistema próprio. |

## Exemplo

Serviço com competência em **30/10/2026**, nota emitida em **05/11/2026**: a competência é anterior a 01/11/2026, então a nota sai pelo **sistema do município**. O Emissor Nacional não aceitaria essa emissão retroativa.

Serviço com competência em **03/11/2026**: a nota sai pelo **Emissor Nacional**, mesmo que o município tenha emissor próprio.

## Papel do município com emissor próprio

- **Competências novas** (a partir de 01/11/2026): o município passa a ser apenas recebedor das notas geradas no ambiente nacional.
- **Competências antigas:** a prefeitura continua autorizando e compartilhando os dados.

> [!ATENCAO]
> Notas de competência anterior a 01/11/2026 ainda dependem do sistema do município. Antes de mudar a emissão da empresa para o Padrão Nacional, confira se ainda há notas dessas competências para emitir.

## Ver também

- [Migrar o emissor para o Padrão Nacional](migracao-padrao-nacional.md): como trocar a versão do município no InvoiCy.
- Emissor Nacional (Web): <https://www.nfse.gov.br/EmissorNacional>
