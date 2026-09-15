namespace Codex.Tests.Integration;

/// <summary>Compartilha um único container Postgres entre as classes de teste de integração desta unidade — subir um container por classe seria caro e desnecessário.</summary>
[CollectionDefinition(Nome)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Nome = "Postgres";
}
