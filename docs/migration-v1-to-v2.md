# Migrating from DynamicDataCore v1.x to v2.0

This guide covers every breaking change and the recommended replacement pattern for each deprecated API.

---

## 1. Namespace rename (Infraestructure → Infrastructure)

The project had a typo in its namespace. Replace every `using` that referenced the old spelling:

```diff
- using DynamicDataCore.Infraestructure.Implementation;
- using DynamicDataCore.Infraestructure.Extensions;
+ using DynamicDataCore.Infrastructure.Implementation;
+ using DynamicDataCore.Infrastructure.Extensions;
```

In Visual Studio or Rider: **Edit → Find and Replace in Files** with regex disabled, replace `DynamicDataCore.Infraestructure` → `DynamicDataCore.Infrastructure` across the whole solution.

---

## 2. OperationResult\<T\> is now a sealed record

`OperationResult<T>` was converted from a mutable class to `sealed record`. Code using the static factories is source-compatible:

```csharp
// Unchanged — these still compile:
var ok = OperationResult<bool>.Ok(true);
var fail = OperationResult<bool>.Fail("Something went wrong");
```

Code that bypassed the factories and used `new OperationResult<T>()` with property setters will not compile. Migrate to the factories:

```diff
- var r = new OperationResult<bool> { Success = true, Data = true };
+ var r = OperationResult<bool>.Ok(true);
```

---

## 3. CancellationToken added to all async methods

Every async method on `IBaseGenericService<T>`, `IGenericRepository<T>`, and `IUnitOfWork` now has a `CancellationToken cancellationToken = default` parameter. Existing call sites compile without changes because the parameter is optional.

The recommended migration is to pass your controller's cancellation token through:

```csharp
// ASP.NET Core controller — pass CancellationToken to services
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id, CancellationToken ct)
{
    var result = await _service.RetrieveByIdAsync(id, cancellationToken: ct);
    return result.Success ? Ok(result.Data) : NotFound(result.Message);
}
```

---

## 4. Pagination — obsolete method replaced by four strategies

`IGenericRepository<T>.RetrievePagedAsync(int page, int perPage, ...)` still works with an `[Obsolete]` warning. Migrate to the strategy that fits your use case:

| Old call | New equivalent |
|---|---|
| `RetrievePagedAsync(page, perPage)` on small tables | `RetrievePagedByOffsetAsync(new OffsetPageRequest(page, perPage), orderBy: e => e.Id)` |
| Infinite scroll / large append-only tables | `RetrievePagedBySeekAsync(new SeekPageRequest<int>(lastSeenId, perPage), keySelector: e => e.Id)` |
| Stable cursor exposed to API clients | `RetrievePagedByCursorAsync(new CursorPageRequest(cursor, perPage), keySelector: e => e.Id)` |

The new offset method **requires** an `orderBy` expression. This was a silent bug in v1 where pagination was non-deterministic without a sort.

---

## 5. IBaseGenericServiceFactory is obsolete

The factory `IBaseGenericServiceFactory.Create<T>(string schemaName)` is marked `[Obsolete]`. The "schema" concept mapped to a DbContext type, which is now handled properly by `IDbContextProvider`.

**Before (v1):**
```csharp
var service = _factory.Create<Order>("Default");
```

**After (v2) — inject directly:**
```csharp
// Register in startup:
services.AddScoped<IBaseGenericService<Order>, BaseGenericServiceImpl<Order>>();

// Inject:
public OrderController(IBaseGenericService<Order> orderService) { ... }
```

**After (v2) — multi-database:**
```csharp
// Startup:
services.AddDbContextPool<PrimaryDbContext>(opt => opt.UseSqlServer(config["Primary"]));
services.AddDynamicCoreInfrastructure(opt => opt.AddDatabase<PrimaryDbContext>("Primary"));

// Usage via IDbContextProvider:
var ctx = _dbContextProvider.CreateContext<PrimaryDbContext>("Primary");
```

---

## 6. Configuration — DbContextMappings removed

The `DbContextMappings` section in `appsettings.json` is no longer used. Connections are now configured entirely in startup code via EF Core's `AddDbContext`/`AddDbContextPool` API:

```diff
// appsettings.json — remove this:
- "DbContextMappings": {
-   "Default": "MyApp.Data.AppDbContext"
- }
```

```csharp
// Program.cs — add this:
builder.Services.AddDbContextPool<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDynamicCoreInfrastructure();
```

---

## 7. Captive dependency fix — IBaseGenericServiceFactory lifetime changed to Scoped

In v1, `BaseGenericServiceFactoryImpl` was registered as **Singleton**. This caused a captive dependency bug: the Singleton held Scoped `DbContext` instances from the root container, creating subtle data consistency issues.

In v2, `ScopedServiceFactory` (the renamed implementation) is registered as **Scoped**.

**Impact**: if you injected `IBaseGenericServiceFactory` into a Singleton service, you will get a DI validation error at startup in v2. Solutions:

- Change your Singleton to Scoped if the lifetime allows it.
- Inject `IServiceScopeFactory` and create a scope explicitly:
  ```csharp
  using var scope = _scopeFactory.CreateScope();
  var factory = scope.ServiceProvider.GetRequiredService<IBaseGenericServiceFactory>();
  ```

---

## 8. IUnitOfWork now implements IAsyncDisposable

Custom `IUnitOfWork` implementations must add a `DisposeAsync()` method. If you have a mock or stub of `IUnitOfWork`, add the method:

```csharp
public ValueTask DisposeAsync() => ValueTask.CompletedTask;
```

The recommended usage pattern with `UnitOfWorkImpl` is:

```csharp
await using var uow = serviceProvider.GetRequiredService<IUnitOfWork>();
await uow.BeginTransactionAsync(ct);
// ...
await uow.CommitTransactionAsync(ct);
```
