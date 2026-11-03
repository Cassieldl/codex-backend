# Erro 173: Chave de comunicação inválida

> [!RESUMO]
> O InvoiCy não reconheceu a chave de comunicação (`EmpCK`): o hash MD5 enviado não bate com o esperado.
> A requisição é recusada antes de chegar à prefeitura, então não é erro da prefeitura nem do documento fiscal.
> Para achar a causa, compare o seu hash com a **EmpCK Esperada** em *Ferramentas de Integração*.

É um dos erros mais comuns ao **começar a integrar** com o InvoiCy via Web Service (SOAP).

| Contexto | Mensagem |
| :--- | :--- |
| Emissão, consulta e eventos | **173: Chave de comunicação inválida** |
| Cadastro de empresa | **173: Chave de comunicação ou chave de parceiro inválida** |

## Como a autenticação funciona

Toda chamada SOAP leva, no cabeçalho:

| Parâmetro | O que é |
| :--- | :--- |
| `EmpPK` | **Chave de parceiro**, enviada pela Migrate ao parceiro por e-mail. É a mesma em homologação e produção. |
| `EmpCK` | **Chave de comunicação**: hash **MD5** de `chave de acesso + XML`. Muda a cada requisição. |
| `EmpCO` | Identificador de PDV (quando usado). |
| `Documento` | O XML da requisição, convertido para texto. |

O `EmpCK` é calculado assim:

```
EmpCK = MD5( chave_de_acesso + "<Envio>...</Envio>" )
```

- **Chave de acesso:** o InvoiCy gera no cadastro da empresa. Fica em *Painel de Controle → grupo Empresa → Dados da Empresa*.
- **XML:** todo o conteúdo do grupo `<Envio>` (ou da raiz da requisição), **antes** de ser convertido para texto dentro de `<inv:Documento>`.
- Com **mais de um documento** na requisição, o hash usa **só o primeiro**.
- Resultado: 32 caracteres hexadecimais, em **minúsculas**.

Exemplo de entrada: `9099628c5a2e63d56a6c058c34e2b46f<Envio><ModeloDocumento>NFSe</ModeloDocumento>...</Envio>`

> [!IMPORTANTE]
> A chave de acesso **é diferente em homologação e em produção**. Ao ir para produção, troque a chave de acesso e o endereço do webservice. A chave de parceiro continua a mesma.

## Causas mais comuns

| Causa | Como corrigir |
| :--- | :--- |
| **Chave de acesso errada** (de outra empresa) | Confira em *Dados da Empresa* da empresa emissora. |
| **Ambiente trocado**: chave de homologação usada em produção, ou o contrário | Cada ambiente tem sua chave de acesso. Ao ir para produção, troque a chave de acesso (a de parceiro continua a mesma) e o endereço do webservice. |
| **XML alterado depois de gerar o hash** (formatação, indentação, troca de encoding) | Gere o MD5 sobre o XML **exatamente** como será enviado e não mexa nele depois. |
| **Acentos ou caracteres especiais** no XML | Remova acentos e caracteres especiais do conteúdo. |
| **Dupla conversão de caracteres** (`<` virando `&lt;` antes do hash) | Calcule o hash sobre o XML original; a conversão para texto é só para colocar dentro de `<inv:Documento>`. |
| **Espaços e quebras de linha** | Use o XML **linearizado** (sem quebras e sem espaços entre tags). |
| **Função MD5 com defeito** (comum em Delphi) ou encoding diferente de UTF-8 | Gere o hash com UTF-8 e compare com uma ferramenta confiável. |
| **Chave de parceiro inválida** (no cadastro de empresa) | Confira a `EmpPK` recebida da Migrate. |

> [!ATENCAO]
> Calcule o hash sobre o XML final, linearizado e sem acentos. Qualquer mudança no XML depois do cálculo, até um espaço ou uma quebra de linha, gera o erro 173.

## Como diagnosticar: compare com o hash esperado

O InvoiCy calcula o `EmpCK` que ele espera. Use essa ferramenta para comparar:

> [!PASSOS]
> 1. Acesse *Painel de Controle → grupo Empresa → **Ferramentas de Integração***.
>
>    ![Painel de Controle com o ícone Ferramentas de Integração destacado no grupo Empresa](../../assets/imagens/erro-173-chave-de-comunicacao/menu-ferramentas-de-integracao.png "Menu Ferramentas de Integração")
>
> 2. Confira se a **empresa selecionada** é a emissora. A tela mostra a chave de acesso e a chave de parceiro.
> 3. Cole em **Conteúdo do XML** o XML **linearizado** que o seu sistema enviou e clique em **Gerar EmpCK**.
>
>    ![Tela Ferramentas de Integração com o campo Conteúdo do XML, o botão Gerar EmpCK e o campo EmpCK Esperada, com as chaves borradas](../../assets/imagens/erro-173-chave-de-comunicacao/gerar-empck-esperada.png "Gerar EmpCK esperada")
>
> 4. Compare o valor de **EmpCK Esperada** com o hash gerado pelo seu sistema:
>    - **Iguais:** o hash está certo; confira se a requisição usa o mesmo XML e o ambiente correto.
>    - **Diferentes:** o problema está no XML (caracteres especiais, espaços, quebras de linha) ou na função MD5.

> [!DICA]
> Para conferir fora do InvoiCy, calcule o MD5 localmente, na sua máquina. Nunca cole a chave de acesso em site de terceiros.

## Exemplos de geração do MD5

> [!DETALHES] Código de exemplo em C# e Java
> **C#**
>
> ```csharp
> public string Md5(string entrada) // entrada = chaveAcesso + "<Envio>…</Envio>"
> {
>     using var md5 = System.Security.Cryptography.MD5.Create();
>     byte[] dados = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(entrada));
>     var construtor = new System.Text.StringBuilder();
>     foreach (byte b in dados) construtor.Append(b.ToString("x2"));
>     return construtor.ToString();
> }
> ```
>
> **Java**
>
> ```java
> public String md5(String entrada) throws Exception {
>     MessageDigest algoritmo = MessageDigest.getInstance("MD5");
>     byte[] digest = algoritmo.digest(entrada.getBytes(StandardCharsets.UTF_8));
>     String hash = new BigInteger(1, digest).toString(16);
>     while (hash.length() < 32) hash = "0" + hash; // mantém os zeros à esquerda
>     return hash;
> }
> ```

> [!ATENCAO]
> Em Java, sem completar com zeros à esquerda, hashes que começam com `0` saem com menos de 32 caracteres e o InvoiCy devolve 173.

## Erros relacionados

| Código | Mensagem | O que fazer |
| :--- | :--- | :--- |
| 174 | Documento e ambiente não identificados | Confira o `ModeloDocumento` e o `tpAmb` do XML. |
| 176 | Tipo documento não identificado | Confira o `ModeloDocumento` (ex.: `NFSE`). |
| 179 | Consumo indevido do Web Service: requisições iguais repetidas são bloqueadas por um período | Não reenvie a mesma requisição em loop; aguarde o desbloqueio. |
| 606 / 607 | Empresa não habilitada / não licenciada para o módulo e ambiente | Confira o cadastro e o licenciamento da empresa (*Módulos Contratados*). |

Ver também: [Mensagens do InvoiCy](../../03-rejeicoes/invoicy-inv03xx.md).

## Fontes

- [Perguntas Frequentes](https://desenvolvedores.migrate.info/Geral/PerguntasFrequentes/) (22/09/2026)
- [Código hash MD5](https://desenvolvedores.migrate.info/Integracoes/SOAP/WebService/CodigoHashMD5/) (28/01/2026)
- [Consumo do Web Service](https://desenvolvedores.migrate.info/Integracoes/SOAP/WebService/ConsumodoWebService/) (16/10/2026)
- [Alterações Necessárias para a Entrada em Produção](https://desenvolvedores.migrate.info/Sistema/Ambientes/AlteracoesNecessariasparaaEntradaemProducao/) (16/02/2026)
- [Códigos de Retorno](https://desenvolvedores.migrate.info/Integracoes/CodigosdeRetorno/)
