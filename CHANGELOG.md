# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/)
and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [2.0.0] - 2026-05-17

### Added

- **Multi-database support** via `IDbContextProvider` and `IDbContextFactory<TContext>`. Map logical keys to DbContext types with `AddDynamicCoreInfrastructure(opt => opt.AddDatabase<PrimaryDb>("Primary"))`.
- **Four pagination strategies**:
  - `RetrievePagedByOffsetAsync` — offset/limit with mandatory `orderBy` for determinism.
  - `RetrievePagedByKeysetAsync` — keyset ("seek with cursor") for large tables; no COUNT query.
  - `RetrievePagedBySeekAsync` — last-seen-ID variant of keyset; ideal for infinite scroll feeds.
  - `RetrievePagedByCursorAsync` — opaque base64 cursor delegating to keyset internally.
- **`IAsyncEnumerable<T> StreamAsync(...)`** on `IGenericRepository<T>` and `IBaseGenericService<T>` for memory-bounded iteration of large result sets.
- **Bulk operations with back-pressure** — `BulkInsertAsync`, `BulkUpdateAsync`, `BulkDeleteAsync` process entities in configurable batches via `System.Threading.Channels` producer-consumer pipeline.
- **`CancellationToken`** propagated through every public async method on all interfaces.
- **`IAsyncDisposable`** on `IUnitOfWork` and `UnitOfWorkImpl`; use `await using` to safely release transactions.
- **`.NET 10` multi-targeting** — packages now target both `net8.0` and `net10.0`.
- **`IDbContextProvider`** interface (`CreateContext<TContext>(string key)`, `GetRegisteredKeys()`).
- **Pagination records**: `OffsetPageRequest`, `KeysetPageRequest<TKey>`, `SeekPageRequest<TKey>`, `CursorPageRequest`, `PagedResult<T>`, `SortDirection`.
- **`Directory.Build.props`** — `CLAUDE.md` and `AGENTS.md` are committed to the repo but excluded from the published NuGet package.
- **`CLAUDE.md`** and **`AGENTS.md`** — AI assistant guides for Claude Code, Cursor, Aider, and similar tools.

### Changed

- `OperationResult<T>` and `PaginationMetadata` converted from mutable classes to **`sealed record`** with `init`-only setters. Existing `.Ok()` / `.Fail()` calls are source-compatible.
- `BaseGenericServiceFactoryImpl` renamed to **`ScopedServiceFactory`**, now registered as **Scoped** (previously Singleton — fixed captive dependency bug where Scoped DbContexts were resolved from the root container).
- Project `DynamicDataCore.Infraestructure` (typo) renamed to **`DynamicDataCore.Infrastructure`**. Assembly name and root namespace updated. Package ID (`DynamicDataCore`) is unchanged.
- All concrete implementations are now **`sealed class`**. All DTOs are **`sealed record`**. All files use **file-scoped namespaces**.

### Fixed

- **Critical — transactions**: `UnitOfWorkImpl.CommitTransactionAsync` previously disposed the transaction without calling `CommitAsync()`, silently triggering an automatic rollback on every commit. Now correctly calls `SaveChangesAsync` → `CommitAsync` with automatic rollback on any exception.
- `UnitOfWorkImpl.Dispose` no longer disposes the injected `DbContext`. The DI container owns the DbContext lifetime.
- `BeginTransactionAsync` race condition: concurrent calls could both pass the `_transaction != null` guard. Fixed with `SemaphoreSlim(1, 1)`.

### Deprecated

- **`IBaseGenericServiceFactory.Create<T>(string schemaName)`** — marked `[Obsolete]`. Use `IDbContextProvider.CreateContext<TContext>(databaseKey)` with scoped `IBaseGenericService<T>` registration instead. Will be removed in v3.0.
- **`IGenericRepository<T>.RetrievePagedAsync(int page, int perPage, ...)`** — marked `[Obsolete]`. Use one of the four new typed pagination methods. Will be removed in v3.0.

### Breaking changes

- Namespace `DynamicDataCore.Infraestructure.*` → `DynamicDataCore.Infrastructure.*`. Update `using` directives.
- `OperationResult<T>` is now a record; code that used `new OperationResult<T>()` with property setters (not the `Ok`/`Fail` factories) will not compile.
- `IUnitOfWork` now extends `IAsyncDisposable, IDisposable`. Custom `IUnitOfWork` implementations must add `DisposeAsync()`.

---

## [1.0.2] - 2026-01-07

### Added
- Support for dynamic primary key types (`int`, `Guid`, `string`, etc.) across repositories and services.
- Unified identifier handling using EF Core native `FindAsync(object[])`.

### Changed
- `RetrieveByIdAsync` now accepts `object id` instead of `int`.
- `DeleteAsync` now accepts `object id` instead of `int`.
- Updated repository and service interfaces to be fully key-agnostic.

### Fixed
- Removed implicit coupling to integer-based primary keys.
- Improved long-term scalability for heterogeneous domain models.

---

## [1.0.1]
- Initial GitHub Package release.
