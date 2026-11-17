# Tomador estrangeiro e exportação no Padrão Nacional

> [!RESUMO]
> Informe o NIF do tomador estrangeiro em DocTomadorEstrangeiro ou o motivo em TomaExSemNIF; na exportação, preencha os campos de comércio exterior.

## Identificação do tomador

| Tag | Quando usar |
| :--- | :--- |
| `DocTomadorEstrangeiro` | Documento (NIF) do tomador, real ou fictício. |
| `TomaExSemNIF` | Tomador sem NIF. Valores abaixo. |

> [!ATENCAO]
> Sem `DocTomadorEstrangeiro`, a `TomaRazaoSocial` não é repassada à prefeitura.

Valores de `TomaExSemNIF`:

- `0`: não informado na nota de origem
- `1`: dispensado do NIF
- `2`: não exigência do NIF

## Exportação de serviços

O erro **E0330** ("É obrigatório prestar informações de comércio exterior para as situações de exportação de serviços") pede os campos abaixo, no grupo `Valores` do serviço.

| Tag | Significado | Valores |
| :--- | :--- | :--- |
| `mdPrestacao` | Modo de prestação | Ver códigos abaixo |
| `vincPrest` | Vínculo prestador × tomador | Ver códigos abaixo |
| `tpMoeda` | Moeda da transação | [Códigos do Banco Central](https://www.bcb.gov.br/estabilidadefinanceira/todasmoedas) (ex.: `220` = dólar) |
| `ValServEst` | Valor do serviço em moeda estrangeira | Valor acordado |
| `mecAFComexP` | Mecanismo de apoio ao comércio exterior do **prestador** | Ver códigos abaixo |
| `mecAFComexT` | Mecanismo de apoio ao comércio exterior do **tomador** | Ver códigos abaixo |
| `movTempBens` | Movimentação temporária de bens | Ver códigos abaixo |
| `mdic` | Compartilhar com o MDIC | `0` não enviar, `1` enviar |

> [!ATENCAO]
> Em `mdPrestacao`, **não use `0`**: gera o erro **E0333**.

> [!DETALHES] Códigos de mdPrestacao, vincPrest e movTempBens
>
> | Tag | Código | Significado |
> | :--- | :--- | :--- |
> | `mdPrestacao` | `1` | Transfronteiriço |
> | `mdPrestacao` | `2` | Consumo no Brasil |
> | `mdPrestacao` | `3` | Presença comercial no exterior |
> | `mdPrestacao` | `4` | Movimento temporário de pessoas físicas |
> | `vincPrest` | `0` | Sem vínculo |
> | `vincPrest` | `1` | Controlada |
> | `vincPrest` | `2` | Controladora |
> | `vincPrest` | `3` | Coligada |
> | `vincPrest` | `4` | Matriz |
> | `vincPrest` | `5` | Filial ou sucursal |
> | `vincPrest` | `6` | Outro vínculo |
> | `movTempBens` | `0` | Desconhecido |
> | `movTempBens` | `1` | Não |
> | `movTempBens` | `2` | Vinculada a declaração de importação |
> | `movTempBens` | `3` | Vinculada a declaração de exportação |

> [!DETALHES] Códigos de mecAFComexP e mecAFComexT
>
> | Código | Prestador (`mecAFComexP`) |
> | :--- | :--- |
> | `00` | Desconhecido |
> | `01` | Nenhum |
> | `02` | ACC |
> | `03` | ACE |
> | `04` | BNDES-Exim pós-embarque |
> | `05` | BNDES-Exim pré-embarque |
> | `06` | FGE |
> | `07` | PROEX equalização |
> | `08` | PROEX financiamento |
>
> No tomador (`mecAFComexT`): `00` desconhecido, `01` nenhum e de `02` a `26` (ex.: `24` royalties e assistência técnica, `26` ZPE). A lista completa está na fonte.

## Local da prestação

| Onde | Tag |
| :--- | :--- |
| No exterior | `PaiPreServico`, com a sigla do país (ex.: `US`) |
| No Brasil | `SerEndcMun`, com o código IBGE, no grupo `LocalPrestacao` |

## Exemplo de XML

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

## Outros provedores

Cada provedor municipal tem regras próprias para tomador estrangeiro (ex.: Betha, CITTA, Equiplano, RLZ, Barueri). Ver [observações por provedor](../../02-observacoes/observacoes-provedores.md).

Fonte: [Emissor Nacional: Tomador Estrangeiro](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Padroes/EmissorNacionalTomadorEstrangeiro/) (15/05/2026).
