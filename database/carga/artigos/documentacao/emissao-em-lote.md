# Emissão de NFS-e em lote

> Fonte: [Emissão de NFS-e em Lote](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/EmissaoLote/) (21/07/2026).

## Resumo

O InvoiCy aceita **vários RPS numa só requisição**, com a mesma autenticação e o mesmo layout do envio individual.

## Estrutura do lote

| Formato | Envelope do lote | Nível que se repete | Como adicionar documentos |
| :--- | :--- | :--- | :--- |
| JSON | Array raiz `[...]` | Objeto `Documento` | Novo `{ "Documento": { ... } }` no array |
| XML | Elemento `<Envio>` | Grupo `<RPS>` | Novo `<RPS>` dentro do mesmo `<Envio>` |

```xml
<Envio>
  <ModeloDocumento>NFSE</ModeloDocumento>
  <Versao>1.00</Versao>
  <RPS> <RPSNumero>5477</RPSNumero> <RPSSerie>770</RPSSerie> ... </RPS>
  <RPS> <RPSNumero>5478</RPSNumero> <RPSSerie>770</RPSSerie> ... </RPS>
</Envio>
```

Exemplos completos: [JSON](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Layout%20Integra%C3%A7%C3%A3o%20-%20Reforma%20Tribut%C3%A1ria/Exemplos%20JSON/NFSe/Envio_NFSe_Lote.json) · [XML](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Layout%20Integra%C3%A7%C3%A3o%20-%20Reforma%20Tribut%C3%A1ria/Exemplos%20XML/NFSe/Emissao_NFSe_Lote.xml).

## Processamento

- Cada RPS é validado e autorizado **individualmente**; o retorno traz o resultado de cada um.
- Cada RPS precisa de **número próprio** (`RPSNumero`) dentro da série (`RPSSerie`).
- Um RPS rejeitado **não invalida** os demais do lote no InvoiCy. Exceção: municípios sequenciais ([Emissão sequencial](emissao-sequencial-rps.md)).

## Cuidados

- Lotes grandes demais numa conexão geram **inv0318**; divida o envio ou use processamento assíncrono.
- Alguns provedores limitam o lote do lado da prefeitura (ex.: GESPAM e TIPLAN: até **50**). No TIPLAN, um RPS inválido **rejeita o lote inteiro** na prefeitura.
