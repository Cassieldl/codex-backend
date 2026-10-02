using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codex.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SepararNormalizacaoDeConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Corpo escrito à mão: a migration não muda o modelo, só acrescenta uma função de
            // banco — o differ do EF gera o arquivo vazio nesse caso (mesmo gotcha já registrado
            // em `rules/database.md`).
            //
            // `codex.normalizar_busca` produz TRÊS formas concatenadas — bruta, quebrada e
            // compactada — e servia tanto à indexação quanto à consulta. As duas semânticas são
            // opostas, e é daí que vinha o defeito:
            //
            //   No DOCUMENTO as três formas são ALTERNATIVAS: o tsvector fica com todas, e
            //   qualquer uma que a pessoa digite casa.
            //
            //   Na CONSULTA elas viram EXIGÊNCIAS, porque `websearch_to_tsquery` une os lexemas
            //   com AND. E a forma compactada de um documento longo é o texto inteiro colado num
            //   único lexema ("e1023inscricaomunicipalinvalida..."), que nunca é igual à forma
            //   compactada do termo ("e1023"). O AND passava a exigir um lexema impossível.
            //
            // Medido antes da correção: `prestador` casava forte, mas `E-1023`,
            // `inscricao municipal` e a variante de mensagem IDÊNTICA à cadastrada caíam todos
            // para o passo parcial — ou seja, os dois caminhos de entrada que o escopo funcional
            // declara críticos (código do erro e trecho de mensagem) quase nunca casavam forte,
            // porque ambos têm mais de um token.
            //
            // A consulta passa a usar só as duas primeiras formas. A indexação não muda:
            // `normalizar_busca` fica intacta e a coluna GENERATED de `codex.itensbusca` não
            // precisa ser reconstruída — que é o motivo de esta ser uma função NOVA em vez de
            // uma alteração da existente.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION codex.normalizar_consulta(entrada text)
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
                $$;
                """);

            migrationBuilder.Sql("""
                COMMENT ON FUNCTION codex.normalizar_consulta(text) IS
                    'Normalizacao do TERMO DE CONSULTA (Infrastructure/Busca). Produz as duas '
                    'primeiras formas de codex.normalizar_busca: bruta e quebrada (separador '
                    'colado -> espaco). NAO inclui a forma compactada, que num documento longo '
                    'vira um unico lexema com o texto inteiro e, exigida por AND na consulta, '
                    'tornava o casamento forte inalcancavel para qualquer termo com mais de um '
                    'token. Indexacao continua em codex.normalizar_busca; esta funcao nao toca '
                    'em codex.itensbusca.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS codex.normalizar_consulta(text);");
        }
    }
}
