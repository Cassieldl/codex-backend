# Codex em produção — procedimento e checklist

Escrito para quem vai publicar o Codex: você opera infraestrutura, conhece contêineres e não
precisa conhecer o código da aplicação.

Este documento cobre o que **não depende** da escolha de provedor. A parte específica de nuvem
(Cloud Run, GKE, Cloud SQL) entra quando o ambiente for decidido — até lá, tudo abaixo vale
para qualquer lugar que rode imagens OCI.

---

## 1. O que sobe

| Componente | Imagem | Porta interna | Não-root |
|---|---|---|---|
| API | construída de `codex-backend/Dockerfile` | 8080 | sim (`$APP_UID`) |
| Portal | construída de `codex-frontend/Dockerfile` | 8080 | sim (uid 101) |
| Postgres | serviço gerenciado do provedor | 5432 | — |
| Keycloak | instância existente | — | — |

O `docker-compose.yml` do repositório é **de desenvolvimento**: sobe Postgres e um Keycloak
`start-dev` com realm importado de um JSON. Nada dele vai para produção.

---

## 2. Construir as imagens

### API

```bash
cd codex-backend
docker build -t codex-backend:<versao> .
```

Não recebe build arg: toda a configuração é lida do ambiente em runtime.

### Portal

```bash
cd codex-frontend
docker build -t codex-frontend:<versao> \
  --build-arg VITE_API_URL=https://codex-api.exemplo.com.br/api/v1 \
  --build-arg VITE_KEYCLOAK_URL=https://auth.exemplo.com.br \
  --build-arg VITE_KEYCLOAK_REALM=<realm> \
  --build-arg VITE_KEYCLOAK_CLIENT_ID=codex-front \
  .
```

⚠️ **A imagem do portal é por ambiente.** O Vite substitui `VITE_*` dentro do bundle durante o
build — as URLs ficam gravadas no JavaScript entregue ao navegador. Promover a imagem de staging
para produção publicaria um portal apontando para a API de staging. O build falha de propósito se
algum desses argumentos faltar.

Corolário: **nada secreto em `VITE_*`** — qualquer visitante lê o bundle.

---

## 3. Configurar

`.env.production.example` de cada repositório lista todas as chaves, com comentário no que muda
em relação ao desenvolvimento. Em ambiente gerenciado, esses valores vêm do cofre de segredos do
provedor, nunca de um arquivo em disco.

Quatro variáveis merecem atenção porque **falham em silêncio quando erradas**:

| Variável | Errar significa |
|---|---|
| `Cors__OrigemPermitida` | Origem diferente da do portal → toda chamada do navegador é bloqueada pelo CORS |
| `AllowedHosts` | A aplicação **recusa subir** com `*` fora de Development — é proposital |
| `ProxyReverso__KnownProxies` / `KnownNetworks` | Sem declarar o ingress que termina TLS: o HSTS não é emitido e o rate limiting vê o IP do proxy, colapsando em uma partição única. Nunca use curinga: seria aceitar IP forjado pelo cliente |
| `CSP_CONNECT_SRC` (portal) | Sem a API e o Keycloak nesta lista, a política de segurança do navegador bloqueia o próprio login |
| `CSP_FRAME_SRC` (portal) | Origem do Keycloak, cujo iframe verifica a sessão. Sem ela o container do portal recusa subir |

---

## 4. Banco de dados

**Migration não é aplicada automaticamente**, por decisão registrada no PRD da 004 (D3): migrar
no start transformaria todo deploy numa alteração de schema não autorizada, e rollback de
aplicação não desfaz schema.

O procedimento recomendado para ambiente remoto é gerar um script idempotente, **revisar** e
executar de forma controlada:

```bash
cd codex-backend
dotnet ef migrations script --idempotent \
  --project src/Codex.Infrastructure \
  --startup-project src/Codex.Infrastructure \
  --output migracao.sql
```

O script é idempotente: reaplicá-lo não repete o que já foi aplicado. Revise antes de executar —
é a última chance de ver o DDL que vai rodar.

### Preparar o banco (uma vez, antes da primeira migration)

`database/init.sql` cria as extensões, o papel `codex_user`, o schema `codex` (de propriedade dele)
e as funções de busca de que as migrations dependem. Em banco gerenciado ele roda com o papel de
administração da instância — no Cloud SQL, o `postgres`, que **não** é superusuário:

```bash
CODEX_APP_DB_PASSWORD='<senha do codex_user>' \
  psql "host=<instância> dbname=codex_db user=postgres sslmode=require" \
  -v ON_ERROR_STOP=1 -f database/init.sql
```

- Exige `psql` 15 ou mais novo (`\getenv`).
- Avisos `no privileges were granted for ...` são esperados: as funções das extensões já são
  executáveis por qualquer papel.
- A senha do `codex_user` não aparece na saída.
- Testado num Postgres 18 com o papel de bootstrap sem superusuário (`CREATEROLE`, dono do banco),
  que é o modelo do Cloud SQL.

### Quem roda a migration

`codex_user` é o dono do schema `codex`, e é com ele que o script de migration roda — é o mesmo
papel da aplicação. Ele não é superusuário e não alcança outro schema, mas pode alterar as próprias
tabelas: separar um papel dono (DDL) de um papel da aplicação (só DML) é a melhoria natural, ainda
não feita.

---

## 5. Sondas

| Rota | Responde | Uso |
|---|---|---|
| `GET /api/v1/saude/vivo` | 200 sempre que o processo responde | Liveness |
| `GET /api/v1/saude/pronto` | 200, ou 503 se o banco não aceita conexão | Readiness |

Configure o orquestrador para usar as **duas**, e não a mesma nas duas posições: banco fora do ar
deve tirar a instância do balanceador (readiness), nunca matar e recriar o processo em laço
(liveness). Nenhuma das duas é cacheada, e nenhuma revela nome de dependência ou exceção.

---

## 6. Checklist antes de abrir ao público

Respondido contra o que o código faz hoje. O que **não** está coberto está marcado.

| Item | Estado |
|---|---|
| HTTPS + HSTS | ✅ emitido pelo middleware — **depende** de `ProxyReverso__*` declarar o ingress |
| CORS restritivo (nunca `*`) | ✅ origem única obrigatória; a aplicação recusa subir com configuração inválida |
| Swagger fora de produção | ✅ registrado só em Development |
| Headers de segurança | ✅ API pelo middleware; portal pelo nginx (CSP, nosniff, Referrer-Policy, Permissions-Policy) |
| Rate limiting | ✅ `/busca` com política própria; demais rotas anônimas na política da vitrine |
| Logs estruturados sem dado sensível | ✅ Serilog em JSON, com `traceId`; token e segredo nunca logados |
| Dependências sem CVE conhecido | ✅ medido em 2026-09-22: `npm audit` → 0 vulnerabilidades; `dotnet list package --vulnerable --include-transitive` → nenhum pacote vulnerável nos 5 projetos. **Repetir a cada publicação** — a resposta envelhece |
| Segredos em cofre | ⚠️ **depende do provedor** — nenhum segredo no repositório hoje |
| Token fora de `localStorage` | ✅ keycloak-js em memória |
| Validação no servidor | ✅ FluentValidation com filtro global |
| DTOs por operação (sem mass assignment) | ✅ |
| Queries parametrizadas | ✅ EF/LINQ; nenhum SQL concatenado |
| Testes de segurança (roles, ownership) | ✅ pipeline real cobre 401/403 por rota |
| Sondas de saúde | ✅ liveness e readiness separados |
| Backup e retenção do banco | ⚠️ **não coberto** — política do provedor |
| Monitoramento e alerta | ⚠️ **não coberto** — sem APM configurado |
| Smoke ponta a ponta após publicar | ⚠️ **obrigatório** — ver §7 |

---

## 7. Depois de publicar: o smoke que falta desde a 003

O portal e a API nunca conversaram fora da suíte de testes. É a única camada que pega divergência
de contrato sozinha — dois defeitos da demanda 003 só apareceram porque alguém foi olhar.

Com tudo no ar, confirme nesta ordem:

1. `GET /api/v1/saude/pronto` → **200**.
2. `GET /api/v1/home` → **200**, com `Cache-Control: public, max-age=60`, e um JSON com
   `data.comunicados`, `data.provedoresPorCategoria`, `data.layout`, `data.artigosRecentes`.
3. Abrir a raiz do portal → a Home carrega, sem erro no console do navegador (CSP bloqueada
   aparece ali).
4. Buscar qualquer termo → a tela de busca responde.
5. Abrir a área interna → redireciona para o Keycloak, e volta autenticado.
6. `GET /api/v1/interno/provedores` sem token → **401**.

Qualquer passo que falhe é bloqueante: pare e corrija antes de divulgar a URL.
