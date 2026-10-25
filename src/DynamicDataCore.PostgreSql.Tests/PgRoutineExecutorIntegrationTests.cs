using DynamicDataCore.Abstractions.Procedures;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.PostgreSql.Tests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PgRoutineExecutorIntegrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    // Each test starts from an empty table.
    public async Task InitializeAsync()
    {
        await using var ctx = fixture.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync("truncate t.item restart identity");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProcedureName Name(string value) => ProcedureName.Create(value);

    [Fact]
    public async Task Scalar_function_returns_value_with_named_arguments()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        // Order does not matter: arguments are passed by name.
        var result = await executor.QuerySingleOrDefaultAsync<int>(
            Name("t.fn_add"), PgParameters.Create().Int("b", 3).Int("a", 2));

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task Set_returning_function_maps_rows_to_records()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);
        await executor.ExecuteFunctionAsync(Name("t.fn_insert_item"), PgParameters.Create().Text("p_name", "camisa"));
        await executor.ExecuteFunctionAsync(Name("t.fn_insert_item"), PgParameters.Create().Text("p_name", "pantalon"));

        var rows = await executor.QueryAsync<ItemRow>(Name("t.fn_items"));

        Assert.Equal([new ItemRow(1, "camisa"), new ItemRow(2, "pantalon")], rows);
    }

    private sealed class Payload
    {
        public string Sku { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    private sealed class EchoResult
    {
        public Payload Echo { get; set; } = new();

        public int Count { get; set; }
    }

    [Fact]
    public async Task Json_round_trips_through_jsonb()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        var result = await executor.JsonAsync<EchoResult>(
            Name("t.fn_json"), PgParameters.Create().Json("p", new { sku = "CAM-001", price = 450.50m }));

        Assert.NotNull(result);
        Assert.Equal("CAM-001", result!.Echo.Sku);
        Assert.Equal(450.50m, result.Echo.Price);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Json_returns_default_for_sql_null()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        Assert.Null(await executor.JsonAsync<EchoResult>(Name("t.fn_null_json")));
    }

    [Fact]
    public async Task Business_rule_error_surfaces_code_from_hint()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        var ex = await Assert.ThrowsAsync<PgRoutineException>(() =>
            executor.ExecuteFunctionAsync(Name("t.fn_fail"), PgParameters.Create().Text("p_code", "insufficient_stock")));

        Assert.True(ex.IsBusinessRule);
        Assert.Equal("P0001", ex.SqlState);
        Assert.Equal("insufficient_stock", ex.Code);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Procedure_call_inserts_row()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        await executor.CallAsync(Name("t.sp_insert_item"), PgParameters.Create().Text("p_name", "corbata"));

        Assert.Equal(1, await ctx.Set<Item>().CountAsync());
    }

    [Fact]
    public async Task Routine_calls_roll_back_with_the_ef_transaction()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            await executor.ExecuteFunctionAsync(Name("t.fn_insert_item"), PgParameters.Create().Text("p_name", "rollback-me"));
            await executor.CallAsync(Name("t.sp_insert_item"), PgParameters.Create().Text("p_name", "rollback-me-too"));
            // Visible inside the same transaction...
            Assert.Equal(2, await ctx.Set<Item>().CountAsync());
            await tx.RollbackAsync();
        }

        // ...and gone after rollback.
        await using var verify = fixture.CreateContext();
        Assert.Equal(0, await verify.Set<Item>().CountAsync());
    }

    [Fact]
    public async Task Routine_calls_and_ef_changes_commit_together()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            ctx.Set<Item>().Add(new Item { Name = "via-ef" });
            await ctx.SaveChangesAsync();
            await executor.ExecuteFunctionAsync(Name("t.fn_insert_item"), PgParameters.Create().Text("p_name", "via-routine"));
            await tx.CommitAsync();
        }

        await using var verify = fixture.CreateContext();
        var names = await verify.Set<Item>().OrderBy(i => i.Id).Select(i => i.Name).ToListAsync();
        Assert.Equal(["via-ef", "via-routine"], names);
    }

    [Fact]
    public async Task Failing_routine_inside_transaction_leaves_nothing_after_rollback()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            await executor.ExecuteFunctionAsync(Name("t.fn_insert_item"), PgParameters.Create().Text("p_name", "before-failure"));
            await Assert.ThrowsAsync<PgRoutineException>(() =>
                executor.ExecuteFunctionAsync(Name("t.fn_fail"), PgParameters.Create().Text("p_code", "x")));
            await tx.RollbackAsync();
        }

        await using var verify = fixture.CreateContext();
        Assert.Equal(0, await verify.Set<Item>().CountAsync());
    }

    [Fact]
    public async Task Timestamp_parameter_accepts_utc_kind_values()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        // Kind=Utc would make Npgsql reject a 'timestamp without time zone' parameter.
        var utc = new DateTime(2026, 10, 24, 18, 30, 0, DateTimeKind.Utc);
        var text = await executor.QuerySingleOrDefaultAsync<string>(
            Name("t.fn_when"), PgParameters.Create().Timestamp("p_at", utc));

        Assert.Equal("2026-10-24 18:30", text);
    }

    [Fact]
    public async Task Connection_is_released_after_each_call()
    {
        await using var ctx = fixture.CreateContext();
        var executor = new PgRoutineExecutor(ctx);

        await executor.QuerySingleOrDefaultAsync<int>(Name("t.fn_add"), PgParameters.Create().Int("a", 1).Int("b", 1));

        Assert.Equal(System.Data.ConnectionState.Closed, ctx.Database.GetDbConnection().State);
    }
}
