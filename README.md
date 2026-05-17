# DynamicDataCore

[![NuGet](https://img.shields.io/badge/nuget-v2.0.0-blue)](https://github.com/CesarSoftNica175/DynamicDataCore/packages)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-purple)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)

Lightweight, extensible data access framework for .NET built on EF Core. Provides Unit of Work, generic repositories, four pagination strategies, async streaming, bulk operations, and multi-database support — all with full `CancellationToken` propagation.

---

## What's new in v2.0.0

- **Critical bug fix** — transactions now actually commit (v1 disposed without calling `CommitAsync`).
- **Multi-database** — `IDbContextProvider` with `IDbContextFactory<TContext>` pooling per logical key.
- **Four pagination strategies** — Offset, Keyset, Seek (last-seen-ID), and Cursor.
- **`IAsyncEnumerable<T>` streaming** — memory-bounded iteration for large result sets.
- **Bulk operations** — producer-consumer pipeline via `System.Threading.Channels`.
- **`CancellationToken`** on every public async API.
- **`IAsyncDisposable`** on `IUnitOfWork`; use `await using`.
- **Multi-target** — `net8.0` and `net10.0`.
- **`record sealed`** for all DTOs; `sealed class` for all implementations.

See [CHANGELOG.md](CHANGELOG.md) for the full list. Upgrading from v1? See [docs/migration-v1-to-v2.md](docs/migration-v1-to-v2.md).

---

## Installation

The package is published to GitHub Packages. Add the source to your `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/CesarSoftNica175/index.json" />
  </packageSources>
</configuration>
```

```bash
dotnet add package DynamicDataCore --version 2.0.0
```

---

## Quick start — single database

Register your `DbContext` and add DynamicDataCore:

```csharp
// Program.cs
builder.Services.AddDbContextPool<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDynamicCoreInfrastructure();
```

Inject and use `IBaseGenericService<T>`:

```csharp
public class ProductService(IBaseGenericService<Product> svc)
{
    public async Task AddAsync(Product p, CancellationToken ct = default)
    {
        var result = await svc.AddAsync(p, ct);
        if (!result.Success) throw new Exception(result.Message);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await svc.RetrieveAsync(cancellationToken: ct);
        return result.Data ?? [];
    }
}
```

Any primary key type is supported — `int`, `Guid`, `string`, etc.:

```csharp
var result = await svc.RetrieveByIdAsync(Guid.Parse("..."), cancellationToken: ct);
```

---

## Multi-database setup

> **Note:** `IDbContextProvider` is only registered when using the `Action<DynamicCoreOptions>` overload below. The parameterless `AddDynamicCoreInfrastructure()` does **not** register it.

Register each `DbContext` independently, then map logical keys:

```csharp
builder.Services.AddDbContextPool<PrimaryDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration["Databases:Primary"]));

builder.Services.AddDbContextPool<ReportingDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["Databases:Reporting"]));

builder.Services.AddDynamicCoreInfrastructure(opt =>
{
    opt.AddDatabase<PrimaryDbContext>("Primary");
    opt.AddDatabase<ReportingDbContext>("Reporting");
});
```

Resolve a typed context by key:

```csharp
public class ReportService(IDbContextProvider provider)
{
    public async Task RunReportAsync(CancellationToken ct)
    {
        using var ctx = provider.CreateContext<ReportingDbContext>("Reporting");
        var rows = await ctx.Set<SalesRow>().ToListAsync(ct);
        // ...
    }
}
```

---

## Transactions

`IUnitOfWork` implements `IAsyncDisposable` — always use `await using`:

```csharp
public class OrderService(IUnitOfWork uow)
{
    public async Task<OperationResult<bool>> CreateOrderAsync(
        Order order, IEnumerable<OrderItem> items, CancellationToken ct = default)
    {
        try
        {
            await uow.BeginTransactionAsync(ct);

            var orderResult = await uow.Repository<Order>().AddAsync(order, ct);
            if (!orderResult.Success) throw new Exception(orderResult.Message);

            foreach (var item in items)
            {
                var itemResult = await uow.Repository<OrderItem>().AddAsync(item, ct);
                if (!itemResult.Success) throw new Exception(itemResult.Message);
            }

            await uow.CommitTransactionAsync(ct);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await uow.RollbackTransactionAsync(ct);
            return OperationResult<bool>.Fail("Transaction rolled back.", ex);
        }
    }
}
```

---

## Pagination — choosing a strategy

| Strategy | Use when | Method |
|---|---|---|
| **Offset** | Small tables, random-access by page number | `RetrievePagedByOffsetAsync` |
| **Keyset** | Large append-only tables, sorted navigation | `RetrievePagedByKeysetAsync` |
| **Seek / Last-Seen-ID** | ID-ordered feeds (infinite scroll) | `RetrievePagedBySeekAsync` |
| **Cursor** | Stable opaque cursor exposed in API responses | `RetrievePagedByCursorAsync` |

### Offset pagination

```csharp
var page = await svc.RetrievePagedByOffsetAsync(
    request: new OffsetPageRequest(Page: 2, PerPage: 20),
    orderBy: p => p.CreatedAt,
    cancellationToken: ct);

// page.Items          — current page entities
// page.Metadata       — CurrentPage, PerPage, Total, LastPage
// page.HasNextPage    — true if more pages exist
```

### Keyset pagination (no COUNT query, fast on large tables)

```csharp
// First page — After: null fetches from the beginning
var page = await svc.RetrievePagedByKeysetAsync(
    request: new KeysetPageRequest<int>(After: null, PerPage: 20),
    keySelector: p => p.Id,
    cancellationToken: ct);

// Next page — pass last ID from previous page
var next = await svc.RetrievePagedByKeysetAsync(
    request: new KeysetPageRequest<int>(After: page.Items[^1].Id, PerPage: 20),
    keySelector: p => p.Id,
    cancellationToken: ct);
```

### Seek / Last-Seen-ID

```csharp
var page = await svc.RetrievePagedBySeekAsync(
    request: new SeekPageRequest<int>(LastSeenId: lastId, PerPage: 20),
    keySelector: p => p.Id,
    cancellationToken: ct);
```

### Cursor pagination (stable opaque cursors)

```csharp
// First page
var page = await svc.RetrievePagedByCursorAsync(
    request: new CursorPageRequest(Cursor: null, PerPage: 20),
    keySelector: p => p.Id,
    cancellationToken: ct);

// Subsequent page — use NextCursor from previous response
var next = await svc.RetrievePagedByCursorAsync(
    request: new CursorPageRequest(Cursor: page.NextCursor, PerPage: 20),
    keySelector: p => p.Id,
    cancellationToken: ct);
```

---

## Streaming large result sets

Use `StreamAsync` to iterate millions of rows without loading them all into memory:

```csharp
await foreach (var product in svc.StreamAsync(
    predicate: p => p.IsActive,
    cancellationToken: ct))
{
    await ProcessAsync(product, ct);
}
```

---

## Bulk operations

`BulkInsertAsync`, `BulkUpdateAsync`, and `BulkDeleteAsync` process entities in configurable batches using a `System.Threading.Channels` producer-consumer pipeline. Back-pressure prevents out-of-memory errors on large collections.

```csharp
var result = await svc.BulkInsertAsync(products, batchSize: 500, cancellationToken: ct);
// result.Data — total rows processed
// result.Success — false if any batch throws
```

---

## OperationResult\<T\> pattern

All service and repository methods return `OperationResult<T>` — a `sealed record` that wraps success/failure uniformly:

```csharp
var result = await svc.AddAsync(entity, ct);

if (!result.Success)
{
    logger.LogError("Failed: {Message} | TraceId: {TraceId}", result.Message, result.TraceId);
    return Problem(result.Message);
}
```

Paginated results from the legacy `RetrievePagedAsync` include `result.PaginationMetadata`:

```csharp
var paged = await svc.RetrievePagedAsync(page: 1, perPage: 10);
var meta = paged.PaginationMetadata; // CurrentPage, Total, LastPage, etc.
```

---

## Architecture

```mermaid
flowchart TD
    Controller -->|injects| Service[IBaseGenericService&lt;T&gt;]
    Service --> UoW[IUnitOfWork]
    Service --> Repo[IGenericRepository&lt;T&gt;]
    UoW -->|creates| UoWImpl[UnitOfWorkImpl]
    Repo --> RepoImpl[GenericRepositoryImpl&lt;T&gt;]
    UoWImpl --> DbContext
    RepoImpl --> DbContext
    Provider[IDbContextProvider] -->|resolves via| Factory[IDbContextFactory&lt;TContext&gt;]
    Factory --> DbContext
    DbContext --> SQL[SQL / PostgreSQL]
```

---

## Package summary

| Layer | Type | Description |
|---|---|---|
| Abstractions | `IAppDbContext` | Minimal EF Core DbContext abstraction |
| Abstractions | `IUnitOfWork` | Transaction coordination + repository access |
| Abstractions | `IGenericRepository<T>` | CRUD, pagination, streaming, bulk |
| Abstractions | `IBaseGenericService<T>` | Application-layer service over repository |
| Abstractions | `IDbContextProvider` | Multi-database context resolution by key |
| Implementation | `UnitOfWorkImpl` | EF Core `IUnitOfWork` (sealed, IAsyncDisposable) |
| Implementation | `GenericRepositoryImpl<T>` | EF Core repository with all pagination strategies |
| Implementation | `BaseGenericServiceImpl<T>` | Delegates to UoW + repository |
| Implementation | `PooledDbContextProvider` | Resolves DbContexts via `IDbContextFactory<TContext>` |
| Common | `OperationResult<T>` | Sealed record — unified success/failure envelope |
| Common | `PaginationMetadata` | Sealed record — page metadata |
| Common | `PagedResult<T>` | Sealed record — items + metadata + next cursor |
| Common | `OffsetPageRequest` | Sealed record — page + perPage |
| Common | `KeysetPageRequest<TKey>` | Sealed record — after + perPage + direction |
| Common | `SeekPageRequest<TKey>` | Sealed record — lastSeenId + perPage |
| Common | `CursorPageRequest` | Sealed record — opaque cursor + perPage |

---

## License

MIT — free to use, modify, and distribute under the same terms.
