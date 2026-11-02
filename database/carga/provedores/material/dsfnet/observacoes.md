## DSFNET

- **Layout:** [DSFNET](../01-layouts/layouts-provedores.md#dsfnet)
- **Fontes:** [obs]; [portal - DSFNET](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/DSFNET/) (07/04/2026); [portal antigo - DSFNET](https://desenvolvedores-antigo.migrate.info/2022/01/18/padrao-dsfnet/)

### Autenticação e acesso

- Exige **certificado A1** (assinatura do RPS).
- **Usuário Autenticação** = CPF/CNPJ de um **subusuário ativo** na prefeitura (*Minha Empresa → Vincular Subusuários*). Recomenda-se certificado com o mesmo CPF/CNPJ do subusuário.

### Processamento

- Abrange DSFNET ABRASF, Sequencial e não sequencial.
- **Assíncrono** em algumas prefeituras; em Belém/PA pode levar minutos. Consulte depois em intervalos.
- **Sequencial:** enquanto houver rejeição, as notas seguintes não são processadas. Ver [Emissão sequencial de RPS](../04-base-conhecimento/artigos/emissao-sequencial-rps.md).

### Cancelamento

- Cancelar um RPS **não reaproveita** o número: se o RPS 5 for cancelado, o próximo é o 6.

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional).
- **Retenções federais:** segue a NT 007: PIS, COFINS e CSLL retidos somados em `ValCSLL`. Ver [Retenções federais](../04-base-conhecimento/artigos/retencoes-federais.md). [validado]
- **Série:** em produção, use série entre **10001 e 49999** (ex.: `SerieRps=10001`); fora da faixa ocorre **L0022**. [obs, validado]
- **Telefone:** sempre com **DDD de 2 dígitos** (ex.: `5535354800`), no tomador e no prestador. O InvoiCy separa os dois primeiros dígitos como DDD.
- **Código de município:** a prefeitura usa o código **SIAFI** (Receita Federal), mas no InvoiCy informe o **IBGE**; o InvoiCy converte.
- **Intermediário:** só Sorocaba/SP e Campo Grande/MS; nesse caso `Operacao = J`.
- **`Operacao` (obrigatória):** `A` sem dedução · `B` com dedução/materiais · `C` imune/isenta · `D` devolução/simples remessa · `J` intermediação.
- **Dedução:** `DedCNPJRef`, `DedCPFRef`, `DedmNFRef`, `DedvlTotRef` só quando a atividade permitir dedução por materiais (`Operacao = B`).
- **Inscrição Municipal:** o tamanho varia por município; o InvoiCy completa com zeros à esquerda.

  ![Tamanho da IM por município](../assets/imagens/dsfnet/tamanho-da-im-por-municipio.png)

- **Discriminação:** `ItemDesc` até **250** caracteres (o excedente é cortado); `Discriminacao` até **1500**. Com um só item e sem `ItemDesc`, usa os 250 primeiros caracteres de `Discriminacao`.
- **Itens:** no máximo **25** por RPS; ao menos um precisa ser tributável. [portal antigo]
- **`ItemTributavel`:** `N` para atividades da lista abaixo; `S` para as demais. [portal antigo]

  ![Item tributável DSFNET](../assets/imagens/dsfnet/item-tributavel-dsfnet.jpg)
  ![Atividades com item não tributável](../assets/imagens/dsfnet/atividades-com-item-nao-tributavel.jpg)
