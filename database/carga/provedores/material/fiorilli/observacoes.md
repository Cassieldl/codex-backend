## Fiorilli

- **Layout:** [Fiorilli](../01-layouts/layouts-provedores.md#fiorilli)
- **Fontes:** [obs]; [portal antigo - ISSWEB Fiorilli](https://desenvolvedores-antigo.migrate.info/2014/04/29/padrao-issweb-fiorilli/)

### Autenticação e acesso

- Informe o **usuário e a senha** do portal nos dados da empresa no InvoiCy.

  ![Usuário e senha Fiorilli](../assets/imagens/fiorilli/usuario-e-senha-fiorilli.png)

### Ambiente de homologação

- **Não tem homologação.** Cadastre direto em produção e cancele as notas de teste.

### Processamento

- Cada prefeitura usa uma **versão diferente do webservice**, com comportamentos e retornos distintos. [obs]
- **Holambra/SP** (Fiorilli – Modelo Nacional): sempre **assíncrono**.

### Disponibilidade

- Os sistemas costumam usar **IP como endereço** e mudam com frequência. Se a emissão parar de repente, veja nos logs se há falha de comunicação e compare o endereço do serviço com o do sistema da prefeitura. Se divergir, avise o atendimento (atendimento@migrate.info).

  ![Falha de comunicação no log](../assets/imagens/fiorilli/falha-de-comunicacao-no-log.png)
  ![Endereço do serviço](../assets/imagens/fiorilli/endereco-do-servico.png)

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional).
- **Retenções federais:** segue a NT 007: PIS, COFINS e CSLL retidos somados em `ValCSLL`. Ver [Retenções federais](../04-base-conhecimento/artigos/retencoes-federais.md). [validado]
- Não é possível emitir NFS-e com data **anterior à última** NFS-e emitida.
- **Tomador obrigatório.**
