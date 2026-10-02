using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace DynamicDataCore.Tests.Procedures;

/// <summary>
/// Starts one disposable SQL Server 2022 container per test run. The SA password is generated at random in
/// memory for each run; it is never written to disk or logged. Requires a running Docker daemon.
/// Skip these tests with: dotnet test --filter "Category!=Integration".
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public SqlServerFixture()
    {
        // Meets SQL Server's complexity policy (upper, lower, digit, symbol) with random material.
        var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword(password)
            .Build();
    }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await SeedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync().AsTask();

    // Test-only schema bootstrap; the test project is deliberately outside the raw-SQL analyzer.
    private async Task SeedAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var batch in SchemaBatches)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static readonly string[] SchemaBatches =
    [
        "CREATE SCHEMA ddc",
        "CREATE TYPE ddc.IdList AS TABLE (Id int NOT NULL, Label nvarchar(50) NULL)",
        "CREATE TABLE ddc.Items (Id int NOT NULL PRIMARY KEY, Label nvarchar(50) NULL, Tag nvarchar(20) NULL)",
        """
        CREATE PROCEDURE ddc.usp_Demo
            @Items ddc.IdList READONLY,
            @Prefix nvarchar(20),
            @Count int OUTPUT,
            @Message nvarchar(100) OUTPUT
        AS
        BEGIN
            INSERT ddc.Items (Id, Label, Tag) SELECT Id, Label, @Prefix FROM @Items;
            SET @Count = (SELECT COUNT(*) FROM @Items);
            SET @Message = CONCAT(N'inserted ', @Count, N' with ', @Prefix);
            SELECT Id, Label, Tag FROM ddc.Items WHERE Tag = @Prefix ORDER BY Id;
            SELECT COUNT(*) AS Total, MAX(Id) AS MaxId FROM ddc.Items WHERE Tag = @Prefix;
            RETURN 42;
        END
        """,
        """
        CREATE PROCEDURE ddc.usp_Echo @Value int
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT @Value AS Value;
        END
        """,
        """
        CREATE PROCEDURE ddc.usp_Two
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT 1 AS Id UNION ALL SELECT 2;
        END
        """,
        "CREATE VIEW ddc.v_ItemSummary AS SELECT Id, Label, Tag FROM ddc.Items"
    ];
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
