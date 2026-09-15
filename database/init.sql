-- =====================================================================================
-- database/init.sql — Codex
-- Roda uma unica vez, na inicializacao do container (volume vazio), como POSTGRES_USER
-- (superuser de bootstrap). Cria: extensoes, role de aplicacao, schema dedicado,
-- configuracao de texto e a funcao de normalizacao de busca.
-- Copiado verbatim de modelo-dados.md §9 — nao redigitar, nao "melhorar".
-- =====================================================================================

-- 1. Extensoes — exigem superuser, por isso vivem aqui e nao em migration.
CREATE EXTENSION IF NOT EXISTS unaccent      WITH SCHEMA public;
CREATE EXTENSION IF NOT EXISTS pg_trgm       WITH SCHEMA public;
CREATE EXTENSION IF NOT EXISTS fuzzystrmatch WITH SCHEMA public;

-- 2. Role de aplicacao — NUNCA superuser (rules/security.md, rules/database.md).
--    Senha lida de variavel de ambiente do container (nunca hardcoded no arquivo).
\getenv codex_app_password CODEX_APP_DB_PASSWORD

DO $do$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'codex_user') THEN
        EXECUTE 'CREATE ROLE codex_user LOGIN PASSWORD '
            || quote_literal(:'codex_app_password')
            || ' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT';
    END IF;
END
$do$;

-- 3. Schema dedicado, de propriedade do role de aplicacao — codex_user tem CREATE/USAGE
--    apenas dentro de codex, nunca em outro schema.
CREATE SCHEMA IF NOT EXISTS codex AUTHORIZATION codex_user;

-- 4. Isolamento do schema public (defesa em profundidade — so USAGE/EXECUTE, nunca CREATE).
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO codex_user;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO codex_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO codex_user;

-- 5. search_path padrao do role de aplicacao — objetos sem schema explicito caem em codex.
ALTER ROLE codex_user SET search_path = codex, public;

-- 6. Infraestrutura de busca — criada como codex_user, para ja nascer de propriedade dele
--    (as migrations seguintes, tambem rodadas como codex_user, dependem destes objetos existirem).
SET ROLE codex_user;

CREATE OR REPLACE FUNCTION codex.f_unaccent(text)
RETURNS text
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
STRICT
AS $$
    SELECT public.unaccent('public.unaccent'::regdictionary, $1)
$$;

COMMENT ON FUNCTION codex.f_unaccent(text) IS
    'Wrapper IMMUTABLE sobre unaccent() (STABLE por padrao), necessario para uso em coluna '
    'GENERATED STORED. Seguro enquanto o dicionario unaccent nao mudar; se mudar, reconstruir '
    'codex.itensbusca por inteiro.';

CREATE TEXT SEARCH CONFIGURATION codex.codex_pt (COPY = pg_catalog.portuguese);

ALTER TEXT SEARCH CONFIGURATION codex.codex_pt
    ALTER MAPPING FOR hword, hword_part, word
    WITH unaccent, portuguese_stem;

CREATE OR REPLACE FUNCTION codex.normalizar_busca(entrada text)
RETURNS text
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
AS $$
    SELECT
        trim(regexp_replace(lower(codex.f_unaccent(coalesce(entrada, ''))), '\s+', ' ', 'g'))
        || ' ' ||
        trim(regexp_replace(
            regexp_replace(
                lower(codex.f_unaccent(coalesce(entrada, ''))),
                '[]/\_.:;,|()<>[-]', ' ', 'g'
            ),
            '\s+', ' ', 'g'
        ))
        || ' ' ||
        regexp_replace(lower(codex.f_unaccent(coalesce(entrada, ''))), '[^a-z0-9]', '', 'g')
$$;

COMMENT ON FUNCTION codex.normalizar_busca(text) IS
    'Unica funcao de normalizacao de busca do Codex (indexacao via coluna GENERATED e consulta '
    'via Infrastructure/Busca). Produz 3 formas concatenadas: bruta, quebrada (separador colado '
    '-> espaco) e compactada (so alfanumerico). IMMUTABLE. Mudar esta funcao exige reconstruir '
    'codex.itensbusca.';

RESET ROLE;

-- 7. codex.set_atualizadoem(), tabelas, indices e demais objetos de schema versionado ficam
--    nas migrations do codex-backend (Codex.Infrastructure/Data/Migrations), nao aqui.
