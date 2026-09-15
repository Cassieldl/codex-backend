using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarConteudoEditorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As 4 tabelas abaixo saem de ExcludeFromMigrations() (decisoes.md U-1). Gotcha
            // pago nesta migration: alternar a exclusão para false NÃO faz o `dotnet ef
            // migrations add` gerar CreateTable sozinho — o snapshot anterior já registrava as
            // colunas destas entidades (só marcadas "não migradas"), então o differ não vê
            // diferença estrutural, só a mudança de anotação. CreateTable/CreateIndex escritos
            // à mão a seguir, replicando byte a byte o DDL de modelo-dados.md §2.

            migrationBuilder.CreateTable(
                name: "artigos",
                schema: "codex",
                columns: table => new
                {
                    artigoid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corpo = table.Column<string>(type: "text", nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    verificadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_artigos", x => x.artigoid);
                    table.CheckConstraint("ck_codex_artigos_tipo", "tipo IN ('Conceito', 'Procedimento', 'Mudanca')");
                });

            migrationBuilder.CreateTable(
                name: "comunicados",
                schema: "codex",
                columns: table => new
                {
                    comunicadoid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corpo = table.Column<string>(type: "text", nullable: false),
                    severidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provedorid = table.Column<int>(type: "integer", nullable: true),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: true),
                    expiraem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publicado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_comunicados", x => x.comunicadoid);
                    table.CheckConstraint("ck_codex_comunicados_severidade", "severidade IN ('Critico', 'Alerta', 'Informativo')");
                    table.CheckConstraint("ck_codex_comunicados_abrangencia", "abrangencia IN ('Global', 'Provedor', 'ProvedorVersao')");
                    table.CheckConstraint(
                        "ck_codex_comunicados_abrangencia_consistente",
                        "(abrangencia = 'Global' AND provedorid IS NULL AND versaoprovedorid IS NULL) OR " +
                        "(abrangencia = 'Provedor' AND provedorid IS NOT NULL AND versaoprovedorid IS NULL) OR " +
                        "(abrangencia = 'ProvedorVersao' AND provedorid IS NOT NULL AND versaoprovedorid IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_codex_comunicados_provedorid",
                        column: x => x.provedorid,
                        principalSchema: "codex",
                        principalTable: "provedores",
                        principalColumn: "provedorid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_comunicados_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artigoscamposlayout",
                schema: "codex",
                columns: table => new
                {
                    artigoid = table.Column<int>(type: "integer", nullable: false),
                    campolayoutid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_artigoscamposlayout", x => new { x.artigoid, x.campolayoutid });
                    table.ForeignKey(
                        name: "fk_codex_artigoscamposlayout_artigoid",
                        column: x => x.artigoid,
                        principalSchema: "codex",
                        principalTable: "artigos",
                        principalColumn: "artigoid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_artigoscamposlayout_campolayoutid",
                        column: x => x.campolayoutid,
                        principalSchema: "codex",
                        principalTable: "camposlayout",
                        principalColumn: "campolayoutid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artigoserros",
                schema: "codex",
                columns: table => new
                {
                    artigoid = table.Column<int>(type: "integer", nullable: false),
                    erroid = table.Column<int>(type: "integer", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_artigoserros", x => new { x.artigoid, x.erroid });
                    table.ForeignKey(
                        name: "fk_codex_artigoserros_artigoid",
                        column: x => x.artigoid,
                        principalSchema: "codex",
                        principalTable: "artigos",
                        principalColumn: "artigoid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_codex_artigoserros_erroid",
                        column: x => x.erroid,
                        principalSchema: "codex",
                        principalTable: "erros",
                        principalColumn: "erroid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_codex_artigos_slug",
                schema: "codex",
                table: "artigos",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_codex_artigos_tipo",
                schema: "codex",
                table: "artigos",
                column: "tipo");

            migrationBuilder.CreateIndex(
                name: "idx_codex_artigos_publicado",
                schema: "codex",
                table: "artigos",
                column: "publicado");

            migrationBuilder.CreateIndex(
                name: "idx_codex_comunicados_provedorid",
                schema: "codex",
                table: "comunicados",
                column: "provedorid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_comunicados_versaoprovedorid",
                schema: "codex",
                table: "comunicados",
                column: "versaoprovedorid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_comunicados_expiraem",
                schema: "codex",
                table: "comunicados",
                column: "expiraem");

            // Sustenta ComunicadoRepositorio.QueryVigentes nas duas direções — leitura
            // (expiraem > referencia) e job de expiração (expiraem <= referencia). O predicado
            // do índice é IDÊNTICO às duas condições estáveis da consulta (modelo-dados.md §2.1/§7).
            migrationBuilder.CreateIndex(
                name: "idx_codex_comunicados_vigentes",
                schema: "codex",
                table: "comunicados",
                column: "expiraem",
                filter: "publicado AND desativadoem IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_codex_artigoscamposlayout_campolayoutid",
                schema: "codex",
                table: "artigoscamposlayout",
                column: "campolayoutid");

            migrationBuilder.CreateIndex(
                name: "idx_codex_artigoserros_erroid",
                schema: "codex",
                table: "artigoserros",
                column: "erroid");

            migrationBuilder.DropCheckConstraint(
                name: "ck_codex_itensbusca_tipoitem",
                schema: "codex",
                table: "itensbusca");

            migrationBuilder.AddCheckConstraint(
                name: "ck_codex_itensbusca_tipoitem",
                schema: "codex",
                table: "itensbusca",
                sql: "tipoitem IN ('erro', 'provedor', 'versaoprovedor', 'campolayout', 'aviso', 'artigo')");

            // Triggers BEFORE UPDATE — mesmo padrão de InitialCreate (EF Core não tem conceito de
            // trigger). Gotcha (modelo-dados.md §8 item 5): esquecer um destes não quebra nada na
            // hora — só faz atualizadoem parar de avançar em UPDATE, silenciosamente.
            migrationBuilder.Sql(CriarTrigger("artigos"));
            migrationBuilder.Sql(CriarTrigger("comunicados"));
            migrationBuilder.Sql(CriarTrigger("artigoscamposlayout"));
            migrationBuilder.Sql(CriarTrigger("artigoserros"));
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
            migrationBuilder.DropCheckConstraint(
                name: "ck_codex_itensbusca_tipoitem",
                schema: "codex",
                table: "itensbusca");

            migrationBuilder.AddCheckConstraint(
                name: "ck_codex_itensbusca_tipoitem",
                schema: "codex",
                table: "itensbusca",
                sql: "tipoitem IN ('erro', 'provedor', 'versaoprovedor', 'campolayout', 'aviso')");

            // Triggers saem junto do DROP TABLE de cada tabela (mesma disciplina de InitialCreate.Down).
            // Ordem inversa de FK: filhas (artigoscamposlayout/artigoserros) antes do pai (artigos).
            migrationBuilder.DropTable(
                name: "artigoscamposlayout",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "artigoserros",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "comunicados",
                schema: "codex");

            migrationBuilder.DropTable(
                name: "artigos",
                schema: "codex");
        }
    }
}
