using System.ComponentModel.DataAnnotations;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DynamicDataCore.Infrastructure.Implementation;

/// <summary>
/// Coordinates multiple repository operations within a single transactional scope.
/// Always dispose via <c>await using</c> to release pooled DbContext resources.
/// </summary>
public sealed class UnitOfWorkImpl : IUnitOfWork
{
    private readonly IAppDbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly SemaphoreSlim _txLock = new(1, 1);
    private readonly Dictionary<Type, object> _repositories = [];
    private bool _disposed;

    public UnitOfWorkImpl(IAppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IGenericRepository<T> Repository<T>() where T : class
    {
        if (_repositories.TryGetValue(typeof(T), out var cached))
            return (IGenericRepository<T>)cached;

        var repo = new GenericRepositoryImpl<T>(_context);
        _repositories[typeof(T)] = repo;
        return repo;
    }

    public bool HasActiveTransaction => _transaction is not null;

    public string? GetDatabaseProviderName()
        => (_context as DbContext)?.Database.ProviderName;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _txLock.WaitAsync(cancellationToken);
        try
        {
            if (_transaction is not null)
                throw new InvalidOperationException("An active transaction already exists.");

            if (_context is not DbContext dbContext)
                throw new InvalidOperationException("The context must inherit from DbContext to support transactions.");

            _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        finally
        {
            _txLock.Release();
        }
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task<OperationResult<bool>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction is not null)
                return OperationResult<bool>.Ok(true);

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult<bool>.Ok(true);
        }
        catch (DbUpdateConcurrencyException concEx)
        {
            return OperationResult<bool>.Fail("Concurrency conflict detected while saving changes.", concEx);
        }
        catch (DbUpdateException dbEx)
        {
            var entities = dbEx.Entries.Select(e => e.Entity.GetType().Name);
            var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
            var detail = GetDatabaseProviderName() switch
            {
                "Microsoft.EntityFrameworkCore.SqlServer" => $"SQL Server: {innerMessage}",
                "Npgsql.EntityFrameworkCore.PostgreSQL"   => $"PostgreSQL: {innerMessage}",
                _                                         => innerMessage
            };
            return OperationResult<bool>.Fail($"DbUpdateException ({string.Join(", ", entities)}): {detail}", dbEx);
        }
        catch (ValidationException valEx)
        {
            return OperationResult<bool>.Fail($"Validation failed: {valEx.Message}", valEx);
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail("Unexpected error while saving changes.", ex);
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisposeTransactionAsync();
        _txLock.Dispose();
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _transaction?.Dispose();
        _txLock.Dispose();
        _disposed = true;
    }
}
