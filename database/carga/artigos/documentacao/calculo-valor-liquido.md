# Cálculo do valor líquido da nota (vLiq)

## Fórmula

```
vLiq = vServ − vDescCond − vDescIncond − valores retidos
```

## Campos usados

| Parcela | Tag | Caminho no XML da NFS-e |
| :--- | :--- | :--- |
| Valor do serviço | `vServ` | `NFSe/infNFSe/DPS/infDPS/valores/vServPrest/vServ` |
| Desconto condicionado | `vDescCond` | `NFSe/infNFSe/DPS/infDPS/valores/vDescCondIncond/vDescCond` |
| Desconto incondicionado | `vDescIncond` | `NFSe/infNFSe/DPS/infDPS/valores/vDescCondIncond/vDescIncond` |

**Valores retidos** = soma de:

| Retenção | Tag | Caminho no XML da NFS-e |
| :--- | :--- | :--- |
| Contribuição previdenciária | `vRetCP` | `NFSe/infNFSe/DPS/infDPS/valores/trib/tribFed/vRetCP` |
| IRRF | `vRetIRRF` | `NFSe/infNFSe/DPS/infDPS/valores/trib/tribFed/vRetIRRF` |
| Contribuições sociais (CSLL + PIS + COFINS retidos) | `vRetCSLL` | `NFSe/infNFSe/DPS/infDPS/valores/trib/tribFed/vRetCSLL` |
| ISSQN | `vISSQN` | `NFSe/infNFSe/valores/vISSQN` - **somar só quando o ISS for retido** |

> Desde a NT 007, PIS e COFINS retidos entram somados em `vRetCSLL`. Ver [NT 007](nt007-retencoes-federais.md).

Fonte: observações do padrão SMARAPDISIL (leiaute nacional).
