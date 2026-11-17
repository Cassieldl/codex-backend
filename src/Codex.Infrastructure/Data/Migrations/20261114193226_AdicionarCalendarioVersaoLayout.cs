using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCalendarioVersaoLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "homologacaodesde",
                schema: "codex",
                table: "versoeslayout",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "liberadanoinvoicyem",
                schema: "codex",
                table: "versoeslayout",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normaorigem",
                schema: "codex",
                table: "versoeslayout",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "obrigatoriaem",
                schema: "codex",
                table: "versoeslayout",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "homologacaodesde",
                schema: "codex",
                table: "versoeslayout");

            migrationBuilder.DropColumn(
                name: "liberadanoinvoicyem",
                schema: "codex",
                table: "versoeslayout");

            migrationBuilder.DropColumn(
                name: "normaorigem",
                schema: "codex",
                table: "versoeslayout");

            migrationBuilder.DropColumn(
                name: "obrigatoriaem",
                schema: "codex",
                table: "versoeslayout");
        }
    }
}
