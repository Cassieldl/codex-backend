# Rejeições: GESPAM (ISS Online)

> Fonte: `_origem/Padrões/GESPAM/OBSERVAÇÕES.txt`. Observações do padrão: [GESPAM](../02-observacoes/observacoes-provedores.md#gespam).

Códigos iniciados por **E** (e **RNG...**) são das regras de negócio nacionais da NFS-e, documentadas no anexo de regras do leiaute nacional: <https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/documentacao-atual>. Os códigos abaixo são específicos do ISS Online.

## Sistema e requisição (S)

| Código | Descrição |
| :--- | :--- |
| S0001 | O lote enviado contém DPS já processada (traz o protocolo do envio que teve sucesso). |
| S0002 | DPS em duplicidade no arquivo enviado. |
| S0003 | A quantidade de DPS informada no lote difere da quantidade identificada no arquivo. |
| S0006 | Corpo da requisição inválido. |
| S0010 | CPF/CNPJ do emitente (ou do autor do evento) ausente. |
| S0011 | DPS/NFS-e com o identificador informado não encontrada. |
| S0013 | Erro interno - contate o suporte. |
| S0014 | Identificador da DPS ausente. |
| S0015 | Protocolo ausente. |
| S0016 | Chave de acesso ausente ou NFS-e não encontrada. |
| S0017 | Versão do leiaute XML ausente. |
| S0018 | Data de emissão (`dhEmi`) no futuro em relação ao horário de Brasília. |

## Contribuinte (C)

| Código | Descrição |
| :--- | :--- |
| C0001 | CNPJ do prestador não autorizado a emitir NFS-e. |
| C0002 | Emitente sem autorização para usar o serviço de processamento de DPS. |
| C0004 | Contribuinte emitente (ou ERP) não encontrado. |
| C0005 | Cadastro do contribuinte inativo. |
| C0007 | Emitente não consta como prestador de serviço. |
| C0008 | Emitente não vinculado à prefeitura do município emissor (`cLocEmi`). |
| C0010 | Protocolo não encontrado. |
| C0011 | Emitente da DPS difere do contribuinte dono da ApiKey. |
| C0012 | Contribuinte não autorizado a cancelar NFS-e. |
| C0013 | Contribuinte difere do emitente da NFS-e. |
| C0014 | `cClassTrib` do IBS/CBS não encontrado. |
| C0015 | Regime tributário da DPS difere do cadastro do contribuinte na prefeitura. |
| C0016 | Competência (`dCompet`) em período de apuração do ISSQN já encerrado. |
| C0017 | Competência anterior ao limite de emissão retroativa autorizado. |

## Prefeitura (P)

| Código | Descrição |
| :--- | :--- |
| P0001 | Código IBGE do município emissor inválido. |
| P0002 | Município emissor não atendido pelo ISS Online. |
| P0003 | Erro no cadastro do município - procure a prefeitura. |
| P0004 | Série inexistente ou inválida (no padrão nacional, só números). |
| P0005 | Parâmetros do benefício municipal não encontrados; no cancelamento, evento anterior impede a operação. |
| P0006 | Município não autorizado a usar este emissor; no cancelamento, guia de pagamento já gerada pelo tomador. |
| P0007 | Nota prestada à prefeitura: cancelamento só por solicitação de análise fiscal. |
| P0008 | Prazo de cancelamento expirado. |
| P0009 | Guia de pagamento da competência já gerada. |
| P0010 / P0011 | Inconsistência na Declaração Mensal de Serviços da nota - contate o suporte. |

## Lote (L)

| Código | Descrição |
| :--- | :--- |
| L0001 | Lote sem DPS. |
| L0002 | Lote com mais de 50 DPS. |

## Problemas comuns

| Sintoma | Causa e solução |
| :--- | :--- |
| HTTP 401 em toda requisição | A ApiKey está errada, foi gerada mas não confirmada, foi substituída por uma nova ou é de outro ambiente (homologação × produção). Confira o header `ApiKey` e o ambiente. |
| HTTP 415 | Falta `Content-Type: application/xml` (ou `application/json` no `/RecepcionarDpsJson`). |
| HTTP 400 com resposta JSON (`application/problem+json`) num endpoint XML | O corpo não é um XML bem formado ou o elemento raiz está errado (ex.: `<LoteDps>` enviado para `/RecepcionarDps`). A mensagem em `errors` indica linha e posição. |
| HTTP 429 | Mais de 100 requisições por minuto por IP. Aguarde e tente de novo. |
| E1235 - Falha no esquema XML | A DPS não segue o XSD do leiaute 1.01. Leia `InformacoesComplementares`: indica posição e campo. Ver [E1235](../04-base-conhecimento/artigos/e1235-falha-esquema-xml.md). |
| C0011 - emitente difere do contribuinte da ApiKey | A ApiKey é de contribuinte e a DPS é de outro CNPJ. Use a chave do próprio emitente ou peça uma ApiKey de ERP. |
| P0004 - série inválida | No padrão nacional a série é só numérica. Confirme com a prefeitura as séries liberadas para o contribuinte. |
| Consulta parada em `AGUARDANDO_PROCESSAMENTO` | Normalmente leva segundos. Acima de 30 minutos, contate o suporte informando o protocolo. |
| Nota emitida, mas o ERP não recebeu resposta | Consulte antes de reenviar. **[pendente] Pendente:** a origem remete à seção "Reenvio e duplicidade" do manual, que não estava na documentação. |
