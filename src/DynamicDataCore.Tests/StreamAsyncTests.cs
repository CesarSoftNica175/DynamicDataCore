using DynamicDataCore.Infrastructure.Implementation;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Tests;

public class StreamAsyncTests
{
    private static async Task<InMemoryDbContext<StreamItem>> SeedAsync(string dbName, int count)
    {
        var opts = new DbContextOptionsBuilder<InMemoryDbContext<StreamItem>>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new InMemoryDbContext<StreamItem>(opts);
        ctx.Set<StreamItem>().AddRange(
            Enumerable.Range(1, count).Select(i => new StreamItem { Id = i, Label = $"L{i}" }));
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task StreamAsync_ReturnsAllEntities()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString(), 1000);
        var repo = new GenericRepositoryImpl<StreamItem>(ctx);

        var count = 0;
        await foreach (var _ in repo.StreamAsync())
            count++;

        Assert.Equal(1000, count);
    }

    [Fact]
    public async Task StreamAsync_WithPredicate_FiltersCorrectly()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString(), 100);
        var repo = new GenericRepositoryImpl<StreamItem>(ctx);

        var ids = new List<int>();
        await foreach (var item in repo.StreamAsync(predicate: s => s.Id < 50))
            ids.Add(item.Id);

        Assert.Equal(49, ids.Count);
        Assert.All(ids, id => Assert.True(id < 50));
    }

    [Fact]
    public async Task StreamAsync_Cancellation_StopsIteration()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString(), 1000);
        var repo = new GenericRepositoryImpl<StreamItem>(ctx);

        using var cts = new CancellationTokenSource();
        var count = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in repo.StreamAsync(cancellationToken: cts.Token))
            {
                count++;
                if (count == 10) cts.Cancel();
            }
        });

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task StreamAsync_EmptyDb_ReturnsNoItems()
    {
        var opts = new DbContextOptionsBuilder<InMemoryDbContext<StreamItem>>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new InMemoryDbContext<StreamItem>(opts);
        var repo = new GenericRepositoryImpl<StreamItem>(ctx);

        var count = 0;
        await foreach (var _ in repo.StreamAsync())
            count++;

        Assert.Equal(0, count);
    }
}
