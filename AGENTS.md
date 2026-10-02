# DynamicDataCore — AI Agents Guide

Cross-tool instructions for Cursor, Aider, Codex, and other AI coding assistants.
For Claude Code specifics, see [CLAUDE.md](CLAUDE.md).

## Stack

- .NET multi-target: `net8.0` + `net10.0`
- Language: C# 13, `LangVersion=latest`
- Version 2.1.0
- EF Core 9.x on `net8.0`, EF Core 10.x on `net10.0` (provider-agnostic — no SqlServer/Npgsql references in the core)
- Test: xUnit 2.9.3 + Moq 4.20.72 + Testcontainers.MsSql (integration tests need Docker)

## Commands

| Action | Command |
|---|---|
| Build | `dotnet build -c Release` |
| Test | `dotnet test src/DynamicDataCore.Tests` (Docker required; add `-f net8.0` / `-f net10.0` to pick a TFM; `--filter "Category!=Integration"` skips containers) |
| Pack | `dotnet pack src/DynamicDataCore.Infrastructure -c Release -o ./artifacts` |
| Format | `dotnet format` |

## Architecture

Three layers:

1. **Abstractions** (`DynamicDataCore.Abstractions`) — interfaces only, no implementation
2. **Common** (`DynamicDataCore.Common`) — pure data types (`record sealed`), no dependencies
3. **Infrastructure** (`DynamicDataCore.Infrastructure`) — EF Core implementation, depends on Abstractions + Common

## Key Conventions

- All concrete classes: `sealed`
- All DTOs / request-response types: `record sealed`
- All public async methods: accept `CancellationToken cancellationToken = default`
- Multi-database: map logical keys to `DbContext` types via `AddDynamicCoreInfrastructure(opt => opt.AddDatabase<T>("key"))`
- Streaming: use `IAsyncEnumerable<T>` from `IGenericRepository.StreamAsync`
- Bulk: use Channel-based batching in `BulkInsertAsync` / `BulkUpdateAsync` / `BulkDeleteAsync`

## Stored Procedures, Views and the Raw-SQL Lock (2.1.0)

- Stored procedures: inject `ISqlProcedureExecutor`; pass a `ProcedureName` (validated `schema.name`), typed `ProcedureParameters` and `TableTypeName` for table-valued parameters. Only `CommandType.StoredProcedure` is used.
- Views / keyless entities: `IReadRepository<T>` plus `ConfigureReadOnlyView<T>()`.
- ZERO raw SQL: `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQuery`, `CommandText` and `SqlCommand(string)` are banned in the libraries by analyzer RS0030 (`BannedSymbols.txt`, errors in `Directory.Build.props`).
- The only `#pragma warning disable RS0030` is in `SqlProcedureExecutor.cs`; a test fails if another one (or a `SuppressMessage` for RS0030) appears in library sources. Never add suppressions.
- `CLAUDE.md` and `AGENTS.md` must stay out of the NuGet packages.

## What NOT to Change Without Asking

- `.github/workflows/publish.yml` (controls NuGet publishing to GitHub Packages)
- Package version numbers in `*.csproj` (bump manually at release time)
- `IAppDbContext` interface (consumers implement this — breaking changes affect all users)
