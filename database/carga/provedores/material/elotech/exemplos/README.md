# Exemplo da Elotech 2.03

Fonte dos exemplos de campo (`exemplo`) da Elotech 2.03. A carga lê este arquivo por
`database/carga/exemplos-xml.mjs` (fonte `elotech`, raiz `<EnviarLoteRpsEnvio>`).

## Origem

`documentacao/Provedores/Elotech/EnviarLoteRpsEnvio.xml`, copiado como `EnviarLoteRpsEnvio.xml`.

## O que foi removido

Cópia saneada na origem, para o repositório não carregar dado que o portal omite:

- A linha `<Senha>` de `IdentificacaoRequerente` (credencial).
- O **texto** das folhas de identificação, que ficam como tag vazia (`<Cnpj></Cnpj>`): documento e
  inscrição (`Cnpj`, `InscricaoMunicipal`, `inscImobFisc`, `cCIB`), nome (`RazaoSocial`, `xNome`),
  contato (`Telefone`, `Email`, `fone`, `email`) e endereço (`Endereco`, `Numero`, `Complemento`,
  `Bairro`, `Cep`, `cEndPost`, `xCidade`, `xEstProvReg`) e chave de acesso (`refNFSe`, que embute o
  CNPJ do emitente). `Endereco`, `Numero`, `Complemento` e
  `Bairro` só contam como identificação sob `Endereco` ou `end`: `IdentificacaoRps/Numero` (`579`) é
  o número do RPS e permanece. A lista exata é `ehFolhaDeIdentificacao` em `exemplos-xml.mjs`.

Todo o resto é cópia fiel do original, inclusive indentação (espaços) e quebras de linha.
Os valores de teste (`Teste de emissao`, `teste item 1`, `teste de envio de tag`) permanecem: não
são dado pessoal, e a carga os descarta por valor de teste.

## Envio assíncrono na fonte, síncrono no catálogo

O arquivo é o envelope **assíncrono** (`EnviarLoteRpsEnvio`), e o mapeamento do catálogo descreve o
**síncrono** (`EnviarLoteRpsSincronoEnvio`). Só o trecho `LoteRps/ListaRps/Rps/` é aproveitado, e as
raízes nunca casam entre si: o que está fora desse trecho (`IdentificacaoRequerente`,
`NumeroLote`, `QuantidadeRps` e o `CpfCnpj` do lote) não recebe exemplo.

A premissa é a estrutura idêntica do `Rps` nos dois envios. Conferida em `../nfse_v2_03.xsd`:

- `EnviarLoteRpsEnvio` e `EnviarLoteRpsSincronoEnvio` têm a mesma sequência: `IdentificacaoRequerente`
  (`tcIdentificacaoRequerente`) e `LoteRps` (`tcLoteRps`).
- `tcLoteRps` é um só tipo para os dois, e `ListaRps/Rps` é `tcDeclaracaoPrestacaoServico`, que
  envolve `InfDeclaracaoPrestacaoServico`.

Logo `LoteRps/ListaRps/Rps/...` tem exatamente a mesma estrutura nos dois envios.
