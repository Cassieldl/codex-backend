using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarExemplosERedirectArtigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // codex.exemplos sai de ExcludeFromMigrations() (decisoes.md U-1/M-a). Mesmo gotcha já
            // pago em AdicionarConteudoEditorial: alternar a anotação não faz o `dotnet ef
            // migrations add` gerar CreateTable sozinho — o snapshot anterior já registrava as
            // colunas de Exemplo como existentes (só marcadas "não migradas"), então o differ só
            // via a mudança de anotação + a FK nova. CreateTable/CreateIndex/trigger abaixo são
            // escritos à mão, replicando byte a byte o DDL de modelo-dados.md §1.6.
            migrationBuilder.CreateTable(
                name: "exemplos",
                schema: "codex",
                columns: table => new
                {
                    exemploid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    versaoprovedorid = table.Column<int>(type: "integer", nullable: false),
                    slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    atualizadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    desativadoem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codex_exemplos", x => x.exemploid);
                    // Guarda de catástrofe (modelo-dados.md §1.5) — 256 KiB em BYTES
                    // (octet_length), não os 64 KiB em CARACTERES do FluentValidation. Números
                    // diferentes de propósito: não igualar.
                    table.CheckConstraint("ck_codex_exemplos_xml_tamanho", "octet_length(xml) <= 262144");
                    table.ForeignKey(
                        name: "fk_codex_exemplos_versaoprovedorid",
                        column: x => x.versaoprovedorid,
                        principalSchema: "codex",
                        principalTable: "versoesprovedor",
                        principalColumn: "versaoprovedorid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_codex_exemplos_versaoprovedorid",
                schema: "codex",
                table: "exemplos",
                column: "versaoprovedorid");

            migrationBuilder.CreateIndex(
                name: "uq_codex_exemplos_versaoprovedorid_nome",
                schema: "codex",
                table: "exemplos",
                columns: new[] { "versaoprovedorid", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_codex_exemplos_versaoprovedorid_slug",
                schema: "codex",
                table: "exemplos",
                columns: new[] { "versaoprovedorid", "slug" },
                unique: true);

            migrationBuilder.Sql(CriarTrigger("exemplos"));

            // M-b (decisoes.md U-1) — na MESMA migration. 'artigo' faltava no CHECK desde a 002;
            // sem isto, Redirecionamento.Criar(..., TipoItemIndexado.artigo) passa nas validações
            // de domínio e só falha no INSERT com 23514 check_violation.
            migrationBuilder.DropCheckConstraint(
                name: "ck_codex_redirecionamentos_tipoitem",
                schema: "codex",
                table: "redirecionamentos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_codex_redirecionamentos_tipoitem",
                schema: "codex",
                table: "redirecionamentos",
                sql: "tipoitem IN ('provedor', 'versaoprovedor', 'erro', 'campolayout', 'artigo')");
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
            // Ordem inversa do Up() — sem dependência real entre as duas operações (tabelas
            // diferentes), mas é a convenção já usada nas duas migrations anteriores.
            migrationBuilder.DropCheckConstraint(
                name: "ck_codex_redirecionamentos_tipoitem",
                schema: "codex",
                table: "redirecionamentos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_codex_redirecionamentos_tipoitem",
                schema: "codex",
                table: "redirecionamentos",
                sql: "tipoitem IN ('provedor', 'versaoprovedor', 'erro', 'campolayout')");

            // Trigger sai junto do DROP TABLE (mesma disciplina das duas migrations anteriores).
            // ck_codex_exemplos_xml_tamanho não tem linha própria aqui: nasceu DENTRO do
            // CreateTable, então o DropTable já remove o CHECK junto com a tabela inteira.
            migrationBuilder.DropTable(
                name: "exemplos",
                schema: "codex");
        }
    }
}
