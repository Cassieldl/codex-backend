using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarLacunasBusca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lacunasbusca",
                schema: "codex",
                columns: table => new
                {
                    lacunabuscaid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    termo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    termonormalizado = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    parceiro = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    numerochamado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    registradoporid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registradopornome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    resolvidaem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolvidoporid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resolvidopornome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_lacunasbusca", x => x.lacunabuscaid);
                    table.CheckConstraint("ck_codex_lacunasbusca_estado", "estado IN ('Aberta', 'Resolvida', 'Descartada')");
                    table.CheckConstraint("ck_codex_lacunasbusca_resolucao_consistente", "(estado = 'Aberta' AND resolvidaem IS NULL AND resolvidoporid IS NULL) OR (estado <> 'Aberta' AND resolvidaem IS NOT NULL AND resolvidoporid IS NOT NULL)");
                    table.CheckConstraint("ck_codex_lacunasbusca_termo_minimo", "length(btrim(termo)) >= 2");
                });

            migrationBuilder.CreateIndex(
                name: "idx_codex_lacunasbusca_estado_criadoem",
                schema: "codex",
                table: "lacunasbusca",
                columns: new[] { "estado", "criadoem" });

            migrationBuilder.CreateIndex(
                name: "idx_codex_lacunasbusca_termonormalizado_estado",
                schema: "codex",
                table: "lacunasbusca",
                columns: new[] { "termonormalizado", "estado" });

            // O EF não gera trigger: sem ele, atualizadoem simplesmente para de ser carimbada.
            migrationBuilder.Sql(CriarTrigger("lacunasbusca"));
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
            // O trigger sai junto com a tabela.
            migrationBuilder.DropTable(
                name: "lacunasbusca",
                schema: "codex");
        }
    }
}
