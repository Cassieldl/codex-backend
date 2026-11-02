# Emissão de NFS-e por decisão judicial

> Fonte: [Emissão de NFS-e por Decisão Judicial](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/EmissaoJudicial/) (14/07/2026).

## Resumo

Modalidade do **Padrão Nacional** para notas determinadas por **ordem judicial ou administrativa**. O ambiente nacional marca a nota com a situação **102 – NFS-e de Decisão Judicial** e **flexibiliza validações** que rejeitariam uma nota normal (alíquotas fora do padrão, deduções vedadas, regimes especiais), respeitando o processo.

No InvoiCy, a emissão segue **o mesmo fluxo e endpoint** da NFS-e normal, com **três campos a mais**. O InvoiCy cuida do direcionamento e da marcação da situação 102.

## Pré-requisito

O **município do prestador** precisa ter cadastrado, na plataforma nacional, os parâmetros da decisão. Sem isso, a emissão é rejeitada mesmo com o documento correto.

Para conferir: no [Portal do Contribuinte da NFS-e Nacional](https://www.nfse.gov.br/EmissorNacional/Login), entre com certificado e acesse *Tipos de emissão → Emissão por decisão administrativa/judicial*. Se o CNPJ/CPF não tiver registro ativo, o portal informa que a modalidade não está disponível.

## Campos adicionais

| Campo | Descrição | Obrigatório |
| :--- | :--- | :--- |
| `nNFSeJudicial` | Número da NFS-e informado pelo emitente (não é gerado pelo ambiente nacional). | Sim |
| `NumProcesso` | Número do processo judicial ou administrativo. | Sim |
| `tpSusp` | Tipo de suspensão da exigibilidade: `1` decisão judicial · `2` processo administrativo. | Sim |

A **presença dos três campos** é o que sinaliza a emissão judicial. Não há endpoint nem parâmetro extra.

## Numeração

- **ERP controla a numeração:** envie `RPSNumero`, `RPSSerie` e `nNFSeJudicial`.
- **InvoiCy controla a numeração:** envie `IDUnico` e `nNFSeJudicial`. Ver [Controle de numeração do RPS](controle-numeracao-rps.md).
- O ERP é responsável por `nNFSeJudicial` não colidir com notas existentes, sob risco de rejeição.

## Pontos de atenção

- **Alíquota do ISSQN:** obrigatória e igual ao percentual determinado no processo.
- **IBS/CBS:** obrigatório para o **regime regular desde 03/08/2026**. Para o **Simples Nacional**, só a partir de **janeiro/2027**: até lá, enviar as tags de IBS/CBS em nota judicial de optante do Simples pode gerar o erro genérico **RNG9999 – Erro não catalogado**.

## Exemplos

- [XML](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Layout%20Integra%C3%A7%C3%A3o%20-%20Reforma%20Tribut%C3%A1ria/Exemplos%20XML/NFSe/Emissao_NFSe_Judicial.xml) · [JSON](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Layout%20Integra%C3%A7%C3%A3o%20-%20Reforma%20Tribut%C3%A1ria/Exemplos%20JSON/NFSe/Envio_NFSe_Judicial.json)

```xml
<RPS>
  <RPSNumero>5462</RPSNumero>
  <RPSSerie>770</RPSSerie>
  <RPSTipo>1</RPSTipo>
  <nNFSeJudicial>1</nNFSeJudicial>
  <NumProcesso>000000000023564521368974658963</NumProcesso>
  <tpSusp>1</tpSusp>
  ...
</RPS>
```

## Depois da emissão

A nota se comporta como uma NFS-e normal: consulta, cancelamento e Webhook pelos mesmos métodos.
