using DynamicDataCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DynamicDataCore.PostgreSql.Tests;

public sealed class Item
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed record ItemRow(int Id, string Name);

public sealed class PgTestDbContext(DbContextOptions<PgTestDbContext> options) : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("item", "t");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name");
        });
}

/// <summary>
/// One disposable PostgreSQL container per test class collection. Requires a running Docker daemon.
/// Skip with: dotnet test --filter "Category!=Integration".
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public PgTestDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PgTestDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await SeedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync().AsTask();

    // Test-only schema bootstrap; test projects are deliberately outside the raw-SQL analyzer.
    private async Task SeedAsync()
    {
        const string sql = """
            create schema t;
            create table t.item (id serial primary key, name text not null);

            create function t.fn_add(a int, b int) returns int language sql as 'select a + b';

            create function t.fn_items() returns table (id int, name text) language sql
                as 'select id, name from t.item order by id';

            create function t.fn_json(p jsonb) returns jsonb language sql
                as $$ select jsonb_build_object('echo', p, 'count', (select count(*) from t.item)) $$;

            create function t.fn_null_json() returns jsonb language sql as 'select null::jsonb';

            create function t.fn_fail(p_code text) returns void language plpgsql as $$
            begin
                raise exception 'boom' using errcode = 'P0001', hint = p_code;
            end $$;

            create function t.fn_insert_item(p_name text) returns void language sql
                as 'insert into t.item(name) values (p_name)';

            create procedure t.sp_insert_item(p_name text) language sql
                as 'insert into t.item(name) values (p_name)';

            create function t.fn_when(p_at timestamp) returns text language sql
                as 'select to_char(p_at, ''YYYY-MM-DD HH24:MI'')';
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
