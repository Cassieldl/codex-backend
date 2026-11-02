# Controle de numeração do RPS pelo InvoiCy (IDUnico)

> Fonte: [Controle de Numeração do RPS](https://desenvolvedores.migrate.info/Integracoes/SOAP/EmissaodeDocumentos/ControledeNumeracaodoRPS/) (13/02/2026).

## Resumo

Com a tag **`IDUnico`**, o **InvoiCy numera o RPS** no lugar do ERP. Funciona no Web Service (SOAP) e na API REST, para envio, consulta e cancelamento.

## Como funciona

- `IDUnico` é **opcional**. Se enviado, **substitui** o número e a série do documento.
- Exige uma **série cadastrada no InvoiCy** para o módulo NFS-e, com o **número do último RPS emitido**. O InvoiCy incrementa a partir dele.
- Sem `RPSSerie`, usa a **série padrão** cadastrada.
- Cada NFS-e tem um `IDUnico` **exclusivo** (até **200 caracteres**, qualquer valor que identifique o RPS).
- O `IDUnico` aparece na aba **Geral** dos detalhes do documento.

> Se o ERP enviar `IDUnico` **e também** número e série, o InvoiCy **não atualiza** o cadastro de séries e a numeração não é incrementada.

## Envio

```xml
<RPS>
  <RPSNumero/>
  <RPSSerie/>
  <RPSTipo>1</RPSTipo>
  <IDUnico>IDUNICO_5</IDUnico>
  ...
</RPS>
```

O retorno traz `DocNumero` (sequência do último RPS) e `DocSerie` (série cadastrada).

## Consulta

Basta o `IDUnico` usado na emissão:

```xml
<Consulta>
  <ModeloDocumento>NFSe</ModeloDocumento>
  <Versao>1.00</Versao>
  <tpAmb>2</tpAmb>
  <CnpjEmissor>99999999999999</CnpjEmissor>
  <IDUnico>IDUNICO_5</IDUnico>
</Consulta>
```

## Cancelamento

Além do `IDUnico`, o **`NFSeNumero` continua obrigatório**.

## Relação com a emissão judicial

Com numeração controlada pelo InvoiCy, a emissão por decisão judicial usa `IDUnico` + `nNFSeJudicial`. Ver [Emissão por decisão judicial](emissao-judicial.md).
