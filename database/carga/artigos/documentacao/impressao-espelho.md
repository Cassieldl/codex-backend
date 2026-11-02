# Impressão e espelho do RPS/NFS-e

> Fontes: [Impressão de NFS-e](https://desenvolvedores.migrate.info/Sistema/PaineldeControle/Impressao/NFSe/) (17/07/2026) e [Gerar Espelho do RPS e da NFS-e](https://desenvolvedores.migrate.info/Sistema/PaineldeControle/Impressao/NFSe/GerarEspelhodoRPSedaNFSe/) (13/02/2026).

## RPS × NFS-e no espelho

- **Nota não efetivada:** o espelho sai como **RPS**, com aviso de que **não vale como comprovante**.
- **Nota efetivada:** sai como **NFS-e**, desde que *Configurações para Emissão → "Imprime NFS-e se efetivada" = Sim*.
- Alguns municípios só permitem imprimir **notas efetivadas** (**inv0331**).

## Como gerar

*Documentos Emitidos* ou *Documentos Recebidos* → coluna **Ações** → **Impressão do documento**. É gerado um PDF.

## Configuração (bloco "Espelho")

Em *Painel de Controle → NFS-e → Configurações para Emissão → Espelho*:

| Opção | Uso |
| :--- | :--- |
| Modelo de Espelho | Três modelos; mudam a disposição das informações. |
| Exibição | Itens, retenções federais, NFS-e efetivada, parcelas e canhoto. |
| Preencher CNAE Ausente | Completa o CNAE na impressão. |
| Discriminação na Impressão do Cupom Fiscal | Detalha ou não os impostos incidentes. |
| Imagem do Rodapé | Upload de imagem para o rodapé. |
| Nome do Tomador no Canhoto | Exibe ou não o nome do tomador no canhoto. |

## Canhoto

Opção **"Imprime Canhoto"** no cadastro da empresa: **Cabeçalho**, **Rodapé** ou **Não Imprimir**.

O canhoto pode sumir conforme o tamanho da discriminação:

| Documento | Canhoto na mesma página | Canhoto oculto | Quebra de página (canhoto volta) |
| :--- | :--- | :--- | :--- |
| RPS | até 6 linhas | 7 a 16 linhas | a partir de 17 linhas |
| NFS-e | até 13 linhas | 14 a 20 linhas | a partir de 21 linhas |

## Impressão no modelo da prefeitura

Alguns padrões disponibilizam o PDF da própria prefeitura (configurável em *Avançado*, "link de impressão da prefeitura"). Outros não (ex.: CENTI 2, Tributos Municipais, SMARAPDSil WS2). Ver [observações por provedor](../../02-observacoes/observacoes-provedores.md).
