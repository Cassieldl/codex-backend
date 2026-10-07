# Codex Backend

[![CI](https://github.com/Cassieldl/codex-backend/actions/workflows/ci.yml/badge.svg)](https://github.com/Cassieldl/codex-backend/actions/workflows/ci.yml)

API do Codex — Portal Técnico de Integração NFS-e (Migrate). .NET 10 LTS + Clean Architecture +
PostgreSQL 18. Autenticação via Keycloak (realm `codex`, local em dev).

## Stack

- .NET 10 LTS, Clean Architecture (`Codex.Domain` → `Codex.Application` → `Codex.Infrastructure`/`Codex.API`)
- PostgreSQL 18 (EF Core 10 + Npgsql)
- Keycloak (dev local via `docker-compose.yml`, realm importado de `docker/keycloak/realm-codex.json`)
- Testes: xUnit, Moq, FluentAssertions 7.x (nunca 8+ — exige licença comercial), Bogus, Testcontainers.PostgreSql

## Estrutura

```
src/
├── Codex.API/              # Controllers, Program.cs, middleware, extension methods de wiring
├── Codex.Application/      # Services, DTOs, interfaces, validators (FluentValidation)
├── Codex.Domain/           # Entidades, enums, value objects, exceções — sem dependências
├── Codex.Infrastructure/   # DbContext, Configurations, Migrations, repositórios, busca
└── Codex.Tests/            # Unit/ e Integration/ (Testcontainers)
```

## Como rodar (dev local)

### 1. Subir Postgres + Keycloak

```bash
cp .env.example .env   # edite as senhas se quiser
docker compose up -d
```

Aguarde o Postgres ficar saudável (`docker compose ps`) antes do próximo passo.

### 2. ATENÇÃO — `.env` não é lido por `dotnet run`/`dotnet ef`

O arquivo `.env` só é consumido pelo `docker compose` (via `env_file`/interpolação do Compose).
`dotnet run` e `dotnet ef` **não leem `.env` automaticamente** — as variáveis precisam estar
exportadas de verdade no shell, ou configuradas em `appsettings.Development.json` (já populado
com os defaults do `docker-compose.yml` para desenvolvimento local).

Se preferir passar por variável de ambiente explicitamente, **atenção ao hífen**:
`Keycloak__auth-server-url` tem hífen, e o bash não aceita hífen em `export NOME=valor`. Use o
padrão abaixo (uma lição que já custou 1h nesta base):

```bash
env "Keycloak__auth-server-url=http://localhost:8080" \
    "Keycloak__realm=codex" \
    "Keycloak__resource=codex-backend" \
    dotnet run --project src/Codex.API
```

### 3. Aplicar as migrations

```bash
dotnet ef database update --project src/Codex.Infrastructure --startup-project src/Codex.Infrastructure
```

(`--startup-project` aponta para `Codex.Infrastructure` porque é lá que está o
`IDesignTimeDbContextFactory` — não precisa da API rodando para gerar/aplicar migrations.)

### 4. Rodar a API

```bash
dotnet run --project src/Codex.API
```

Swagger disponível em `/swagger` **apenas em `Development`** — nunca exposto em produção sem
autenticação (rules/security.md A05).

### Ordem de subida resumida

`docker compose up -d` → aguardar Postgres saudável → `dotnet ef database update` → `dotnet run`.

Keycloak já sobe com o realm `codex` importado (`docker/keycloak/realm-codex.json`) — client
público `codex-front`, client confidencial `codex-backend` com todas as roles da 001, e um
usuário de teste (`codex.dev` / senha `codex.dev`) com todas elas atribuídas. Não é preciso
configurar nada manualmente no admin console em dev.

## Testes

```bash
dotnet test
```

Testes de integração (`Codex.Tests/Integration/`) usam Testcontainers — sobem um Postgres 18
descartável automaticamente, não dependem do `docker-compose.yml` deste repositório. Docker
Desktop precisa estar rodando na máquina.

## Carga de conteúdo

`database/carga/emissor-nacional/importar.mjs` carrega o provedor **Emissor Nacional** (versão 0.20)
pela API interna: erros e avisos da Sefin, o vocabulário do Layout InvoiCy 8 (545 campos), os campos
da versão e o artigo de emissão judicial. Escreve sempre pela API — nunca por SQL —, então slug,
índice de busca e regras de negócio são os mesmos do produto.

- **Sincroniza**, não só cria: rodar de novo corrige o que mudou e não duplica nada.
- **Não publica nada.** Publicar é decisão editorial, feita na área interna.
- Material do InvoiCy (XSD de entrada, extrato da planilha v8, mapeamento das telas do provedor) fica
  versionado na pasta. O material da Sefin (leiaute e regras da DPS) é lido de uma pasta local,
  passada como argumento.

Com a API e o Keycloak no ar:

```bash
CODEX_DEV_USUARIO=codex.dev CODEX_DEV_SENHA=codex.dev \
  node database/carga/emissor-nacional/importar.mjs <pasta-do-material-da-sefin>
```

| Variável | Uso |
|---|---|
| `CODEX_DEV_USUARIO` / `CODEX_DEV_SENHA` | Login no Keycloak local (fluxo PKCE do `codex-front`; o realm não tem login direto por senha) |
| `CODEX_TOKEN` | Token pronto, no lugar do login — para ambiente que não seja o local |
| `CODEX_API_URL` / `CODEX_KEYCLOAK_URL` | Endereços, se diferentes dos locais |

Testes da carga (Node, sem dependência): `node --test <arquivo>.test.mjs` em
`database/carga/emissor-nacional/`. Passar o diretório falha nesta versão do Node — passe o arquivo.

O que ainda depende de resposta do time do InvoiCy está no relatório de completude da demanda 008
(`.dev-team/demandas/008-campos-emissor-nacional/relatorio-completude.md`, fora deste repositório).

## Variáveis de ambiente

Ver `.env.example`. Principais:

| Variável | Uso |
|---|---|
| `ConnectionStrings__Default` | Connection string da aplicação — usa `codex_user`, nunca `POSTGRES_USER` (superuser) |
| `CODEX_APP_DB_PASSWORD` | Senha do role de aplicação `codex_user`, lida por `database/init.sql` via `\getenv` |
| `Keycloak__auth-server-url` / `Keycloak__realm` / `Keycloak__resource` | Configuração do Keycloak |
| `Cors__OrigemPermitida` | Única origem permitida no CORS (nunca `*`) |
| `Busca__LimiarAutocorrecao` / `Busca__TimeoutMs` / `Busca__OrcamentoRequisicaoMs` | Configuração da busca (nunca hardcode no código). Os dois tempos aceitam só 250..30000 ms |
| `AllowedHosts` | Hosts servidos. Fora de Development a aplicação recusa subir com `*` |
| `ProxyReverso__KnownProxies__N` / `KnownNetworks__N` | Ingress que termina TLS. Sem ele não sai HSTS e o rate limit vê um IP só |
| `Keycloak__ValidAudiences__N` / `Keycloak__MetadataAddress` / `Keycloak__RequireHttpsMetadata` | Audiência aceita; endereço interno do discovery; dispensa explícita de HTTPS em rede interna |
| `Comunicados__ExpiracaoHabilitada` / `Comunicados__IntervaloExpiracaoSegundos` | Job que tira do ar o comunicado vencido |

Produção (imagem, ordem de subida, checklist e smoke): `docs/producao.md`, com os valores em `.env.production.example`.

## Migrations

Nunca aplicar migration em ambiente remoto sem autorização explícita. Localmente:

```bash
# Criar nova migration
dotnet ef migrations add NomeDaMigration --project src/Codex.Infrastructure --startup-project src/Codex.Infrastructure

# Aplicar
dotnet ef database update --project src/Codex.Infrastructure --startup-project src/Codex.Infrastructure
```
