# Credenciais de autenticação da prefeitura

> [!RESUMO]
> Cada padrão autentica de um jeito: certificado digital, usuário e senha, token ou chave. No InvoiCy, as credenciais ficam em *Painel de controle → NFS-e → Configurações para emissão*, bloco **Gerais**.
> O passo a passo de cada provedor, com os prints do portal da prefeitura, fica na página do provedor. Este artigo reúne o que vale para todos e os provedores que ainda não têm página.

Ver também [Configurações de emissão](configuracoes-de-emissao.md).

## Certificado digital

- **Web Service e API REST:** exigem certificado **A1**.
- **A3:** aceito somente pelo **InvoiCy Conector** (e na digitação).
- **Certificado da matriz:** a filial pode emitir com o certificado da matriz (mesma raiz de CNPJ). A **consulta de notas recebidas** e o acesso ao **portal nacional** exigem o certificado do próprio estabelecimento.
- Certificado de outra raiz de CNPJ gera, no Padrão Nacional, "A assinatura deve ser feita com o certificado digital do emitente da DPS".
- Cadastro e troca de certificado: <https://desenvolvedores.migrate.info/Sistema/PaineldeControle/Certificados/>

## Onde informar cada credencial no InvoiCy

| Credencial | Campo no InvoiCy | Provedores |
| :--- | :--- | :--- |
| Token / chave do prestador | **Chave primária de autenticação** | E&L, e-Governe ISS, RLZ, Tributos Municipais WS |
| Usuário e senha do webservice | **Usuário de autenticação** e **Senha de autenticação** | CENTI, CONAM, Caucaia, DSFNET, Elotech, Fiorilli, GLC Consultoria, IPM, RLZ, Sigcorp, SMARAPDISIL |
| ApiKey | Configuração do provedor | GESPAM |
| Certificado digital A1 | Cadastro de certificado da empresa | Betha, Coplan, DSFNET, E&L, Elotech (assina o SOAP), Emissor Nacional, Equiplano, GISS, Governo Digital, IPM, Nota Salvador, RLZ, WebISS |

### Usuário e senha de autenticação

Em *Painel de controle → NFS-e → Configurações para emissão de NFS-e*, selecione a inscrição municipal e preencha **Usuário de autenticação** e **Senha de autenticação**.

![Tela Configurações para a emissão de NFS-e com os campos Usuário de autenticação e Senha de autenticação destacados no bloco Gerais](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/usuario-e-senha-de-autenticacao.png "Usuário e senha de autenticação")

### Chave primária de autenticação

Na mesma tela, o token ou a chave do prestador vai em **Chave primária de autenticação**.

![Tela Configurações para a emissão de NFS-e com o campo Chave primária de autenticação destacado](../../assets/imagens/configuracoes-para-emissao-de-nfs-e-e-particularidades/chave-primaria-de-autenticacao.png "Chave primária de autenticação")

## Provedores sem página própria

| Provedor | Como obter | Como preencher |
| :--- | :--- | :--- |
| Caucaia | Login do portal da prefeitura. | **Usuário de autenticação** = CNPJ/CPF sem máscara; **Senha de autenticação** = senha do portal. |
| CENTI | Fornecido pela prefeitura. | Usuário e senha. |
| CONAM | Credenciamento na prefeitura. **Código do Usuário:** *Sistema → Segurança → Editar Perfil*, aba "Código do usuário para login em Web Services". **Código do Contribuinte:** *Contribuintes → Cadastro de Contribuintes* → consultar o CNPJ (lupa) → menu **Código**. Erro se estiver errado: "Usuário/Contribuinte Não Identificado". | **Usuário de autenticação** = Código do Usuário; **Senha de autenticação** = Código do Contribuinte. Homologação e produção têm chaves diferentes. |
| e-Governe ISS | *Notas Fiscais → Web Service - Gerar Chave Autenticação*. | **Chave Primária Autenticação**. |
| GLC Consultoria | E-mail a **suporte@sigissweb.com**, ou *Gerenciamento → Usuários → cadeado da coluna "Web. Serv."* (usuário diferente do SigissWeb). | Usuário e senha. |
| SMARAPDISIL (WS) | Fornecido pela prefeitura. | **Usuário** = CNPJ; **Senha** com letras **maiúsculas**. |
| Tributos Municipais WS | Plataforma *connect* da Tributos. | Chave primária de autenticação. |

## Erros típicos de credencial

- **HTTP 401** em todas as requisições: credencial errada, não confirmada, substituída ou de outro ambiente.
- Credencial de **homologação** não funciona em **produção** (CONAM, GESPAM).
- **inv0326:** certificado não encontrado, inativo ou vencido.

Detalhes em [observações por provedor](../../02-observacoes/observacoes-provedores.md).
