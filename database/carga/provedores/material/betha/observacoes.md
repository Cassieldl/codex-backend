## Betha

- **Layout:** [Betha](../01-layouts/layouts-provedores.md#betha)
- **Rejeições:** [betha.md](../03-rejeicoes/betha.md)
- **Fontes:** [obs]; [portal antigo - Betha](https://desenvolvedores-antigo.migrate.info/2014/04/29/padrao-betha) e [Betha 2.0](https://desenvolvedores-antigo.migrate.info/2016/10/10/padrao-betha-2-0) (versões antigas, ABRASF; só valem onde indicado)

### Visão geral

- O Betha atual é **leiaute nacional** (DPS), não ABRASF. Valem as regras do [Emissor Nacional](#emissor-nacional) e os códigos de rejeição da DPS em [betha.md](../03-rejeicoes/betha.md). [validado]
- Conteúdo do **Betha 1.0** e do **Betha 2.0 (ABRASF)**, quando aparece abaixo, está marcado como **legado**.

### Autenticação e acesso

- Antes de emitir, confira no portal da prefeitura se a **Inscrição Municipal** está preenchida. Sem ela ocorre **E43** ("Inscrição Municipal do prestador não encontrada"). Se o campo estiver bloqueado para edição, peça ao suporte da prefeitura.

  ![Inscrição municipal no cadastro Betha](../assets/imagens/betha/inscricao-municipal-no-cadastro-betha.jpg)

- "Emissor não habilitado": peça ao fiscal da prefeitura para mudar o tipo de emissão para **ambos** (digitação e webservice).
- **Faixa de RPS:** o prestador precisa solicitar uma faixa de numeração. No Betha online: *Menu RPS → Autorização para impressão de RPS* (quantidade, série e motivo). Um fiscal defere ou indefere; acompanhe em *Menu RPS → Relatório de autorizações para impressão de RPS*. Depois, use a faixa e a série liberadas.

### Ambiente de homologação e produção

O webservice usa **a mesma URL** para homologação e produção. O ambiente ativo é escolhido no portal Betha (<https://nota-eletronica.betha.cloud>):

1. Entre como contribuinte e, no menu à esquerda, abra **Ambiente de Homologação → Ativar Ambiente de Homologação**.

   ![Ativar ambiente de homologação](../assets/imagens/betha/ativar-ambiente-de-homologacao.png)

2. Confirme com **Sim**. Em homologação, os menus ficam reduzidos e os dados vão para um banco de testes, sem efeito fiscal.

   ![Confirmação da homologação](../assets/imagens/betha/confirmacao-da-homologacao.png)

3. Para voltar à produção, abra de novo **Ambiente de Homologação → Ativar Ambiente Oficial** e confirme. A homologação é desativada automaticamente.

   ![Ativar ambiente oficial](../assets/imagens/betha/ativar-ambiente-oficial.png)
   ![Confirmação do ambiente oficial](../assets/imagens/betha/confirmacao-do-ambiente-oficial.png)

### Processamento

- **Assíncrono:** recebe o lote de RPS, devolve protocolo e processa depois. Aceita lote e não exige envio sequencial. [obs]
- O webservice **não permite reenviar RPS rejeitado**. [obs]
- **Chapecó** e **Lagoa Vermelha**: numeração de RPS **sequencial crescente**. [portal antigo]

### Cancelamento

- **Maracás/BA** não cancela via webservice (mensagem BTH09); cancele no portal do município.

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional).
- **Obrigatórios (DPS):** código de tributação nacional (E031), código NBS (E033), local de prestação (E034) e tributação do ISSQN (E042). [obs]
- `ValorDeducoesServico`, `DescontoCondicionadoServico` e `DescontoIncondicionadoServico` são usados só no envio, não na impressão. [obs]
- **Simples Nacional:** enviar conforme o cadastro do prestador no portal (senão E146). [obs]
- **Discriminação:** os itens do RPS são concatenados em `<Discriminacao>`, limitada a **2000 caracteres**. Com muitos itens, divida em mais de uma nota.
- **ISS retido:** o campo `ISSRetido` do InvoiCy alimenta `tpRetISSQN` (1 não retido, 2 retido pelo tomador, 3 retido pelo intermediário). A opção "3 – Substituição Tributária" era do **Betha 1.0 (legado)**.
- **CNAE:** só quando a legislação do município permitir; senão, em branco.
- **Tomador do exterior:** sem CPF/CNPJ e sem CEP; código do município vazio no tomador e no serviço; país da prestação no grupo do serviço. Exportação: `natOp = 9` (vai como `tribISSQN = 3`). Pendente: a regra antiga citava exigibilidade ISS = 4 (ABRASF).
- **Regime especial de tributação e forma de pagamento:** Pendente. Os prints abaixo vêm do portal antigo (Betha 2.0, ABRASF) e podem não valer para o leiaute nacional. A natureza da operação **não** segue o primeiro print: vale a tabela do Padrão Nacional.

  ![Natureza da operação ABRASF (legado)](../assets/imagens/betha/natureza-da-operacao-abrasf.jpg)
  ![Regime especial de tributação ABRASF (legado)](../assets/imagens/betha/regime-especial-de-tributacao-abrasf.jpg)
  ![Forma de pagamento Betha (legado)](../assets/imagens/betha/forma-de-pagamento-betha.jpg)

### Reforma Tributária (IBS/CBS)

- Atualizado conforme a **NT004** publicada pelo padrão em 18/06/2026 e adequado à **NT007**. [obs]
- A emissão com IBS/CBS **depende de parametrização da prefeitura** (senão ocorre E999). Ver [IBS/CBS e parametrização da prefeitura](../04-base-conhecimento/artigos/ibs-cbs-parametrizacao-prefeitura.md).

### Links

- Exemplo de XML (Betha 2.0, legado ABRASF): <https://migrate.info/desenvolvedores/web/BR/InvoiCy/NFSe/documentos/Exemplos/PadraoBetha20.txt>
