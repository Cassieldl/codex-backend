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

## Variáveis de ambiente

Ver `.env.example`. Principais:

| Variável | Uso |
|---|---|
| `ConnectionStrings__Default` | Connection string da aplicação — usa `codex_user`, nunca `POSTGRES_USER` (superuser) |
| `CODEX_APP_DB_PASSWORD` | Senha do role de aplicação `codex_user`, lida por `database/init.sql` via `\getenv` |
| `Keycloak__auth-server-url` / `Keycloak__realm` / `Keycloak__resource` | Configuração do Keycloak |
| `Cors__OrigemPermitida` | Única origem permitida no CORS (nunca `*`) |
| `Busca__LimiarAutocorrecao` / `Busca__TimeoutMs` | Configuração da busca (nunca hardcode no código) |

## Migrations

Nunca aplicar migration em ambiente remoto sem autorização explícita. Localmente:

```bash
# Criar nova migration
dotnet ef migrations add NomeDaMigration --project src/Codex.Infrastructure --startup-project src/Codex.Infrastructure

# Aplicar
dotnet ef database update --project src/Codex.Infrastructure --startup-project src/Codex.Infrastructure
```
