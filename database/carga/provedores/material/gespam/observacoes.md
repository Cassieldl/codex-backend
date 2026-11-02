## GESPAM

- **Layout:** [GESPAM](../01-layouts/layouts-provedores.md#gespam)
- **Rejeições:** [gespam.md](../03-rejeicoes/gespam.md)
- **Fontes:** [obs]; [portal antigo - GESPAM](https://desenvolvedores-antigo.migrate.info/2016/11/18/padrao-gespam)

### Autenticação e acesso

- **ApiKey** por ambiente: a de homologação não funciona em produção.
- A chave só vale depois de **"Confirmar Uso da Chave"**. Uma nova chave **invalida a anterior na hora** (HTTP 401). É exibida **uma única vez**; não registre em logs.
- Para usar homologação e produção, faça **solicitação formal à prefeitura** (CNPJ e IM). [portal antigo]

### Processamento

- **Recepção (síncrona):** valida XSD, autenticação, cadastro, série, janela de competência e regras básicas. Erros voltam em HTTP 400 e nenhuma NFS-e é gerada.
- **Processamento (assíncrono):** gera a NFS-e e valida no ADN. Erros aparecem nas consultas como `PROCESSADO_COM_ERROS`.
- `AGUARDANDO_PROCESSAMENTO` costuma levar segundos; acima de 30 minutos, acione o suporte com o protocolo.

### Limites (produção)

- **100 requisições/minuto por IP** (acima disso, HTTP 429). Corpo de até **10 MB**. Lote de até **50 DPS**.

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional).
- **Retenções federais:** segue a NT 007: PIS, COFINS e CSLL retidos somados em `ValCSLL`. Ver [Retenções federais](../04-base-conhecimento/artigos/retencoes-federais.md). [validado]
- Série **só numérica** no padrão nacional; confirme as séries liberadas.
- ApiKey de contribuinte só emite para o próprio CNPJ (C0011); para vários CNPJs, peça uma ApiKey de ERP.
- **Não permite** (versão ABRASF): RPS sem endereço completo do tomador, **tags em branco**, desconto de 100%. [portal antigo]
- **Outras informações:** não há tag; marque **"Concatenar Discriminação e Outras Informações" = Sim**. [portal antigo]

  ![Concatenar discriminação e outras informações](../assets/imagens/gespam/concatenar-discriminacao-e-outras-informacoes.png)
