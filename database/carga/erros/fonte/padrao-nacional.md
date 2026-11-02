# Rejeições: Padrão Nacional (Emissor Nacional / ADN)

> Fontes: [FAQ - Padrão Nacional](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/FAQ/) (31/03/2026), [Padrão Emissor Nacional](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/EmissorNacional/) (18/03/2026), [Padrão COPLAN](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/COPLAN/) (14/05/2026) e [Tomador Estrangeiro](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/EmissorNacionalTomadorEstrangeiro/) (15/05/2026).

Valem para o **Emissor Nacional** e para padrões que seguem as regras dele (Coplan, versões "Modelo Nacional" de vários provedores). As mensagens chegam como estão abaixo ou dentro de "Mensagem retornada pela prefeitura" ([inv0328](invoicy-inv03xx.md)).

Observações do padrão: [Emissor Nacional](../02-observacoes/observacoes-provedores.md#emissor-nacional).

## Configuração e cadastro

| Mensagem | Causa | Como resolver |
| :--- | :--- | :--- |
| O estabelecimento informado deverá emitir suas NFS-e por meio do emissor nacional da NFS-e | O município migrou para o Padrão Nacional. | No InvoiCy: *Painel de controle → NFS-e → Configurações para emissão → Versão do Município = 0.20 – NFS-e Nacional*. Depois reenvie as notas rejeitadas. Ver [Migrar para o Padrão Nacional](../04-base-conhecimento/artigos/migracao-padrao-nacional.md). |
| O município emissor informado na DPS deve estar parametrizado para utilizar os emissores públicos nacionais | Nem todo município usa a versão 0.20; alguns mantiveram sistema próprio. | Antes de mudar para 0.20, confira no site da prefeitura o comunicado sobre o sistema emissor vigente. |
| A assinatura deve ser feita com o certificado digital do emitente da DPS | O certificado não é do CNPJ emissor nem da mesma raiz (matriz). | Cadastre no InvoiCy o certificado do emitente ou da matriz (mesma raiz de CNPJ). Passo a passo: <https://desenvolvedores.migrate.info/Sistema/PaineldeControle/Certificados/> |
| E0037 - Município emissor inexistente no cadastro de convênio | Município sem Produção Restrita habilitada. | Ver [e-l.md](e-l.md) e [E0037](../04-base-conhecimento/artigos/e0037-producao-restrita.md). |

## Identificação da DPS

| Mensagem | Causa | Como resolver |
| :--- | :--- | :--- |
| Identificador mal formado. O ID da DPS deve conter 42 números. | Série com letras. | Use **série numérica** (1, 2, 3). |
| Erro de esquema. The Id attribute is invalid - The value DPS…S000… is invalid according to its datatype TSIdDPS | Série com letras (o "S" no meio do ID). | Use **série numérica**. |
| E1235 - Falha no esquema XML | XML fora do XSD. | Ver [E1235](../04-base-conhecimento/artigos/e1235-falha-esquema-xml.md). |

## Serviço e tributação

| Mensagem | Causa | Como resolver |
| :--- | :--- | :--- |
| Falha Schema Xml: element cServ has invalid child element xDescServ… expected: cTribNac | Falta o código de tributação nacional. | Preencha `codTributNacional`. O emissor confere o código liberado no Emissor Nacional ou com a contabilidade. |
| O código de tributação nacional informado não existe ou não está administrado pelo município de incidência do ISSQN… | Código de tributação não liberado para o CNPJ/município. | Revise `TributMunicipio`: teste `000`, `001`, `002` ou não envie. Se nenhum servir, o CNPJ já tem código vinculado: simule uma emissão no Portal Nacional (sem concluir) para ver os códigos aceitos. |
| O código de tributação municipal informado não existe ou não está administrado pelo município… | Mesmo caso. | Mesma solução acima. |
| Erro não catalogado | Geralmente `TributMunicipio` ou `cIndOp`. | Revise `TributMunicipio` como acima; `cIndOp` precisa estar correlacionado ao item da lista e ao NBS ([tabela oficial](https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/rtc/anexovii-indop_ibscbs_v1-00-00.xlsx/view)). |
| Falha Schema Xml: cIndOp element is invalid - The value 0 is invalid (TSRTCCodIndOp) | `cIndOp` com valor inválido. | Preencha `cIndOp` conforme a [tabela de indOp](https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/rtc/anexovii-indop_ibscbs_v1-00-00.xlsx/view). |
| Erro de esquema: cLocPrestacao element is invalid - The value 0 is invalid (TSCodMunIBGE) | Município do local de prestação inválido (é preenchimento, não configuração). | Informe em `SerEndcMun` (grupo `LocalPrestacao`) o **código IBGE** da cidade onde o serviço é prestado. |
| Erro de esquema: xInfComp element is invalid (TSDescInfCompl) | Quebra de linha no texto. | Remova quebras de linha da **Discriminação** e da **Descrição do serviço**. |
| O tipo de imunidade é obrigatório e deve ser informado somente quando a tributação do ISSQN for igual a 2 – Imunidade | `natOp = 4` sem `tpImunidade`. | Com `natOp = 4`, envie `tpImunidade = 2`. Se a empresa não é imune, revise a `natOp`. |

Print de referência (exemplo; cada emissor tem liberações próprias):

![Campos de tributação no portal ADN](../assets/imagens/emissor-nacional/campos-de-tributacao-no-portal-adn.png)

## Simples Nacional e totais de tributos

| Mensagem | Causa | Como resolver |
| :--- | :--- | :--- |
| É obrigatório o preenchimento do campo de regime de apuração dos tributos do SN para o optante do Simples Nacional ME/EPP | Falta `regApTribSN`. | Preencha `regApTribSN`: `1` federais e municipal pelo SN · `2` federais pelo SN e ISSQN por fora · `3` federais e municipal por fora do SN. |
| Informe o Regime de Apuração Tributária pelo Simples Nacional | Mesmo caso. | Mesma solução acima. |
| Para ME/EPP somente um dos 3 poderá ser informado: grupo vTotTrib ou pTotTrib ou pTotTribSN | Regime incorreto para ME/EPP. | Confirme se é ME/EPP ([consulta Simples Nacional](https://www8.receita.fazenda.gov.br/simplesnacional/aplicacoes.aspx?id=21)). Se for: `optSN = 1` e `RegEspTrib = 6`. O InvoiCy só trata esses campos com o regime correto. |
| The element trib has incomplete content. Expected: totTrib | Não optante sem os totais aproximados. | Com `optSN = 2`, preencha `ValRetFederais`, `ValRetEstaduais` e `ValRetMunicipais` e **não** envie `ValAproxTribAliqSN`. |
| The element trib has invalid child element totTrib | Mesmo tema de `totTrib`. | Mesma verificação de ME/EPP acima. |

## Tomador estrangeiro e exportação

| Código / mensagem | Causa | Como resolver |
| :--- | :--- | :--- |
| E0330 - É obrigatório prestar informações de comércio exterior para as situações de exportação de serviços | Faltam campos de comércio exterior. | Preencha `mdPrestacao`, `vincPrest`, `tpMoeda`, `ValServEst`, `mecAFComexP`, `mecAFComexT`, `movTempBens` e `mdic`. |
| E0333 | `mdPrestacao = 0`. | Use apenas `1` a `4`. |

Detalhes e exemplo de XML: [Tomador estrangeiro no Padrão Nacional](../04-base-conhecimento/artigos/tomador-estrangeiro-padrao-nacional.md).

## Retenções (NT 007)

- PIS, COFINS e CSLL retidos vão somados em `ValCSLL` (`vRetCSLL`). `ValPIS` e `ValCOFINS` são só débito próprio. Ver [NT 007](../04-base-conhecimento/artigos/nt007-retencoes-federais.md).
