using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTipoArtigoConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_codex_artigos_tipo",
                schema: "codex",
                table: "artigos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_codex_artigos_tipo",
                schema: "codex",
                table: "artigos",
                sql: "tipo IN ('Conceito', 'Procedimento', 'Mudanca', 'Configuracao')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_codex_artigos_tipo",
                schema: "codex",
                table: "artigos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_codex_artigos_tipo",
                schema: "codex",
                table: "artigos",
                sql: "tipo IN ('Conceito', 'Procedimento', 'Mudanca')");
        }
    }
}
