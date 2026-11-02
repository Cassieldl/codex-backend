## RLZ

- **Layout:** [RLZ](../01-layouts/layouts-provedores.md#rlz)
- **Fontes:** [obs]; [portal - RLZ](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/RLZ/) (07/04/2026); [portal antigo - RLZ](https://desenvolvedores-antigo.migrate.info/2015/09/21/padrao-rlz) e [RLZ ABRASF](https://desenvolvedores-antigo.migrate.info/2019/04/05/padrao-rlz-abrasf)

### Autenticação e acesso

- Abrange RLZ (cidadão online), RLZ – Modelo Nacional, RLZ ABRASF e RLZ ABRASF Assíncrono.
- Exige **certificado A1** e **usuário e senha** de autenticação. [portal]
- **Token Web Service:** obtido no menu **Token Web Service** do sistema online do município e informado em **Chave primária de autenticação**.

  ![Token Web Service RLZ](../assets/imagens/rlz/token-web-service-rlz.jpg)

### Ambiente de homologação

- O webservice de homologação precisa ser **criado no sistema da prefeitura**, em área restrita a empresas já cadastradas.
- A RLZ exige **homologar antes** da produção. Inicialmente só **Rancharia/SP** foi inicializado; para outro município, peça ao atendimento Migrate a criação do webservice de homologação.

### Processamento

- Emissão em **tempo real**. Versões sem serviço de consulta podem deixar o documento **em conflito**: confira no portal antes de reenviar. Ver [Status em conflito](../04-base-conhecimento/artigos/status-em-conflito.md).

### Cancelamento

- Alguns municípios **não têm API de cancelamento**: cancele no portal; o InvoiCy marca como cancelado internamente.

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional). Na versão ABRASF vale a tabela própria.
- **Retenções federais:** segue a NT 007: PIS, COFINS e CSLL retidos somados em `ValCSLL`. Ver [Retenções federais](../04-base-conhecimento/artigos/retencoes-federais.md). [validado]
- **Local de prestação:** informe o grupo `<LocalPrestacao>` com `<SerEndCep>`.
- **Intermediário:** não aceito.
- **Tomador:** além do CPF/CNPJ, aceita **RG** (PF) ou **inscrição estadual** (PJ) em `<TomaIE>`. Estrangeiro: passaporte em `<DocTomadorEstrangeiro>`.
- **Série:** recomenda-se a série **B** (notas eletrônicas). [portal antigo]
- **RLZ ABRASF:** não permite tomador não identificado nem valor do serviço em branco. Natureza da operação (3 → 3; 4 → 5; 7 → 1; 9 → 4, [tabela](../04-base-conhecimento/artigos/natureza-da-operacao.md#rlz-abrasf-cidadão-online)):

  ![Natureza da operação RLZ ABRASF](../assets/imagens/rlz/natureza-da-operacao-rlz-abrasf.jpg)

### Reforma Tributária (IBS/CBS)

- Municípios já com os campos da Reforma: **Jales/SP** e **Primavera do Leste/MT** (abril/2026). [portal]
