# DynamicDataCore — Claude Code Guide

## Project

NuGet framework for dynamic data access via EF Core (MSSQL + PostgreSQL).
Three publishable packages: `DynamicDataCore`, `DynamicDataCore.Abstractions`, `DynamicDataCore.Common`.

## Structure

```
src/
  DynamicDataCore.Abstractions/   — Pure interfaces (IUnitOfWork, IGenericRepository<T>, IBaseGenericService<T>, IDbContextProvider)
  DynamicDataCore.Common/         — Records: OperationResult<T>, PaginationMetadata, pagination request/response types
  DynamicDataCore.Infrastructure/ — EF Core implementations (UnitOfWorkImpl, GenericRepositoryImpl<T>, etc.)
  DynamicDataCore.Tests/          — xUnit + Moq unit tests + Testcontainers SQL Server integration tests (needs Docker)
```

## Key Workflows

```bash
dotnet restore
dotnet build -c Release
dotnet test src/DynamicDataCore.Tests --logger "console;verbosity=detailed"
dotnet pack src/DynamicDataCore.Infrastructure -c Release -o ./artifacts
```

After packing, verify AI files are absent from the nupkg:
```powershell
$nupkg = Get-ChildItem ./artifacts/*.nupkg | Select-Object -First 1
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
$zip.Entries | Where-Object { $_.Name -match 'claude|agents|copilot|\.ai' }
$zip.Dispose()
```

## Coding Conventions

- `sealed class` for all concrete implementations — no extension points unless explicitly needed
- `record sealed` for all DTOs, request/response types, `OperationResult<T>`, `PaginationMetadata`
- File-scoped namespaces (`namespace Foo.Bar;`) in all `.cs` files
- `CancellationToken cancellationToken = default` on **every** public async method
- `IAsyncDisposable` for any type owning a `DbContext` or transaction
- Never use `Type.GetType(string)` to resolve DbContexts — use `IDbContextFactory<TContext>` or `IDbContextProvider`
- Never couple `DynamicDataCore.Infrastructure` to specific providers (SqlServer/Npgsql) — providers are the consumer's responsibility
- EF Core 9.x on `net8.0`, 10.x on `net10.0` (versions set per TFM in `Directory.Build.props`)
- No EF Core migrations in this package — consumers own their own DbContext and migrations

## Stored Procedures and Read-Only Views (2.1.0)

- `ISqlProcedureExecutor` (Abstractions) is the only way to call stored procedures. Inputs are typed: `ProcedureName` (validated `schema.name`), `ProcedureParameters`/`ProcedureParameter`, and `TableTypeName` for table-valued parameters. Results come as `ProcedureResult` / `ProcedureMultipleResult`. Only `CommandType.StoredProcedure` is ever used.
- Register with `AddDynamicDataCoreProcedures(connectionStringName)`.
- `IReadRepository<T>` serves keyless entities (views): map them with `ModelBuilder.ConfigureReadOnlyView<T>(viewName, schema)`, register with `AddDynamicDataCoreReadRepositories()`.
- **ZERO raw SQL** in the libraries: no `FromSqlRaw`/`ExecuteSqlRaw`/`SqlQuery`, no `CommandText`, no `SqlCommand(string)`. This is enforced at compile time by the RS0030 analyzer (`Microsoft.CodeAnalysis.BannedApiAnalyzers`, symbols in `BannedSymbols.txt`, warnings-as-errors in `Directory.Build.props`; the test project is excluded).
- The single permitted `#pragma warning disable RS0030` lives in `SqlProcedureExecutor.cs` (assigns `CommandText` from a validated `ProcedureName`). A test fails if any other RS0030 suppression appears in library sources.
- Integration tests share one SQL Server container per test process (`SqlServerFixture`); a cross-process file lock serializes the net8.0 and net10.0 hosts when `dotnet test` runs both TFMs.

## Multi-Database Pattern

```csharp
// In consumer startup:
services.AddDbContextPool<PrimaryDb>(opt => opt.UseSqlServer(config["Primary"]));
services.AddDbContextPool<ReportingDb>(opt => opt.UseNpgsql(config["Reporting"]));

services.AddDynamicCoreInfrastructure(opt =>
{
    opt.AddDatabase<PrimaryDb>("Primary");
    opt.AddDatabase<ReportingDb>("Reporting");
});

// Usage via IDbContextProvider:
var ctx = provider.CreateContext<PrimaryDb>("Primary");
```

## Pagination Quick Reference

| Strategy | Use When | Method |
|---|---|---|
| Offset | Small tables, random access | `RetrievePagedByOffsetAsync` |
| Keyset | Large append tables, sorted by key | `RetrievePagedByKeysetAsync` |
| Seek / Last-Seen-ID | ID-ordered feeds (like infinite scroll) | `RetrievePagedBySeekAsync` |
| Cursor | Stable pagination under concurrent inserts | `RetrievePagedByCursorAsync` |

## Rules

- Do NOT push to `master` directly — use `feature/*` or `fix/*` branches
- Do NOT modify `.github/workflows/publish.yml` without user confirmation
- Do NOT commit `appsettings.Local.json`, `*.user`, or any file with real credentials
- Breaking changes require a major version bump (current line: 2.1.0)
- `CLAUDE.md` and `AGENTS.md` are excluded from the NuGet packages (see `Directory.Build.props`) — keep them that way
- Run `dotnet test` before every commit — CI blocks on test failures
