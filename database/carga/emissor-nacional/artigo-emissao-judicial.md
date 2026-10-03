A NFS-e por decisão judicial é a modalidade do Emissor Nacional para quando a emissão foi determinada por ordem judicial ou administrativa. O ambiente nacional marca a nota com a situação **102 – NFS-e de Decisão Judicial** e deixa de aplicar boa parte das validações fiscais que a rejeitariam no fluxo normal: o que vale é o que o processo determinou.

No InvoiCy, a emissão segue o **mesmo endpoint e o mesmo leiaute da NFS-e normal**, com três campos a mais. É a presença deles que sinaliza a emissão judicial — não existe endpoint nem parâmetro próprio. O InvoiCy encaminha ao serviço certo do ambiente nacional e cuida da marcação da situação 102.

## Antes de emitir: o município precisa ter cadastrado a decisão

A nota só é autorizada se o município do prestador tiver cadastrado antes, na plataforma nacional da NFS-e, os parâmetros da decisão judicial ou administrativa. Sem esse cadastro o ambiente nacional rejeita a emissão, mesmo com o documento preenchido corretamente.

Para conferir, acesse o Portal do Contribuinte da NFS-e Nacional com o certificado digital e abra **Tipos de emissão → Emissão por decisão administrativa/judicial**. Se o CNPJ ou CPF não tiver registro ativo para a modalidade, o próprio portal informa que a emissão não está disponível.

## Os três campos da emissão judicial

| Campo | O que informar | Obrigatoriedade |
|---|---|---|
| `nNFSeJudicial` | Número da NFS-e, definido pelo emitente | Obrigatório |
| `NumProcesso` | Número do processo judicial ou administrativo que determinou a emissão | Obrigatório |
| `tpSusp` | Tipo de suspensão da exigibilidade | Obrigatório |

Valores aceitos em `tpSusp`:

- `1` — exigibilidade suspensa por decisão judicial;
- `2` — exigibilidade suspensa por processo administrativo.

### A numeração é sua, não do ambiente nacional

No fluxo normal o número da NFS-e é atribuído pela Sefin. No judicial, quem define é o emitente, pelo `nNFSeJudicial`. Controlar a sequência e evitar colisão com notas já existentes fica a cargo do sistema parceiro — número repetido é rejeitado na autorização.

A numeração do RPS segue as regras de sempre, combinada com o `nNFSeJudicial`:

- **numeração controlada pelo sistema parceiro:** informe `RPSNumero`, `RPSSerie` e `nNFSeJudicial`;
- **numeração controlada pelo InvoiCy:** informe `IDUnico` e `nNFSeJudicial`.

Nos dois casos, `NumProcesso` e `tpSusp` são obrigatórios para que o documento chegue ao ambiente nacional.

## Pontos de atenção

**Alíquota do ISSQN.** Na emissão judicial ela é obrigatória e deve ser exatamente o percentual determinado no processo.

**IBS e CBS no Simples Nacional.** Segundo a documentação do InvoiCy de 14/07/2026, a validação dos grupos de IBS/CBS estava suspensa (NT 004), com obrigatoriedade a partir de 3 de agosto de 2026 no regime regular e só em janeiro de 2027 no Simples Nacional. Para emitente do Simples, enviar as tags de IBS/CBS numa nota judicial pode resultar em erro genérico do ambiente nacional (**RNG9999 – Erro não catalogado**). As validações de alíquota de IBS e CBS continuam valendo no fluxo judicial.

## Quando a nota judicial é rejeitada por uma regra de tributação

No fluxo judicial o ambiente nacional deixa de executar mais de 200 regras com código que valem na emissão normal — a maior parte sobre tributação do ISSQN, tributos federais, benefício municipal, regime tributário, comércio exterior, código de serviço e local de incidência. O exemplo mais comum é o **teto de 5% da alíquota do ISSQN** (E0595 e E1300), que não se aplica à nota judicial.

Por isso, se uma nota que deveria ser judicial voltar com uma dessas rejeições, confira primeiro se os três campos foram enviados. Sem qualquer um deles, o InvoiCy trata a nota como emissão normal, e todas as validações voltam a valer.

## Exemplo

O começo do RPS de uma emissão por decisão judicial, com a numeração controlada pelo parceiro:

```xml
<Envio>
    <ModeloDocumento>NFSE</ModeloDocumento>
    <Versao>1.00</Versao>
    <RPS>
        <RPSNumero>5462</RPSNumero>
        <RPSSerie>770</RPSSerie>
        <RPSTipo>1</RPSTipo>
        <nNFSeJudicial>1</nNFSeJudicial>
        <NumProcesso>000000000023564521368974658963</NumProcesso>
        <tpSusp>1</tpSusp>
        <!-- o restante segue igual à emissão normal -->
```

Os exemplos completos, em XML e JSON, estão no repositório de integração citado na [documentação do InvoiCy sobre emissão judicial](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/EmissaoJudicial/).

## Depois de autorizada

A NFS-e por decisão judicial se comporta como uma nota normal. Não há endpoint específico de consulta nem de cancelamento: use os mesmos métodos, endpoints e Webhooks das NFS-e comuns.
