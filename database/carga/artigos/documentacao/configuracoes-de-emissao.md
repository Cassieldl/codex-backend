# Configurações para emissão de NFS-e e particularidades

> [!RESUMO]
> As configurações de NFS-e ficam em *Painel de Controle → grupo NFS-e → Configurações para Emissão*, por inscrição municipal.
> A tela tem cinco blocos: Gerais, Espelho, Ambientes, Avançado e Particularidades.
> Para corrigir diferenças de um município sem mexer no ERP, use as **particularidades condicionadas**.

## Onde fica

*Painel de Controle → grupo NFS-e → Configurações para Emissão*. Selecione primeiro a **inscrição municipal**.

![Tela Configurações para a emissão de NFS-e com o seletor de inscrição municipal e o início do bloco Gerais: usuário e senha de autenticação, cadastro mobiliário, chave primária de autenticação e cliente Id](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/configuracoes-para-emissao-de-nfs-e.png "Configurações para emissão de NFS-e")

## Blocos da tela

| Bloco | O que configura |
| :--- | :--- |
| **Gerais** | Usuário e senha de autenticação, cliente ID, regime especial de tributação, cadastro mobiliário, chave primária de autenticação. Também os padrões do Simples Nacional (abaixo). |
| **Espelho** | Parâmetros do documento auxiliar (impressão) da NFS-e. |
| **Ambientes** | Ambiente, padrão, **versão do município** (ex.: *0.20 - NFS-e Nacional*) e **tipo de processamento** (síncrono/assíncrono). |
| **Avançado** | Retorno de XML/PDF, PDF no webservice de exportação, link de impressão da prefeitura, formato do XML no Webhook, **envio forçado à NFS-e Nacional** e "Manter encoding/namespace no XML?". |
| **Particularidades** | Correção automática de dados do RPS, emissão para **MEI em ambiente nacional**, imprimir autenticidade e impostos federais sem retenção, destacar valor líquido, **"Adequado à NT007?"**, "Concatenar Discriminação e Outras Informações", "Enviar forma pagamento no Padrão IPM?" e outros. |

## Padrões do Simples Nacional (bloco Gerais)

| Campo | Tag | Uso |
| :--- | :--- | :--- |
| Optante Simples Nacional | `OptSN` | Enquadramento no regime. |
| Regime de Apuração Tributária pelo SN para IBS e CBS | `regApIBSCBSSN` | Se IBS/CBS são apurados pelo SN ou pelo regime regular. |
| Código da atividade do Simples Nacional (LC 123/2006) | `CodAtividadeSN` | Anexo da LC 123/2006 da atividade. |

Os três vêm como **"0 - Não Configurado"**.

> [!IMPORTANTE]
> Esses campos valem como **padrão da empresa**: se o XML não trouxer a tag, o InvoiCy usa o valor da tela; se trouxer, vale o XML.

## Campos que mais aparecem no FAQ

| Campo | `Sim` | `Não` |
| :--- | :--- | :--- |
| **Adequado à NT007?** | O InvoiCy soma `ValCSLL`, `ValPISRetido` e `ValCOFINSRetido` e envia no campo de CSLL retida da prefeitura (considera `tpRetPisCofins`; sem ela, `PISRetido`, `COFINSRetido` e `CSLLRetido`). | Repassa como o cliente enviou. |
| **Manter encoding/namespace no XML?** | O XML da NFS-e retornado mantém a declaração de codificação e os namespaces em `NFSe`, `DPS` e `Signature`, como o Padrão Nacional (útil para sistemas contábeis). | Formato sem encoding e namespaces. |

Ver [NT 007](nt007-retencoes-federais.md).

- **Concatenar Discriminação e Outras Informações:** para padrões sem campo de outras informações (CITTA, GESPAM, TIPLAN 2.0, WebISS 2.0).
- **Tipo de processamento:** ver [Emissão sequencial](emissao-sequencial-rps.md) e [Síncrono × assíncrono](processamento-sincrono-assincrono.md).

## Particularidades condicionadas

Em *Painel de Controle → NFS-e → Cadastro de Particularidades* você trata tags do XML **antes do envio à prefeitura**, por inscrição municipal, e pode replicar para outras empresas. Serve para corrigir diferenças de um município específico sem mudar o ERP.

![Tela Cadastro de Particularidades de NFS-e com o seletor de inscrição municipal, os botões Salvar, Nova Particularidade e Replicar e a lista de tags com as colunas Ignorar, Condicionada, Valor Condicionado, Valor fixo e Valor Tag](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/cadastro-de-particularidades.png "Cadastro de particularidades")

> [!PASSOS]
> 1. Abra *Painel de Controle → NFS-e → Cadastro de Particularidades* e selecione a inscrição municipal.
> 2. Na tag que quer tratar, marque **"Condicionada"**.
> 3. Preencha **Valor Condicionado**, **Valor Fixo** ou **Valor Tag** conforme o cenário da tabela abaixo.

| Cenário | Configuração | Resultado |
| :--- | :--- | :--- |
| **Trocar valor** (cliente envia `Art = 1`, prefeitura espera `2`) | Valor Condicionado = `1`; Valor Fixo = `2` | Todo `1` vira `2`. |
| **Herdar de outra tag** (`Art` vazio deve receber `CodObra`) | Valor Condicionado e Valor Fixo vazios; Valor Tag = `CodObra` (ícone de olho) | Se `Art` vier vazio, recebe `CodObra`; se vier preenchido, mantém. |
| **Valor padrão** (`Art` sempre `1234` quando ausente) | Valor Condicionado vazio; Valor Fixo = `1234` | Se a tag não vier, é criada com `1234`. |

> [!DETALHES] Exemplos de cada cenário na tela
> **Trocar valor**
>
> ![Linha da tag Abatimento com Condicionada marcada, Valor Condicionado 1 e Valor fixo 2](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/herdar-tag.png "Trocar valor")
>
> **Herdar de outra tag**
>
> ![Linha da tag Abatimento com Condicionada marcada, Valor Condicionado e Valor fixo vazios e o ícone de olho para escolher a tag de origem](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/trocar-valor.png "Herdar tag")
>
> **Valor padrão**
>
> ![Linha da tag Abatimento com Condicionada marcada, Valor Condicionado vazio e Valor fixo 1234](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/valor-padrao.png "Valor padrão")

## Fontes

- [Configurações para Emissão: NFS-e](https://desenvolvedores.migrate.info/Sistema/PaineldeControle/ConfiguracoesparaEmissao/NFSe/) (16/10/2026)
- [Cadastro de Particularidades dos Padrões](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/Funcionalidades/CadastrodeParticularidadesdosPadroes/) (06/03/2026)
