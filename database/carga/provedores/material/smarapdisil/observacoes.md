## SMARAPDISIL

- **Layout:** [SMARAPDISIL](../01-layouts/layouts-provedores.md#smarapdisil)
- **Fontes:** [obs]; [portal antigo - SMARAPD SIL WS](https://desenvolvedores-antigo.migrate.info/2014/07/09/padrao-smarapd-sil-tecnologia-ws) e [WS2](https://desenvolvedores-antigo.migrate.info/2018/03/14/padrao-smarapdsil-ws2)

### Origem da rejeição

- Erro que começa com **"Rejeitado pelo Emissor Nacional"** veio do Emissor Nacional; os demais vieram da prefeitura. [obs] Ver [Origem da rejeição](../04-base-conhecimento/artigos/origem-da-rejeicao.md).

### Versões anteriores (WS e WS2)

As regras abaixo são do [portal antigo] e valem para municípios que ainda usam essas versões:

- **Credenciais:** Usuário Autent. = **CNPJ**; Senha Autent. = senha da prefeitura, **com letras em maiúsculas** (`abc123` → `ABC123`).
- **Série fixa "7"**; tomador obrigatório; sem intermediário (WS).
- `dEmis` não pode ser menor que a data da última NFS-e efetivada.
- `LocalPrestServ` obrigatório: `1` tributação no município · `2` fora.
- **Sem homologação** (WS). **Nova Odessa/SP:** sem cancelamento via webservice.
- **Status em conflito:** timeouts frequentes impedem capturar o protocolo. Consulte no site da prefeitura (CNPJ do prestador, número do RPS e CPF/CNPJ do tomador) e atualize o registro pelo webservice de correção ou pela tela de suporte.

  ![Consultar nota fiscal SMARAPD](../assets/imagens/smarapdisil/consultar-nota-fiscal-smarapd.png)
  ![Nota fiscal localizada](../assets/imagens/smarapdisil/nota-fiscal-localizada.png)

- **WS2:**
  - Intermediário precisa ser contribuinte cadastrado.
  - Exportação: municípios em branco, país do serviço e do tomador, natOp de exportação.

  ![Natureza da operação SMARAPDSil WS2](../assets/imagens/smarapdisil/natureza-da-operacao-smarapdsil-ws2.jpg)

### Artigos relacionados

- [E1235 - Falha no esquema XML](../04-base-conhecimento/artigos/e1235-falha-esquema-xml.md)
- [Cálculo do valor líquido (vLiq)](../04-base-conhecimento/artigos/calculo-valor-liquido.md)
- [NT 007 - retenções de PIS, COFINS e CSLL](../04-base-conhecimento/artigos/nt007-retencoes-federais.md)

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional). Na versão ABRASF vale a tabela própria.
