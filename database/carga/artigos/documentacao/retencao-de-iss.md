# Retenção de ISS

> Fonte: [Retenção de ISS](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/RetencaodeISS/) (13/02/2026).

## O que é

Na retenção, o **tomador** recolhe o ISS no lugar do prestador. O tomador vira **substituto tributário** e o prestador, **substituído**. O prestador continua contribuinte: o imposto já está no preço; só a obrigação de recolher passa ao tomador.

## Base legal

- **LC 116/2003:** a regra geral é o ISS devido no local do **estabelecimento do prestador**. O art. 3º lista serviços devidos no **local da prestação** ou do tomador, o que fundamenta a retenção.
- **Art. 6º da LC 116/2003:** o município pode atribuir a responsabilidade a terceiros por lei local.
- **LC 157/2016:** alíquota mínima de **2%**. Se o município conceder menos, o ISS passa a ser devido no local do tomador ou intermediário, e a retenção se torna obrigatória.
- **LC 175/2020:** alterou regras de competência do art. 3º.

## Reforma Tributária

As regras continuam válidas na transição (EC 132/2023). O ISS será gradualmente substituído pelo **IBS** até **2033**, quando entram a tributação no destino e o *split payment*.

## No InvoiCy

| Tag | Uso |
| :--- | :--- |
| `ISSRetido` | Indica se há retenção. |
| `ValISSRetido` | Valor do ISS retido. |
| `ItemValAliqISSRetido` | Alíquota do ISS retido no item. |
| `RespRetencao` / `ItemRespRetencao` | Responsável pela retenção (obrigatório em alguns padrões, como IPM). |

## Particularidades por padrão

- Vários padrões **não diferenciam** ISS de ISS retido; a prefeitura decide pelo código de tributação (Elotech, RLZ, IPM, Equiplano, Governo Digital, DSFNET).
- **Betha:** leiaute nacional; o ISS retido vai em `tpRetISSQN`. A opção **3 – Substituição Tributária** era do Betha 1.0 (legado).
- **Dueto:** responsável pela retenção só quando houver retenção.
- **ISSNET:** não retenha de tomador com IM baixada (L042).
- **Sigcorp:** retenção exige tomador ou intermediário identificado por CPF/CNPJ.
- **WebISS:** com ISS retido, o valor do ISS enviado é ignorado.
