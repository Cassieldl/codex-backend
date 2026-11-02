# Consultas sobre o município: integrados, status e layout

> Fontes: [Consulta de Municípios Integrados](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Funcionalidades/ConsultadeMunicipiosIntegrados/) (16/02/2026), [Consulta de Status do Município](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Funcionalidades/ConsultadeStatusdoMunicipio/) (16/02/2026) e [Layout XML Recepção](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Funcionalidades/LayoutXMLRecepcao/) (02/12/2025).

## O município está integrado?

**Tela:** *Painel de Controle → NFS-e → Municípios Integrados*. Mostra padrão, forma de integração, tipo de operação e particularidades. Há também um [mapa dos municípios integrados](https://www.google.com/maps/d/u/0/viewer?mid=1gVaqU3tUa0Ll7ngjHD79cfKDCeA&ll=-14.199246408076363%2C-55.94937748457028&z=4).

**Webservice (SOAP):**

- WSDL homologação: <https://consultahomolog.invoicy.com.br/aconsultadocumentos.aspx?wsdl>
- WSDL produção: <https://consulta.invoicy.com.br/aconsultadocumentos.aspx?wsdl>
- Sempre obrigatórios: `ModeloDocumento`, `Versao` e `CnpjEmissor` (CNPJ de uma empresa **emissora de NFS-e ativa** no InvoiCy).

| Consulta | Campos | Uso |
| :--- | :--- | :--- |
| Por UF | `UF` | Lista os municípios integrados da UF. |
| Por código IBGE | `CodMunicipio` | Confirma se um município está integrado. Veja `possuiHomolog` para saber se tem homologação. |
| Por ambiente | `tpAmb` (0 todos · 1 produção · 2 homologação) | Municípios por ambiente. |
| Por tipo de ambiente | `tpAmb` + `codmunicipio` | Se não houver o ambiente: "O município informado não possui ambiente de homologação!" |
| Por data de inicialização | `DataInicializacao` (sozinho) | Municípios integrados a partir da data (inv0305/inv0306). |

A tag `importaNotasRecebidas` indica o que o município exige para consultar notas recebidas (IM no município, cadastro de prestadores, etc.).

Exemplos de XML: [repositório de integração](https://github.com/migrate-company/integracao-InvoiCy-BR/tree/main/Consulta%20Munic%C3%ADpios%20Integrados%20NFSe).

## A prefeitura está fora do ar?

A **Consulta de Status do Município** (mesmo WSDL acima) informa se o serviço está **online ou offline**, os tempos **mínimo, máximo e médio** de resposta e a data do **último documento processado com sucesso**. Retorno de sucesso: código **100**.

Os tempos só aparecem depois de ao menos uma consulta ou tentativa de emissão naquele município.

Exemplo: [Consulta Status Comunicacao Municipio - Envio.xml](https://github.com/migrate-company/integracao-InvoiCy-BR/blob/main/Consulta%20status%20comunica%C3%A7%C3%A3o%20munic%C3%ADpios/Exemplos%20XML/Consulta%20Status%20Comunicacao%20Municipio%20-%20Envio.xml).

## Qual tag da prefeitura corresponde a qual campo InvoiCy?

*Painel de Controle → NFS-e → Layout XML Recepção*. Selecione o município e veja, para cada tag: nome na prefeitura, tipo, grupo, nome no InvoiCy, ocorrência, descrição e se é interno ou externo. Há filtros por sequencial, tag da prefeitura, grupo e tag InvoiCy.

É a mesma informação do nosso [layout De/Para](../../01-layouts/layouts-provedores.md), mas sempre **atualizada pelo sistema**, e é o caminho indicado na mensagem **inv0328**.
