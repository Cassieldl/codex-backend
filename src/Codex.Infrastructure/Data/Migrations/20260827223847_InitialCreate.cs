using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "codex");

            // Função de auditoria compartilhada (modelo-dados.md §3) — SQL bruto, referenciada
            // pelo trigger BEFORE UPDATE de toda tabela auditada abaixo. Precisa existir antes
            // de qualquer CREATE TRIGGER (ver final deste método).
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION codex.set_atualizadoem()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.atualizadoem := now();
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.CreateTable(
                name: "erros",
                schema: "codex",
                columns: table => new
                {
                    erroid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    causaprovavel = table.Column<string>(type: "text", nullable: true),
                    solucaosugerida = table.Column<string>(type: "text", nullable: true),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_erros", x => x.erroid);
                    table.CheckConstraint("ck_codex_erros_origem", "origem IN ('Prefeitura', 'InvoiCy', 'Schema')");
                });

            migrationBuilder.CreateTable(
                name: "provedores",
                schema: "codex",
                columns: table => new
                {
                    provedorid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    identificadorinvoicy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    categoria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_provedores", x => x.provedorid);
                    table.CheckConstraint("ck_codex_provedores_categoria", "categoria IN ('Nacional', 'Proprio')");
                });

            migrationBuilder.CreateTable(
                name: "redirecionamentos",
                schema: "codex",
                columns: table => new
                {
                    redirecionamentoid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    origem = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    destino = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    tipoitem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_redirecionamentos", x => x.redirecionamentoid);
                    table.CheckConstraint("ck_codex_redirecionamentos_tipoitem", "tipoitem IN ('provedor', 'versaoprovedor', 'erro', 'campolayout')");
                });

            migrationBuilder.CreateTable(
                name: "termosbusca",
                schema: "codex",
                columns: table => new
                {
                    termobuscaid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    termo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_termosbusca", x => x.termobuscaid);
                    table.CheckConstraint("ck_codex_termosbusca_termo_minimo", "length(termo) >= 3");
                });

            migrationBuilder.CreateTable(
                name: "versoeslayout",
                schema: "codex",
                columns: table => new
                {
                    versaolayoutid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    rotulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_versoeslayout", x => x.versaolayoutid);
                    table.CheckConstraint("ck_codex_versoeslayout_estado", "estado IN ('Prevista', 'Vigente', 'Anterior')");
                });

            migrationBuilder.CreateTable(
                name: "variantesmensagem",
                schema: "codex",
                columns: table => new
                {
                    variantemensagemid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    erroid = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_variantesmensagem", x => x.variantemensagemid);
                    table.ForeignKey(
                        name: "fk_codex_variantesmensagem_erroid",
                        column: x => x.erroid,
                        principalSchema: "codex",
                        principalTable: "erros",
                        principalColumn: "erroid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "aliasesprovedor",
                schema: "codex",
                columns: table => new
                {
                    aliasprovedorid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    provedorid = table.Column<int>(type: "integer", nullable: false),
                    alias = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_aliasesprovedor", x => x.aliasprovedorid);
                    table.ForeignKey(
                        name: "fk_codex_aliasesprovedor_provedorid",
                        column: x => x.provedorid,
                        principalSchema: "codex",
                        principalTable: "provedores",
                        principalColumn: "provedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "camposlayout",
                schema: "codex",
                columns: table => new
                {
                    campolayoutid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    versaolayoutid = table.Column<int>(type: "integer", nullable: false),
                    versaolayoutdescontinuadoid = table.Column<int>(type: "integer", nullable: true),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    caminhoxml = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    significado = table.Column<string>(type: "text", nullable: false),
                    grupo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_camposlayout", x => x.campolayoutid);
                    table.CheckConstraint("ck_codex_camposlayout_descontinuado_consistente", "(estado = 'Ativo' AND versaolayoutdescontinuadoid IS NULL) OR (estado = 'Descontinuado' AND versaolayoutdescontinuadoid IS NOT NULL)");
                    table.CheckConstraint("ck_codex_camposlayout_estado", "estado IN ('Ativo', 'Descontinuado')");
                    table.ForeignKey(
                        name: "fk_codex_camposlayout_versaolayoutdescontinuadoid",
                        column: x => x.versaolayoutdescontinuadoid,
                        principalSchema: "codex",
                        principalTable: "versoeslayout",
                        principalColumn: "versaolayoutid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_camposlayout_versaolayoutid",
                        column: x => x.versaolayoutid,
                        principalSchema: "codex",
                        principalTable: "versoeslayout",
                        principalColumn: "versaolayoutid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versoesprovedor",
                schema: "codex",
                columns: table => new
                {
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    provedorid = table.Column<int>(type: "integer", nullable: false),
                    versaolayoutid = table.Column<int>(type: "integer", nullable: false),
                    rotulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    verificadoem = table.Column<DateOnly>(type: "date", nullable: true),
                    introducao = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_versoesprovedor", x => x.versaoprovedorid);
                    table.ForeignKey(
                        name: "fk_codex_versoesprovedor_provedorid",
                        column: x => x.provedorid,
                        principalSchema: "codex",
                        principalTable: "provedores",
                        principalColumn: "provedorid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_versoesprovedor_versaolayoutid",
                        column: x => x.versaolayoutid,
                        principalSchema: "codex",
                        principalTable: "versoeslayout",
                        principalColumn: "versaolayoutid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "erroscampos",
                schema: "codex",
                columns: table => new
                {
                    erroid = table.Column<int>(type: "integer", nullable: false),
                    campolayoutid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_erroscampos", x => new { x.erroid, x.campolayoutid });
                    table.ForeignKey(
                        name: "fk_codex_erroscampos_campolayoutid",
                        column: x => x.campolayoutid,
                        principalSchema: "codex",
                        principalTable: "camposlayout",
                        principalColumn: "campolayoutid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_erroscampos_erroid",
                        column: x => x.erroid,
                        principalSchema: "codex",
                        principalTable: "erros",
                        principalColumn: "erroid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos",
                schema: "codex",
                columns: table => new
                {
                    avisoid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    provedorid = table.Column<int>(type: "integer", nullable: true),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: true),
                    severidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corpo = table.Column<string>(type: "text", nullable: false),
                    vigenciainicio = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_avisos", x => x.avisoid);
                    table.CheckConstraint("ck_codex_avisos_escopo_xor", "(provedorid IS NOT NULL AND versaoprovedorid IS NULL) OR (provedorid IS NULL AND versaoprovedorid IS NOT NULL)");
                    table.CheckConstraint("ck_codex_avisos_severidade", "severidade IN ('Critico', 'Alerta', 'Informativo')");
                    table.ForeignKey(
                        name: "fk_codex_avisos_provedorid",
                        column: x => x.provedorid,
                        principalSchema: "codex",
                        principalTable: "provedores",
                        principalColumn: "provedorid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_avisos_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "camposversao",
                schema: "codex",
                columns: table => new
                {
                    campoversaoid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: false),
                    campolayoutid = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tamanho = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    mascara = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    dominio = table.Column<string>(type: "text", nullable: true),
                    estadopreenchimento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    condicao = table.Column<string>(type: "text", nullable: true),
                    exemplo = table.Column<string>(type: "text", nullable: true),
                    notapreenchimento = table.Column<string>(type: "text", nullable: true),
                    destinoxml = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    notatransformacao = table.Column<string>(type: "text", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_camposversao", x => x.campoversaoid);
                    table.CheckConstraint("ck_codex_camposversao_estadopreenchimento", "estadopreenchimento IN ('Obrigatorio', 'Opcional', 'Condicional')");
                    table.ForeignKey(
                        name: "fk_codex_camposversao_campolayoutid",
                        column: x => x.campolayoutid,
                        principalSchema: "codex",
                        principalTable: "camposlayout",
                        principalColumn: "campolayoutid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_camposversao_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "errosversoes",
                schema: "codex",
                columns: table => new
                {
                    erroid = table.Column<int>(type: "integer", nullable: false),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_errosversoes", x => new { x.erroid, x.versaoprovedorid });
                    table.ForeignKey(
                        name: "fk_codex_errosversoes_erroid",
                        column: x => x.erroid,
                        principalSchema: "codex",
                        principalTable: "erros",
                        principalColumn: "erroid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_errosversoes_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itensbusca",
                schema: "codex",
                columns: table => new
                {
                    itembuscaid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipoitem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    itemid = table.Column<int>(type: "integer", nullable: false),
                    chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subtitulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    url = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    provedorid = table.Column<int>(type: "integer", nullable: true),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: true),
                    provedorids = table.Column<int[]>(type: "integer[]", nullable: false, defaultValueSql: "'{}'::integer[]"),
                    pesoa = table.Column<string>(type: "text", nullable: true),
                    pesob = table.Column<string>(type: "text", nullable: true),
                    pesoc = table.Column<string>(type: "text", nullable: true),
                    pesod = table.Column<string>(type: "text", nullable: true),
                    textobusca = table.Column<string>(type: "text", nullable: true, computedColumnSql: "codex.normalizar_busca(coalesce(pesoa, '') || ' ' || coalesce(pesob, '') || ' ' || coalesce(pesoc, '') || ' ' || coalesce(pesod, ''))", stored: true),
                    visivelpublico = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "publicado AND desativadoem IS NULL AND tipoitem <> 'campolayout'", stored: true),
                    documento = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesoa, ''))), 'A') || setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesob, ''))), 'B') || setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesoc, ''))), 'C') || setweight(to_tsvector('codex.codex_pt'::regconfig, codex.normalizar_busca(coalesce(pesod, ''))), 'D')", stored: true),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_itensbusca", x => x.itembuscaid);
                    table.CheckConstraint("ck_codex_itensbusca_tipoitem", "tipoitem IN ('erro', 'provedor', 'versaoprovedor', 'campolayout', 'aviso')");
                    table.ForeignKey(
                        name: "fk_codex_itensbusca_provedorid",
                        column: x => x.provedorid,
                        principalSchema: "codex",
                        principalTable: "provedores",
                        principalColumn: "provedorid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_itensbusca_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisoscampos",
                schema: "codex",
                columns: table => new
                {
                    avisoid = table.Column<int>(type: "integer", nullable: false),
                    campoversaoid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_avisoscampos", x => new { x.avisoid, x.campoversaoid });
                    table.ForeignKey(
                        name: "fk_codex_avisoscampos_avisoid",
                        column: x => x.avisoid,
                        principalSchema: "codex",
                        principalTable: "avisos",
                        principalColumn: "avisoid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_avisoscampos_campoversaoid",
                        column: x => x.campoversaoid,
                        principalSchema: "codex",
                        principalTable: "camposversao",
                        principalColumn: "campoversaoid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_codex_aliasesprovedor_provedorid",
                schema: "codex",
                table: "aliasesprovedor",
                column: "provedorid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_aliasesprovedor_provedorid_alias",
                schema: "codex",
                table: "aliasesprovedor",
                columns: new[] { "provedorid", "alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_avisos_provedorid",
                schema: "codex",
                table: "avisos",
                column: "provedorid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_avisos_severidade",
                schema: "codex",
                table: "avisos",
                column: "severidade");

            migrationBuilder.CreateIndex(
                name: "idx_codex_avisos_versaoprovedorid",
                schema: "codex",
                table: "avisos",
                column: "versaoprovedorid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_avisoscampos_campoversaoid",
                schema: "codex",
                table: "avisoscampos",
                column: "campoversaoid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_camposlayout_estado",
                schema: "codex",
                table: "camposlayout",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "idx_codex_camposlayout_versaolayoutdescontinuadoid",
                schema: "codex",
                table: "camposlayout",
                column: "versaolayoutdescontinuadoid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_camposlayout_versaolayoutid",
                schema: "codex",
                table: "camposlayout",
                column: "versaolayoutid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_camposlayout_caminhoxml",
                schema: "codex",
                table: "camposlayout",
                column: "caminhoxml",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_codex_camposlayout_slug",
                schema: "codex",
                table: "camposlayout",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_camposversao_campolayoutid",
                schema: "codex",
                table: "camposversao",
                column: "campolayoutid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_camposversao_versaoprovedorid",
                schema: "codex",
                table: "camposversao",
                column: "versaoprovedorid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_camposversao_versaoprovedorid_campolayoutid",
                schema: "codex",
                table: "camposversao",
                columns: new[] { "versaoprovedorid", "campolayoutid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_erros_codigo",
                schema: "codex",
                table: "erros",
                column: "codigo");

            migrationBuilder.CreateIndex(
                name: "idx_codex_erros_origem",
                schema: "codex",
                table: "erros",
                column: "origem");

            migrationBuilder.CreateIndex(
                name: "uq_codex_erros_slug",
                schema: "codex",
                table: "erros",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_erroscampos_campolayoutid",
                schema: "codex",
                table: "erroscampos",
                column: "campolayoutid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_errosversoes_versaoprovedorid",
                schema: "codex",
                table: "errosversoes",
                column: "versaoprovedorid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_itensbusca_documento_interno",
                schema: "codex",
                table: "itensbusca",
                column: "documento",
                filter: "desativadoem IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_codex_itensbusca_documento_publico",
                schema: "codex",
                table: "itensbusca",
                column: "documento",
                filter: "visivelpublico")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_codex_itensbusca_provedorids",
                schema: "codex",
                table: "itensbusca",
                column: "provedorids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_codex_itensbusca_textobusca_trgm",
                schema: "codex",
                table: "itensbusca",
                column: "textobusca")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_codex_itensbusca_tipoitem_provedorid",
                schema: "codex",
                table: "itensbusca",
                columns: new[] { "tipoitem", "provedorid" });

            migrationBuilder.CreateIndex(
                name: "IX_itensbusca_provedorid",
                schema: "codex",
                table: "itensbusca",
                column: "provedorid");

            migrationBuilder.CreateIndex(
                name: "IX_itensbusca_versaoprovedorid",
                schema: "codex",
                table: "itensbusca",
                column: "versaoprovedorid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_itensbusca_tipoitem_itemid",
                schema: "codex",
                table: "itensbusca",
                columns: new[] { "tipoitem", "itemid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_provedores_categoria",
                schema: "codex",
                table: "provedores",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "uq_codex_provedores_identificadorinvoicy",
                schema: "codex",
                table: "provedores",
                column: "identificadorinvoicy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_codex_provedores_slug",
                schema: "codex",
                table: "provedores",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_redirecionamentos_tipoitem",
                schema: "codex",
                table: "redirecionamentos",
                column: "tipoitem");

            migrationBuilder.CreateIndex(
                name: "uq_codex_redirecionamentos_origem",
                schema: "codex",
                table: "redirecionamentos",
                column: "origem",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_codex_termosbusca_termo",
                schema: "codex",
                table: "termosbusca",
                column: "termo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_termosbusca_termo_trgm",
                schema: "codex",
                table: "termosbusca",
                column: "termo")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_codex_variantesmensagem_erroid",
                schema: "codex",
                table: "variantesmensagem",
                column: "erroid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_versoeslayout_estado_vigente",
                schema: "codex",
                table: "versoeslayout",
                column: "estado",
                unique: true,
                filter: "estado = 'Vigente' AND desativadoem IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_codex_versoeslayout_ordem",
                schema: "codex",
                table: "versoeslayout",
                column: "ordem",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_codex_versoeslayout_slug",
                schema: "codex",
                table: "versoeslayout",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_versoesprovedor_provedorid",
                schema: "codex",
                table: "versoesprovedor",
                column: "provedorid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_versoesprovedor_versaolayoutid",
                schema: "codex",
                table: "versoesprovedor",
                column: "versaolayoutid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_versoesprovedor_provedorid_slug",
                schema: "codex",
                table: "versoesprovedor",
                columns: new[] { "provedorid", "slug" },
                unique: true);

            // Triggers BEFORE UPDATE — EF Core não tem conceito de trigger, SQL bruto para toda
            // tabela auditada (modelo-dados.md §3, uma por tabela). Precisam vir depois de todos
            // os CREATE TABLE acima (a tabela precisa existir antes do CREATE TRIGGER).
            migrationBuilder.Sql(CriarTrigger("versoeslayout"));
            migrationBuilder.Sql(CriarTrigger("camposlayout"));
            migrationBuilder.Sql(CriarTrigger("provedores"));
            migrationBuilder.Sql(CriarTrigger("aliasesprovedor"));
            migrationBuilder.Sql(CriarTrigger("versoesprovedor"));
            migrationBuilder.Sql(CriarTrigger("camposversao"));
            migrationBuilder.Sql(CriarTrigger("avisos"));
            migrationBuilder.Sql(CriarTrigger("avisoscampos"));
            migrationBuilder.Sql(CriarTrigger("erros"));
            migrationBuilder.Sql(CriarTrigger("variantesmensagem"));
            migrationBuilder.Sql(CriarTrigger("errosversoes"));
            migrationBuilder.Sql(CriarTrigger("erroscampos"));
            migrationBuilder.Sql(CriarTrigger("itensbusca"));
            migrationBuilder.Sql(CriarTrigger("termosbusca"));
            migrationBuilder.Sql(CriarTrigger("redirecionamentos"));
        }

        private static string CriarTrigger(string tabela) =>
            $"""
            CREATE TRIGGER trg_codex_{tabela}_atualizadoem
                BEFORE UPDATE ON codex.{tabela}
                FOR EACH ROW EXECUTE FUNCTION codex.set_atualizadoem();
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aliasesprovedor",
                schema: "codex");


            migrationBuilder.DropTable(
                name: "avisoscampos",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "erroscampos",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "errosversoes",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "itensbusca",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "redirecionamentos",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "termosbusca",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "variantesmensagem",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "avisos",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "camposversao",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "erros",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "camposlayout",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "versoesprovedor",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "provedores",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "versoeslayout",
                schema: "codex");

            // Triggers já saem junto com o DROP TABLE de cada tabela — só a função compartilhada
            // precisa de DROP explícito, depois que nenhum trigger mais a referencia.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS codex.set_atualizadoem();");
        }
    }
}
