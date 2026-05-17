using DynamicDataCore.Infrastructure.Implementation;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Tests;

public class BulkProcessorTests
{
    private static async Task<InMemoryDbContext<BulkEntity>> BuildContextAsync(string dbName)
    {
        var opts = new DbContextOptionsBuilder<InMemoryDbContext<BulkEntity>>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new InMemoryDbContext<BulkEntity>(opts);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    [Fact]
    public async Task BulkInsertAsync_Inserts1500Entities_InBatchesOf500()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var ctx = await BuildContextAsync(dbName);
        var repo = new GenericRepositoryImpl<BulkEntity>(ctx);

        var entities = Enumerable.Range(1, 1500)
            .Select(i => new BulkEntity { Id = i, Value = $"V{i}" });

        var result = await repo.BulkInsertAsync(entities, batchSize: 500);

        Assert.True(result.Success);
        Assert.Equal(1500, result.Data);

        var stored = await ctx.Set<BulkEntity>().CountAsync();
        Assert.Equal(1500, stored);
    }

    [Fact]
    public async Task BulkInsertAsync_EmptyCollection_ReturnsOkWithZero()
    {
        await using var ctx = await BuildContextAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<BulkEntity>(ctx);

        var result = await repo.BulkInsertAsync([], batchSize: 500);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data);
    }

    [Fact]
    public async Task BulkUpdateAsync_UpdatesAllEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var ctx = await BuildContextAsync(dbName);
        ctx.Set<BulkEntity>().AddRange(
            Enumerable.Range(1, 10).Select(i => new BulkEntity { Id = i, Value = "old" }));
        await ctx.SaveChangesAsync();

        var repo = new GenericRepositoryImpl<BulkEntity>(ctx);
        var updated = ctx.Set<BulkEntity>().ToList();
        foreach (var e in updated) e.Value = "new";

        var result = await repo.BulkUpdateAsync(updated, batchSize: 5);

        Assert.True(result.Success);
        Assert.Equal(10, result.Data);
        Assert.All(ctx.Set<BulkEntity>().ToList(), e => Assert.Equal("new", e.Value));
    }

    [Fact]
    public async Task BulkDeleteAsync_DeletesAllEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var ctx = await BuildContextAsync(dbName);
        ctx.Set<BulkEntity>().AddRange(
            Enumerable.Range(1, 20).Select(i => new BulkEntity { Id = i, Value = $"V{i}" }));
        await ctx.SaveChangesAsync();

        var repo = new GenericRepositoryImpl<BulkEntity>(ctx);
        var toDelete = ctx.Set<BulkEntity>().ToList();

        var result = await repo.BulkDeleteAsync(toDelete, batchSize: 10);

        Assert.True(result.Success);
        Assert.Equal(20, result.Data);
        Assert.Equal(0, await ctx.Set<BulkEntity>().CountAsync());
    }

    [Fact]
    public async Task BulkInsertAsync_Cancellation_ReturnsFail()
    {
        await using var ctx = await BuildContextAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<BulkEntity>(ctx);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var entities = Enumerable.Range(1, 100).Select(i => new BulkEntity { Id = i, Value = $"V{i}" });
        var result = await repo.BulkInsertAsync(entities, batchSize: 10, cts.Token);

        Assert.False(result.Success);
    }
}
