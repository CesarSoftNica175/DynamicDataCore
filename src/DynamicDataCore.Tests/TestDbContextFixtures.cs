using DynamicDataCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DynamicDataCore.Tests;

// ── Shared in-memory entity + context ──────────────────────────────────────

public class PaginableItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class StreamItem
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class BulkEntity
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class TxEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Generic in-memory context usable for any entity type
public class InMemoryDbContext<TEntity> : DbContext, IAppDbContext
    where TEntity : class
{
    public InMemoryDbContext(DbContextOptions<InMemoryDbContext<TEntity>> options)
        : base(options) { }

    DatabaseFacade IAppDbContext.Database => Database;
    DbSet<T> IAppDbContext.Set<T>() => Set<T>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TEntity>();
}

// SQLite context for real-transaction tests (IAppDbContext + DbContext)
public class SqliteTestDbContext : DbContext, IAppDbContext
{
    public SqliteTestDbContext(DbContextOptions<SqliteTestDbContext> options)
        : base(options) { }

    DatabaseFacade IAppDbContext.Database => Database;
    DbSet<T> IAppDbContext.Set<T>() => Set<T>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TxEntity>();
}

// Minimal DbContexts for IDbContextProvider tests
public class ProviderDbA : DbContext
{
    public ProviderDbA(DbContextOptions<ProviderDbA> options) : base(options) { }
}

public class ProviderDbB : DbContext
{
    public ProviderDbB(DbContextOptions<ProviderDbB> options) : base(options) { }
}
