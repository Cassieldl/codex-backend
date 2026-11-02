# Solicitar a integração de um novo município

> Fonte: [Solicitando um Novo Município](https://desenvolvedores.migrate.info/Sistema/PaineldeControle/CadastrodeEmpresa/SolicitandoumNovoMunicipio/) (28/01/2026).

## 1. Confirme que o município não está integrado

- Pela tela: *Painel de Controle → NFS-e → Municípios Integrados* (filtros e mapa).
- Pelo webservice: [Consultas sobre o município](consultas-municipio.md).
- Ou peça a lista ao seu gerente de contas.

Mensagem quando não está integrado: **inv0302 - Este município ainda não está preparado para emitir NFS-e**.

## 2. Verifique se a integração é possível

- O município precisa oferecer **webservice/API**. Muitos só permitem digitação no site, o que inviabiliza a integração. Confirme com a secretaria de finanças ou com outros prestadores da cidade.
- É preciso uma **empresa piloto** domiciliada no município e obrigada a emitir. A Migrate **não integra só pelo manual**: os manuais costumam divergir do sistema real.
- A empresa piloto precisa:
  - Estar habilitada na **homologação** da prefeitura (se houver).
  - Ter **certificado A1**.
  - Ter a **permissão para emitir via webservice**, se o município exigir (causa comum de atraso).

## 3. Preencha os dados da empresa piloto

| Dados |  |
| :--- | :--- |
| CNPJ | Nome fantasia |
| Razão social | Endereço |
| Cidade/UF | Bairro |
| CEP | Complemento |
| DDD/telefone | CMC (Cadastro Mobiliário de Contribuinte) |
| Inscrição municipal | Inscrição estadual (se tiver) |
| Usuário do portal da prefeitura | Senha do portal da prefeitura |
| Certificado digital A1 | Senha do certificado |
| Códigos de tributação do município habilitados | Itens da lista de serviços habilitados |
| CNAE | Alíquota de ISS |
| Estimativa de notas (dia, semana ou mês) | Data de exigência legal para começar a emitir |

Se tiver links e manuais de integração do município, envie junto. Indique também um **contato responsável pela NFS-e** no parceiro.

## 4. Envie a solicitação

- Pelo **gerente de contas**, com o município e o formulário; ou
- Por e-mail para **integracao@migrate.info**, assunto: *"Solicitação de novo município na NFS-e do InvoiCy, Nome Município – Estado"*.

## Prazo

A solicitação é **validada por um analista** (documentação e testes de envio) e então entra na **fila de integração**, atendida nas sprints mensais conforme a capacidade. Quando priorizada, a data de liberação é informada ao parceiro.

> Sistemas municipais não são padronizados e às vezes têm problemas que dependem da prefeitura para resolver. Alinhe as expectativas com o cliente final e evite contratos atrelados a prazo de integração.
