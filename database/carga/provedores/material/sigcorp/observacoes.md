## Sigcorp

- **Layout:** [Sigcorp](../01-layouts/layouts-provedores.md#sigcorp)
- **Fontes:** [obs]; [portal antigo - Sigcorp ABRASF](https://desenvolvedores-antigo.migrate.info/2019/04/09/padrao-sigcorp-abrasf)

### Autenticação e acesso

- Login e senha do prestador via **HTTP Basic** (`Authorization: Basic <base64(IM:senha)>`); **não exige certificado**.
  - Usuário: **Inscrição Municipal**.
  - Senha: senha do prestador no município.
- Toda chamada exige também `X-NFSe-Prestador-CNPJ: <CNPJ, só números>`, igual ao prestador da DPS (ou ao emitente da nota consultada); divergência rejeita o acesso.
- O prestador deve solicitar à prefeitura **autorização para emitir via webservice**. [portal antigo]
- No InvoiCy, preencha **Usuário de autenticação** (Inscrição Municipal) e **Senha de autenticação** (senha do portal) em *Painel de controle → NFS-e → Configurações para emissão de NFS-e*. [obs]

### Processamento

| Status | Significado | Dados adicionais |
| :--- | :--- | :--- |
| `em_processamento` | Recebida, em validação ou aguardando o ADN | - |
| `rejeitado` | Rejeitada nas validações do webservice | Lista de motivos |
| `recusada` | Recusada pelo Ambiente Nacional (ADN) | Motivo do ADN |
| `erro_interno` | Falha técnica | Mensagem genérica; o provedor é notificado |
| `aprovado` | Emitida | XML, chave de acesso e link de impressão |

### Regras de preenchimento

- **Natureza da operação (leiaute nacional):** usa a [tabela do Padrão Nacional](../04-base-conhecimento/artigos/natureza-da-operacao.md#padrão-nacional-e-provedores-de-leiaute-nacional). Na versão ABRASF vale a tabela própria.
- **Não aceita quebra de linha.**
- Versão ABRASF: sem tomador do exterior e sem intermediário. Tomador não identificado → CPF **77777777777** e razão social **"NÃO INFORMADO"**. [portal antigo]
- **E405 – Método em utilização** costuma esconder dado inválido no XML (texto em campo numérico, código inválido, valor acima do limite). Revise tag a tag. [portal antigo]
- Validações próprias além do padrão nacional. Uma nota pode ser rejeitada por:
  - Prestador sem autorização para o código de serviço informado.
  - Tomador/intermediário PJ do município sem cadastro na prefeitura (regularização presencial).
  - `nNFSe` já usado.
  - Isenção/imunidade sem o benefício no cadastro.
  - `pAliq` ausente ou fora da faixa, quando obrigatória.
  - Retenção de ISSQN sem tomador/intermediário identificado.
  - CNPJ do tomador igual ao do prestador, ou raiz do intermediário igual à do prestador.
  - E-mail inválido.
  - Endereço nacional/exterior incompatível com o tipo de identificação.

### Reforma Tributária (IBS/CBS)

- Rejeita:
  - `IBSCBS` de optante do Simples.
  - `cNBS` ausente quando há `IBSCBS`.
  - Grupo imóvel incompatível com o serviço.
  - Classificação tributária (`cClassTrib`, NBS, `cIndOp`, código nacional) fora da tabela municipal.

  Natureza da operação (versão ABRASF):

  ![Natureza da operação Sigcorp ABRASF](../assets/imagens/sigcorp/natureza-da-operacao-sigcorp-abrasf.jpg)
