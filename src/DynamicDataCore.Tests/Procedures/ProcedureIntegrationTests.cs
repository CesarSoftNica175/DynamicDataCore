using System.Data;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Abstractions.Procedures;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Infrastructure.Extensions;
using DynamicDataCore.Infrastructure.Implementation;
using DynamicDataCore.Infrastructure.Procedures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DynamicDataCore.Tests.Procedures;

public sealed class ItemRow
{
    public int Id { get; set; }
    public string? Label { get; set; }
    public string? Tag { get; set; }
}

public sealed record SummaryRow(int Total, int MaxId);

public sealed class ItemView
{
    public int Id { get; set; }
    public string? Label { get; set; }
    public string? Tag { get; set; }
}

public sealed class ViewDbContext : DbContext, IAppDbContext
{
    public ViewDbContext(DbContextOptions<ViewDbContext> options) : base(options) { }

    DatabaseFacade IAppDbContext.Database => Database;
    DbSet<T> IAppDbContext.Set<T>() => Set<T>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureReadOnlyView<ItemView>("v_ItemSummary", "ddc");
}

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class ProcedureIntegrationTests(SqlServerFixture sql)
{
    private SqlProcedureExecutor Executor => new(sql.ConnectionString);

    private static TableValuedRows Rows(params (int Id, string? Label)[] items)
        => new(
            [new TableValueColumn("Id", SqlDbType.Int), new TableValueColumn("Label", SqlDbType.NVarChar, 50)],
            items.Select(i => new object?[] { i.Id, i.Label }));

    private static ProcedureParameters DemoParameters(string prefix, TableValuedRows rows)
        => ProcedureParameters.Create()
            .Structured("@Items", rows, TableTypeName.Create("ddc.IdList"))
            .Input("@Prefix", SqlDbType.NVarChar, prefix, size: 20)
            .Output("@Count", SqlDbType.Int)
            .Output("@Message", SqlDbType.NVarChar, size: 100)
            .ReturnValue();

    [Fact]
    public async Task Execute_ReturnsRowsAffected_ReturnValue_AndOutputParameters_WithRowsTvp()
    {
        var result = await Executor.ExecuteAsync(
            ProcedureName.Create("ddc.usp_Demo"),
            DemoParameters("exec", Rows((101, "a"), (102, "b"), (103, null))));

        Assert.Equal(3, result.RowsAffected);
        Assert.Equal(42, result.ReturnValue);
        Assert.Equal(3, result.GetOutput<int>("@Count"));
        Assert.Equal("inserted 3 with exec", result.GetOutput<string>("Message"));
    }

    [Fact]
    public async Task Execute_AcceptsDataTableTvp()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Label", typeof(string));
        table.Rows.Add(201, "x");
        table.Rows.Add(202, "y");

        var parameters = ProcedureParameters.Create()
            .Structured("@Items", table, TableTypeName.Create("ddc.IdList"))
            .Input("@Prefix", SqlDbType.NVarChar, "dt", size: 20)
            .Output("@Count", SqlDbType.Int)
            .Output("@Message", SqlDbType.NVarChar, size: 100)
            .ReturnValue();

        var result = await Executor.ExecuteAsync(ProcedureName.Create("ddc.usp_Demo"), parameters);

        Assert.Equal(2, result.GetOutput<int>("@Count"));
        Assert.Equal(42, result.ReturnValue);
    }

    [Fact]
    public async Task Execute_EmptyTvp_IsTreatedAsEmptyTable()
    {
        var result = await Executor.ExecuteAsync(ProcedureName.Create("ddc.usp_Demo"), DemoParameters("empty", Rows()));

        Assert.Equal(0, result.GetOutput<int>("@Count"));
        Assert.Equal(0, result.RowsAffected);
    }

    [Fact]
    public async Task QueryMultiple_ReadsBothResultSetsInOrder()
    {
        await using var multi = await Executor.QueryMultipleAsync(
            ProcedureName.Create("ddc.usp_Demo"),
            DemoParameters("multi", Rows((301, "m1"), (302, "m2"))));

        var items = await multi.ReadAsync<ItemRow>();
        var summary = await multi.ReadSingleOrDefaultAsync<SummaryRow>();

        Assert.Equal([301, 302], items.Select(i => i.Id));
        Assert.Equal("m1", items[0].Label);
        Assert.Equal("multi", items[0].Tag);
        Assert.Equal(new SummaryRow(2, 302), summary);

        await Assert.ThrowsAsync<InvalidOperationException>(() => multi.ReadAsync<ItemRow>());
    }

    [Fact]
    public async Task Query_MapsFirstResultSet_AndScalars()
    {
        var items = await Executor.QueryAsync<ItemRow>(
            ProcedureName.Create("ddc.usp_Demo"),
            DemoParameters("query", Rows((401, "q1"), (402, "q2"), (403, "q3"))));
        Assert.Equal(3, items.Count);

        var ids = await Executor.QueryAsync<int>(ProcedureName.Create("ddc.usp_Two"));
        Assert.Equal([1, 2], ids);
    }

    [Fact]
    public async Task QuerySingleOrDefault_ReturnsRow_Default_AndThrowsOnMany()
    {
        var one = await Executor.QuerySingleOrDefaultAsync<int>(
            ProcedureName.Create("ddc.usp_Echo"),
            ProcedureParameters.Create().Input("@Value", SqlDbType.Int, 7));
        Assert.Equal(7, one);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Executor.QuerySingleOrDefaultAsync<int>(ProcedureName.Create("ddc.usp_Two")));
    }

    [Fact]
    public async Task Parameters_AreNeverConcatenated_InjectionTextIsJustData()
    {
        var evil = "x'; DROP TABLE ddc.Items; --";
        var result = await Executor.ExecuteAsync(
            ProcedureName.Create("ddc.usp_Demo"),
            DemoParameters(evil.Substring(0, 20), Rows((501, evil[..20]))));

        Assert.Equal(1, result.GetOutput<int>("@Count"));
        // table still exists
        var all = await new ReadRepositoryImpl<ItemView>(NewViewContext()).CountAsync();
        Assert.True(all > 0);
    }

    [Fact]
    public async Task UnknownProcedure_Fails_WithSqlException()
        => await Assert.ThrowsAsync<SqlException>(() => Executor.ExecuteAsync(ProcedureName.Create("ddc.usp_DoesNotExist")));

    [Fact]
    public async Task CancelledToken_IsHonoured()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Executor.ExecuteAsync(ProcedureName.Create("ddc.usp_Two"), null, cts.Token));
    }

    // ── Keyless view through IReadRepository<T> ──────────────────────────────

    private ViewDbContext NewViewContext()
        => new(new DbContextOptionsBuilder<ViewDbContext>().UseSqlServer(sql.ConnectionString).Options);

    [Fact]
    public async Task ReadRepository_QueriesKeylessView_WithFilterOrderAndPaging()
    {
        await Executor.ExecuteAsync(ProcedureName.Create("ddc.usp_Demo"),
            DemoParameters("view", Rows((601, "v1"), (602, "v2"), (603, "v3"), (604, "v4"), (605, "v5"))));

        await using var ctx = NewViewContext();
        Assert.Null(ctx.Model.FindEntityType(typeof(ItemView))!.FindPrimaryKey());
        var repo = new ReadRepositoryImpl<ItemView>(ctx);

        var filtered = await repo.ListAsync(v => v.Tag == "view", q => q.OrderByDescending(v => v.Id));
        Assert.Equal([605, 604, 603, 602, 601], filtered.Select(v => v.Id));
        Assert.Empty(ctx.ChangeTracker.Entries());               // AsNoTracking

        Assert.Equal(5, await repo.CountAsync(v => v.Tag == "view"));
        Assert.True(await repo.AnyAsync(v => v.Id == 603));
        Assert.Equal("v1", (await repo.FirstOrDefaultAsync(v => v.Tag == "view", q => q.OrderBy(v => v.Id)))!.Label);

        var page2 = await repo.PageAsync(new OffsetPageRequest(2, 2), q => q.OrderBy(v => v.Id), v => v.Tag == "view");
        Assert.Equal([603, 604], page2.Items.Select(v => v.Id));
        Assert.Equal(5, page2.Metadata.Total);
        Assert.Equal(3, page2.Metadata.LastPage);
        Assert.True(page2.HasNextPage);

        var last = await repo.PageAsync(new OffsetPageRequest(3, 2), q => q.OrderBy(v => v.Id), v => v.Tag == "view");
        Assert.Single(last.Items);
        Assert.False(last.HasNextPage);
    }
}
