# Migrar o emissor para o Padrão Nacional

> Fontes: [FAQ - Padrão Nacional](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/FAQ/), [Padrão Emissor Nacional](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/EmissorNacional/) e [Padrões](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/).

## Resumo

Cada vez mais municípios deixam o sistema próprio e passam a emitir pelo **Padrão Nacional** (Emissor Nacional / ADN). Quando isso acontece, a empresa precisa trocar a versão do município no InvoiCy.

## Sintoma

> O estabelecimento informado deverá emitir suas Notas Fiscais de Serviço Eletrônicas (NFS-e) por meio do emissor nacional da NFS-e

## Antes de mudar

Nem todo município migrou: alguns mantiveram o sistema próprio. Se mudar sem necessidade, ocorre:

> O município emissor informado na DPS deve estar parametrizado para utilizar os emissores públicos nacionais

Confira no **site da prefeitura** o comunicado sobre a mudança e qual é o sistema emissor vigente.

## Como mudar pela tela

1. Selecione o emissor no InvoiCy.
2. Acesse *Painel de controle → NFS-e → Configurações para emissão*.

   ![Configurações para emissão](../../assets/imagens/migrar-o-emissor-para-o-padrao-nacional/configuracoes-para-emissao.png)

3. Em **Versão do Município**, selecione **0.20 – NFS-e Nacional**.

   ![Versão do município 0.20](../../assets/imagens/migrar-o-emissor-para-o-padrao-nacional/versao-do-municipio-0-20.png)

4. Salve e **reenvie as notas rejeitadas**.

## Como mudar pela integração

No cadastro de empresa via API/SOAP (`cadEmpresa`), envie a tag **`MEIAmbienteNacional = S`**.

## Depois da migração

- Série **só numérica**.
- Preencha `codTributNacional`, `CodigoNBS`, `cIndOp` e, para Simples ME/EPP, `regApTribSN`.
- Retenções conforme a [NT 007](nt007-retencoes-federais.md).
- Rejeições comuns e soluções: [padrao-nacional.md](../../03-rejeicoes/padrao-nacional.md).

## Envio forçado

Em *Configurações para emissão → Avançado* há a opção de **ativar o envio forçado à NFS-e Nacional**. Há também, em *Particularidades*, a opção de **emissão para MEI em ambiente nacional**.
