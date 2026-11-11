using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNaturezasOperacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "naturezasoperacao",
                schema: "codex",
                columns: table => new
                {
                    naturezasoperacaoid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: false),
                    codigoinvoicy = table.Column<short>(type: "smallint", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valorprovedor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aconfirmar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ordem = table.Column<short>(type: "smallint", nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_naturezasoperacao", x => x.naturezasoperacaoid);
                    table.CheckConstraint("ck_codex_naturezasoperacao_codigoinvoicy", "codigoinvoicy BETWEEN 0 AND 999");
                    table.ForeignKey(
                        name: "fk_codex_naturezasoperacao_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_codex_naturezasoperacao_versaoprovedorid_codigoinvoicy",
                schema: "codex",
                table: "naturezasoperacao",
                columns: new[] { "versaoprovedorid", "codigoinvoicy" },
                unique: true);

            // EF não gera trigger: carimbo de atualizadoem igual ao das outras tabelas auditadas.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_codex_naturezasoperacao_atualizadoem
                    BEFORE UPDATE ON codex.naturezasoperacao
                    FOR EACH ROW EXECUTE FUNCTION codex.set_atualizadoem();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "naturezasoperacao",
                schema: "codex");
        }
    }
}
