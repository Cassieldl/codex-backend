## Dueto

- **Layout:** [Dueto](../01-layouts/layouts-provedores.md#dueto)
- **Fontes:** [obs]; [portal antigo - Dueto](https://desenvolvedores-antigo.migrate.info/2014/04/29/padrao-dueto) e [Dueto 2.0](https://desenvolvedores-antigo.migrate.info/2015/11/19/padrao-dueto-2-0/)

### Ambiente de homologação

- **Itaperuna/RJ** e **Rolândia/PR** não têm homologação. Em **Cândido Mota/SP**, peça autorização no ISS online da prefeitura.

### Cancelamento

- **Cândido Mota/SP:** o decreto municipal dá **0 dias para cancelamento**; o webservice sempre recusa ("O prazo para Cancelamento do RPS/NFS-e expirou"). Para cancelar, contate a prefeitura.

  ![Confirmação do prazo de cancelamento](../assets/imagens/dueto/confirmacao-do-prazo-de-cancelamento.png)

- **Catanduva/SP:** sem cancelamento via webservice.

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional).
- **Retenções federais:** segue a NT 007: PIS, COFINS e CSLL retidos somados em `ValCSLL`. Ver [Retenções federais](../04-base-conhecimento/artigos/retencoes-federais.md). [validado]
- **Não aceita quebra de linha.** [obs]
- **Alíquota:** informe só quando o ISSQN for devido a outro município, ou quando o prestador for do Simples com retenção ou permissão de editar a alíquota. Fora disso o RPS é rejeitado.
- **Responsável pela retenção:** só quando o ISS for retido; caso contrário rejeita.
- Simples Nacional com município igual ao de incidência: o valor do ISS enviado é ignorado.
- **Tomador estrangeiro (Eldorado do Sul):** cadastre o tomador no sistema da prefeitura e envie o código gerado em `DocTomadorEstrangeiro`.

### Links

- XML de exemplo (Dueto 2.0): <https://migrate.info/desenvolvedores/web/BR/InvoiCy/NFSe/documentos/Exemplos/PadraoDUETO2.xml>
