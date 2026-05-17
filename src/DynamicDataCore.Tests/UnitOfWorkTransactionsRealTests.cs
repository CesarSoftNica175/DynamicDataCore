using DynamicDataCore.Infrastructure.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Tests;

/// <summary>
/// Validates real transaction semantics using SQLite in-memory.
/// EF Core InMemory provider does not support transactions; SQLite does.
/// </summary>
public class UnitOfWorkTransactionsRealTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SqliteTestDbContext> _options;

    public UnitOfWorkTransactionsRealTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<SqliteTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new SqliteTestDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private SqliteTestDbContext NewContext() => new(_options);

    // ── Core commit/rollback semantics ───────────────────────────────────────

    [Fact]
    public async Task CommitTransactionAsync_PersistsData()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        await uow.BeginTransactionAsync();
        ctx.Set<TxEntity>().Add(new TxEntity { Name = "committed" });
        await uow.CommitTransactionAsync(); // calls SaveChanges + CommitAsync

        await using var verify = NewContext();
        var count = await verify.Set<TxEntity>().CountAsync();
        Assert.Equal(1, count);
        Assert.Equal("committed", (await verify.Set<TxEntity>().FirstAsync()).Name);
    }

    [Fact]
    public async Task RollbackTransactionAsync_DoesNotPersistData()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        await uow.BeginTransactionAsync();
        ctx.Set<TxEntity>().Add(new TxEntity { Name = "rollback-me" });
        // Call context SaveChanges directly so the row is staged within the transaction
        await ctx.SaveChangesAsync();
        await uow.RollbackTransactionAsync();

        await using var verify = NewContext();
        var count = await verify.Set<TxEntity>().CountAsync();
        Assert.Equal(0, count);
    }

    // ── Guard conditions ─────────────────────────────────────────────────────

    [Fact]
    public async Task BeginTransactionAsync_ThrowsWhenAlreadyActive()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        await uow.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => uow.BeginTransactionAsync());

        await uow.RollbackTransactionAsync();
    }

    [Fact]
    public async Task HasActiveTransaction_IsFalse_AfterCommit()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        await uow.BeginTransactionAsync();
        Assert.True(uow.HasActiveTransaction);
        await uow.CommitTransactionAsync();
        Assert.False(uow.HasActiveTransaction);
    }

    [Fact]
    public async Task HasActiveTransaction_IsFalse_AfterRollback()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        await uow.BeginTransactionAsync();
        Assert.True(uow.HasActiveTransaction);
        await uow.RollbackTransactionAsync();
        Assert.False(uow.HasActiveTransaction);
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_ReleasesTransaction()
    {
        await using var ctx = NewContext();

        await using (var uow = new UnitOfWorkImpl(ctx))
        {
            await uow.BeginTransactionAsync();
            Assert.True(uow.HasActiveTransaction);
        } // DisposeAsync called here

        // A new UoW on the same context should be able to begin a new transaction
        await using var uow2 = new UnitOfWorkImpl(ctx);
        // After dispose, previous transaction is cleaned up — no InvalidOperationException
        await uow2.BeginTransactionAsync();
        Assert.True(uow2.HasActiveTransaction);
        await uow2.RollbackTransactionAsync();
    }

    // ── Concurrency guard (SemaphoreSlim) ────────────────────────────────────

    [Fact]
    public async Task BeginTransactionAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var successes = 0;

        var t1 = Task.Run(async () =>
        {
            try { await uow.BeginTransactionAsync(); Interlocked.Increment(ref successes); }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        var t2 = Task.Run(async () =>
        {
            try { await uow.BeginTransactionAsync(); Interlocked.Increment(ref successes); }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        await Task.WhenAll(t1, t2);

        Assert.Equal(1, successes);
        Assert.Single(exceptions);
        Assert.IsType<InvalidOperationException>(exceptions.First());

        await uow.RollbackTransactionAsync();
    }

    // ── SaveChangesAsync in transaction mode ─────────────────────────────────

    [Fact]
    public async Task SaveChangesAsync_IsNoop_WhenTransactionActive()
    {
        await using var ctx = NewContext();
        var uow = new UnitOfWorkImpl(ctx);

        await uow.BeginTransactionAsync();
        // SaveChangesAsync returns Ok(true) without actually flushing during an active transaction
        var result = await uow.SaveChangesAsync();
        Assert.True(result.Success);

        await uow.RollbackTransactionAsync();
    }
}
