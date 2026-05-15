# DynamicDataCore — AI Agents Guide

Cross-tool instructions for Cursor, Aider, Codex, and other AI coding assistants.
For Claude Code specifics, see [CLAUDE.md](CLAUDE.md).

## Stack

- .NET multi-target: `net8.0` + `net10.0`
- Language: C# 13, `LangVersion=latest`
- EF Core 9 (provider-agnostic — no SqlServer/Npgsql references in the core)
- Test: xUnit 2.9.3 + Moq 4.20.72

## Commands

| Action | Command |
|---|---|
| Build | `dotnet build -c Release` |
| Test | `dotnet test src/DynamicDataCore.Tests` |
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

## What NOT to Change Without Asking

- `.github/workflows/publish.yml` (controls NuGet publishing to GitHub Packages)
- Package version numbers in `*.csproj` (bump manually at release time)
- `IAppDbContext` interface (consumers implement this — breaking changes affect all users)
