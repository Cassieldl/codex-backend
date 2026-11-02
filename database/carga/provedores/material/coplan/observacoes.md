## Coplan

- **Layout:** [Coplan](../01-layouts/layouts-provedores.md#coplan)
- **Rejeições:** [Padrão Nacional](../03-rejeicoes/padrao-nacional.md)
- **Fontes:** [portal - COPLAN](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/COPLAN/) (14/05/2026); [portal antigo - COPLAN](https://desenvolvedores-antigo.migrate.info/2015/12/18/padrao-coplan/)

### Visão geral

- Abrange **COPLAN** e **COPLAN – Modelo Nacional**. Segue **as mesmas regras do Emissor Nacional**. Ver [Emissor Nacional](#emissor-nacional).
- Produção: <https://www.nfse.gov.br/EmissorNacional/Login?ReturnUrl=%2fEmissorNacional>
- Homologação (Produção Restrita): <https://www.producaorestrita.nfse.gov.br/EmissorNacional/Login?ReturnUrl=%2fEmissorNacional>

### Autenticação e acesso

- **E138 - Usuário não autorizado a realizar o serviço:** o prestador não tem permissão para emitir via webservice em produção. Peça a liberação a **equipetributario@coplanmt.com.br** com os dados do prestador. Em **Sinop/MT**, a liberação é feita pela prefeitura. [[Perguntas Frequentes](https://desenvolvedores.migrate.info/Geral/PerguntasFrequentes/), 22/09/2026]

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional).
- **Retenções federais:** segue a NT 007: PIS, COFINS e CSLL retidos somados em `ValCSLL`. Ver [Retenções federais](../04-base-conhecimento/artigos/retencoes-federais.md). [validado]
- Intermediário pode ser informado, mas a prefeitura não o valida. [portal antigo]
- Alíquota de ISS só é recebida para optante do Simples Nacional com ISS retido. [portal antigo]
