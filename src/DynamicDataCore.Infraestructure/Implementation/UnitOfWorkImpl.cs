using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DynamicDataCore.Infraestructure.Implementation
{

    /// <summary>
    /// Description: Provides a concrete implementation of the <see cref="IUnitOfWork"/> interface,
    /// coordinating multiple repository operations within a single transactional scope and
    /// ensuring atomic persistence through Entity Framework Core.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public class UnitOfWorkImpl(IAppDbContext context) : IUnitOfWork, IDisposable
    {

        private readonly IAppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private IDbContextTransaction? _transaction;
        private bool _disposed;
        private readonly Dictionary<Type, object> _repositories = [];

        /// <summary>
        /// Description: Retrieves a generic repository instance for the specified entity type.
        /// </summary>
        /// <typeparam name="T">The entity type associated with the repository.</typeparam>
        /// <returns>An instance of <see cref="IGenericRepository{T}"/>.</returns>
        public IGenericRepository<T> Repository<T>() where T : class
        {
            if (_repositories.ContainsKey(typeof(T)))
                return (IGenericRepository<T>)_repositories[typeof(T)];

            var repo = new GenericRepositoryImpl<T>(_context);
            _repositories.Add(typeof(T), repo);
            return repo;
        }

        /// <summary>
        /// Description: Indicates whether there is an active transaction currently in progress.
        /// </summary>
        public bool HasActiveTransaction => _transaction != null;

        /// <summary>
        /// Description: Begins a new database transaction asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when an active transaction already exists.</exception>
        public async Task BeginTransactionAsync()
        {
            if (_transaction != null)
                throw new InvalidOperationException("An active transaction already exists.");

            if (_context is DbContext dbContext)
            {
                _transaction = await dbContext.Database.BeginTransactionAsync();
            }
            else
            {
                throw new InvalidOperationException("Unable to start transaction. The provided context is not a valid DbContext.");
            }
        }

        /// <summary>
        /// Description: Commits the active transaction asynchronously,
        /// saving all pending changes atomically.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous commit operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when there is no active transaction.</exception>
        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
                throw new InvalidOperationException("No active transaction to commit.");

            await _context.SaveChangesAsync();
            await DisposeTransactionAsync();
        }

        /// <summary>
        /// Description: Rolls back the active transaction asynchronously,
        /// discarding any uncommitted changes.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous rollback operation.</returns>
        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null) return;

            await _transaction.RollbackAsync();
            await DisposeTransactionAsync();
        }

        /// <summary>
        /// Description: Retrieves the name of the database provider used by the current context.
        /// </summary>
        /// <returns>A string representing the database provider name, or <c>null</c> if unavailable.</returns>
        public string? GetDatabaseProviderName()
            => (_context as DbContext)?.Database.ProviderName;

        /// <summary>
        /// Description: Saves all pending changes to the database asynchronously,
        /// encapsulating error handling and transaction awareness.
        /// </summary>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> containing a <see cref="bool"/> value
        /// indicating whether the operation succeeded.
        /// </returns>
        public async Task<OperationResult<bool>> SaveChangesAsync()
        {
            try
            {
                // If a transaction is active, defer save until commit
                if (_transaction != null)
                    return OperationResult<bool>.Ok(true);

                await _context.SaveChangesAsync();
                return OperationResult<bool>.Ok(true);
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                return OperationResult<bool>.Fail("Concurrency conflict detected while saving changes.", concEx);
            }
            catch (DbUpdateException dbEx)
            {
                var entries = dbEx.Entries.Select(e => e.Entity.GetType().Name);
                var providerName = GetDatabaseProviderName();
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                string detailedMessage = providerName switch
                {
                    "Microsoft.EntityFrameworkCore.SqlServer" => $"SQL Server error: {innerMessage}",
                    "Npgsql.EntityFrameworkCore.PostgreSQL" => $"PostgreSQL error: {innerMessage}",
                    _ => innerMessage
                };

                return OperationResult<bool>.Fail($"DbUpdateException ({string.Join(", ", entries)}): {detailedMessage}", dbEx);
            }
            catch (ValidationException valEx)
            {
                return OperationResult<bool>.Fail($"ValidationException: {valEx.Message}", valEx);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Unexpected error occurred while saving changes.", ex);
            }
        }

        /// <summary>
        /// Description: Disposes the current database transaction asynchronously and releases related resources.
        /// </summary>
        private async Task DisposeTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// Description: Releases all managed resources associated with the current Unit of Work instance.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _transaction?.Dispose();
            (_context as IDisposable)?.Dispose();
            _disposed = true;
        }

    }
}
