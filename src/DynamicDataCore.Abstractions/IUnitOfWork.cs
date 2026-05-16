using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions;

/// <summary>
/// Coordinates transactional operations across multiple repositories within a single database context.
/// Implements <see cref="IAsyncDisposable"/> to release pooled DbContext instances cleanly.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    /// <summary>Retrieves a cached repository for the specified entity type.</summary>
    IGenericRepository<T> Repository<T>() where T : class;

    /// <summary>Returns the active database provider name (e.g., SqlServer, Npgsql).</summary>
    string? GetDatabaseProviderName();

    /// <summary>Begins a new database transaction.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the active transaction, persisting all pending changes atomically.</summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the active transaction, discarding all uncommitted changes.</summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes outside of an explicit transaction.</summary>
    Task<OperationResult<bool>> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>True when an explicit transaction is currently open.</summary>
    bool HasActiveTransaction { get; }
}
