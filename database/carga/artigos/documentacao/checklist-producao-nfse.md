# Checklist para emitir NFS-e em produção

> Fonte: [Verifique se sua Empresa está Apta para Emitir em Produção](https://desenvolvedores.migrate.info/Sistema/Ambientes/VerifiquesesuaEmpresaestaAptaparaEmitiremProducao/) (16/02/2026).

Antes de ir para produção, leia a documentação do módulo NFS-e e as [observações do provedor](../../02-observacoes/observacoes-provedores.md) do município. Testes mínimos em homologação:

## Testes obrigatórios

- [ ] Cadastrar **mais de uma empresa**.
- [ ] Gerar o **XML de envio**.
- [ ] **Autorizar** um documento.
- [ ] **Consultar** um documento.
- [ ] **Cancelar** um documento.
- [ ] Emitir **com itens** (`ListaItens` e grupo `Servico` preenchidos corretamente).
- [ ] Emitir um **lote com 5 RPS ou mais**. Ver [Emissão em lote](emissao-em-lote.md).
- [ ] Seguir o fluxo **síncrono e assíncrono**. Ver [Processamento](processamento-sincrono-assincrono.md).
- [ ] Padrões por **arquivo** (TXT): seguir o procedimento específico do padrão.

## Testes conforme o cliente

Teste os cenários reais de cada cliente, por exemplo:

- Com **ISS retido** e **sem ISS retido**.
- Com os **códigos de tributação reais** da empresa no município, simulando o dia a dia.
- Tomador estrangeiro, construção civil, deduções, se o cliente usar.

Essas regras de negócio são de responsabilidade do parceiro, que deve testá-las e aprová-las no ERP.

## Se o município não tem homologação

Mensagem **inv0303**. Teste em produção com **valores baixos** e **cancele** as notas, ou peça à prefeitura um usuário de testes. Ver [invoicy-inv03xx.md](../../03-rejeicoes/invoicy-inv03xx.md).
