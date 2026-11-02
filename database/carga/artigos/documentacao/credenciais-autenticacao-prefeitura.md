# Credenciais de autenticação da prefeitura

## Resumo

Cada padrão autentica de um jeito: certificado digital, usuário e senha, token ou chave. No InvoiCy, as credenciais ficam em **Painel de controle → NFS-e → Configurações para emissão** (bloco **Gerais**). Ver [Configurações de emissão](configuracoes-de-emissao.md).

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
| Usuário e senha do webservice | **Usuário Autent.** e **Senha Autent.** | CENTI, CONAM, Caucaia, DSFNET, Elotech, Fiorilli, GLC Consultoria, IPM, RLZ, SMARAPDISIL |
| ApiKey | Configuração do provedor | GESPAM |
| Certificado digital A1 | Cadastro de certificado da empresa | Betha, Coplan, DSFNET, E&L, Elotech (assina o SOAP), Emissor Nacional, Equiplano, GISS, Governo Digital, IPM, Nota Salvador, RLZ, WebISS |

## Onde obter e como preencher

| Provedor | Como obter | Como preencher |
| :--- | :--- | :--- |
| CENTI | Fornecido pela prefeitura. | Usuário e senha. |
| CONAM | Credenciamento na prefeitura. **Código do Usuário:** *Sistema → Segurança → Editar Perfil*, aba "Código do usuário para login em Web Services". **Código do Contribuinte:** *Contribuintes → Cadastro de Contribuintes* → consultar o CNPJ (lupa) → menu **Código**. Erro se estiver errado: "Usuário/Contribuinte Não Identificado". | **Usuário Autent.** = Código do Usuário; **Senha Autent.** = Código do Contribuinte. Homologação e produção têm chaves diferentes. |
| DSFNET | Subusuário em *Minha Empresa → Vincular Subusuários*. | **Usuário Autent.** = CPF/CNPJ do subusuário ativo (de preferência o mesmo do certificado). |
| E&L | Token gerado no portal após o login. | Chave primária de autenticação. |
| e-Governe ISS | *Notas Fiscais → Web Service – Gerar Chave Autenticação*. | **Chave Primária Autenticação**. |
| Elotech | *Informações da Empresa → cadeado*. [print](../../assets/imagens/elotech/senha-do-webservice-elotech.png) | **Usuário** = CPF do responsável; **Senha** = senha gerada. |
| Fiorilli | Login do portal. [print](../../assets/imagens/fiorilli/usuario-e-senha-fiorilli.png) | Usuário e senha. |
| GESPAM | ApiKey no portal; vale só após **"Confirmar Uso da Chave"**; nova chave invalida a anterior; exibida uma vez; uma por ambiente. | ApiKey. |
| GLC Consultoria | E-mail a **suporte@sigissweb.com**, ou *Gerenciamento → Usuários → cadeado da coluna "Web. Serv."* (usuário diferente do SigissWeb). | Usuário e senha. |
| IPM | Login do portal, após autorização para webservice no autoatendimento. | Usuário e senha (*Autenticação da Prefeitura*). |
| ISSNET | Liberação pela Nota Control (E157 = não liberado). | - |
| RLZ | Menu **Token Web Service**. [print](../../assets/imagens/rlz/token-web-service-rlz.jpg) | Chave primária + usuário e senha. |
| Sigcorp | IM + senha do prestador no município. | HTTP Basic + cabeçalho `X-NFSe-Prestador-CNPJ`. |
| SMARAPDISIL (WS) | Fornecido pela prefeitura. | **Usuário** = CNPJ; **Senha** com letras **maiúsculas**. |
| Tributos Municipais WS | Plataforma *connect* da Tributos. | Chave primária de autenticação. |

## Erros típicos de credencial

- **HTTP 401** em todas as requisições: credencial errada, não confirmada, substituída ou de outro ambiente.
- Credencial de **homologação** não funciona em **produção** (CONAM, GESPAM).
- **inv0326:** certificado não encontrado, inativo ou vencido.

Detalhes em [observações por provedor](../../02-observacoes/observacoes-provedores.md).
