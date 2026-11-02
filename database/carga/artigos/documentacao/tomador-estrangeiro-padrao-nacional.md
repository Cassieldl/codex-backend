# Tomador estrangeiro e exportação no Padrão Nacional

> Fonte: [Emissor Nacional: Tomador Estrangeiro](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/EmissorNacionalTomadorEstrangeiro/) (15/05/2026).

## Identificação do tomador

| Tag | Uso |
| :--- | :--- |
| `DocTomadorEstrangeiro` | Documento (NIF) do tomador; pode ser real ou fictício. **Sem ela, `TomaRazaoSocial` não é repassada à prefeitura.** |
| `TomaExSemNIF` | Quando o tomador não tem NIF: `0` não informado na nota de origem · `1` dispensado do NIF · `2` não exigência do NIF. |

## Exportação de serviços

Rejeição **E0330** - "É obrigatório prestar informações de comércio exterior para as situações de exportação de serviços": preencha os campos abaixo (grupo `Valores` do serviço).

| Tag | Significado | Valores |
| :--- | :--- | :--- |
| `mdPrestacao` | Modo de prestação | `1` transfronteiriço · `2` consumo no Brasil · `3` presença comercial no exterior · `4` movimento temporário de pessoas físicas. **Não use `0`** (rejeição **E0333**). |
| `vincPrest` | Vínculo prestador × tomador | `0` sem vínculo · `1` controlada · `2` controladora · `3` coligada · `4` matriz · `5` filial ou sucursal · `6` outro vínculo |
| `tpMoeda` | Moeda da transação | [Códigos do Banco Central](https://www.bcb.gov.br/estabilidadefinanceira/todasmoedas) (ex.: `220` = dólar) |
| `ValServEst` | Valor do serviço em moeda estrangeira | Valor acordado |
| `mecAFComexP` | Mecanismo de apoio ao comércio exterior do **prestador** | `00` desconhecido · `01` nenhum · `02` ACC · `03` ACE · `04` BNDES-Exim pós-embarque · `05` BNDES-Exim pré-embarque · `06` FGE · `07` PROEX equalização · `08` PROEX financiamento |
| `mecAFComexT` | Mecanismo de apoio ao comércio exterior do **tomador** | `00` desconhecido · `01` nenhum · `02`–`26` (ex.: `24` royalties e assistência técnica · `26` ZPE); lista completa na fonte |
| `movTempBens` | Movimentação temporária de bens | `0` desconhecido · `1` não · `2` vinculada a declaração de importação · `3` vinculada a declaração de exportação |
| `mdic` | Compartilhar com o MDIC | `0` não enviar · `1` enviar |

## Local da prestação

- **No exterior:** informe `PaiPreServico` (sigla do país, ex.: `US`).
- **No Brasil:** informe `SerEndcMun` com o código IBGE, no grupo `LocalPrestacao`.

## Exemplo resumido

```xml
<Servico>
  <codTributNacional>010601</codTributNacional>
  <CodigoNBS>115011000</CodigoNBS>
  <PaiPreServico>US</PaiPreServico>
  <Valores>
    <ValServicos>1</ValServicos>
    <mdPrestacao>4</mdPrestacao>
    <vincPrest>0</vincPrest>
    <tpMoeda>220</tpMoeda>
    <ValServEst>2000</ValServEst>
    <mecAFComexP>01</mecAFComexP>
    <mecAFComexT>01</mecAFComexT>
    <movTempBens>1</movTempBens>
    <mdic>0</mdic>
  </Valores>
</Servico>
<Tomador>
  <TomaRazaoSocial>Razão Social</TomaRazaoSocial>
  <TomaUF>EX</TomaUF>
  <TomaPais>MX</TomaPais>
  <DocTomadorEstrangeiro>001</DocTomadorEstrangeiro> <!-- ou TomaExSemNIF quando não houver NIF -->
</Tomador>
```

O exemplo completo está na fonte.

## Em outros padrões

Cada padrão municipal tem regras próprias para tomador estrangeiro (ex.: Betha, CITTA, Equiplano, RLZ, Barueri). Ver [observações por provedor](../../02-observacoes/observacoes-provedores.md).
