using System;
using System.Threading.Tasks;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions
{

    /// <summary>
    /// Description: Defines the contract for implementing the Unit of Work pattern,
    /// which coordinates transactional operations and ensures that multiple repositories
    /// work together under a single database context.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {

        /// <summary>
        /// Description: Retrieves a generic repository for a specific entity type.
        /// </summary>
        /// <typeparam name="T">The entity type for which the repository is created.</typeparam>
        /// <returns>An instance of <see cref="IGenericRepository{T}"/> associated with the current context.</returns>
        IGenericRepository<T> Repository<T>() where T : class;

        /// <summary>
        /// Description: Returns the name of the active database provider
        /// (e.g., Microsoft.EntityFrameworkCore.SqlServer, Npgsql, etc.).
        /// </summary>
        /// <returns>A string representing the database provider name, or <c>null</c> if unavailable.</returns>
        string? GetDatabaseProviderName();

        /// <summary>
        /// Description: Begins a new database transaction asynchronously,
        /// ensuring that all subsequent operations are executed within the same transactional scope.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task BeginTransactionAsync();

        /// <summary>
        /// Description: Commits the current transaction asynchronously,
        /// persisting all pending changes to the database atomically.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task CommitTransactionAsync();

        /// <summary>
        /// Description: Rolls back the current transaction asynchronously,
        /// reverting all uncommitted changes.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task RollbackTransactionAsync();

        /// <summary>
        /// Description: Persists all pending changes in the current context
        /// to the underlying database asynchronously.
        /// </summary>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> containing a <see cref="bool"/> value
        /// indicating the success or failure of the operation.
        /// </returns>
        Task<OperationResult<bool>> SaveChangesAsync();

        /// <summary>
        /// Description: Indicates whether an active transaction is currently in progress.
        /// </summary>
        bool HasActiveTransaction { get; }

    }
}
