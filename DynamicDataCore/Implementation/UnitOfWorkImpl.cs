using System.ComponentModel.DataAnnotations;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DynamicDataCore.Implementation
{
    public class UnitOfWorkImpl(IAppDbContext context) : IUnitOfWork, IDisposable
    {

        private readonly IAppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private IDbContextTransaction? _transaction;
        private bool _disposed;
        private readonly Dictionary<Type, object> _repositories = [];

        public IGenericRepository<T> Repository<T>() where T : class
        {
            if (_repositories.ContainsKey(typeof(T)))
                return (IGenericRepository<T>)_repositories[typeof(T)];

            var repo = new GenericRepositoryImpl<T>(_context);
            _repositories.Add(typeof(T), repo);
            return repo;
        }

        public bool HasActiveTransaction => _transaction != null;

        public async Task BeginTransactionAsync()
        {
            if (_transaction != null)
                throw new InvalidOperationException("Ya existe una transacción activa.");

            if (_context is DbContext dbContext)
            {
                // EF Core solo permite un parámetro: IsolationLevel
                _transaction = await dbContext.Database.BeginTransactionAsync();
            }
            else
            {
                throw new InvalidOperationException("No se pudo iniciar la transacción. El contexto no es DbContext.");
            }
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
                throw new InvalidOperationException("No existe una transacción activa.");

            await _context.SaveChangesAsync();
            await DisposeTransactionAsync();
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null) return;

            await _transaction.RollbackAsync();
            await DisposeTransactionAsync();
        }

        public string? GetDatabaseProviderName()
            => (_context as DbContext)?.Database.ProviderName;

        public async Task<OperationResult<bool>> SaveChangesAsync()
        {
            try
            {
                // No ejecutar si hay transacción activa, se confirmará al final
                if (_transaction != null)
                    return OperationResult<bool>.Ok(true);

                await _context.SaveChangesAsync();
                return OperationResult<bool>.Ok(true);
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                return OperationResult<bool>.Fail("Error de concurrencia detectado.", concEx);
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
                return OperationResult<bool>.Fail("Error al guardar los cambios.", ex);
            }
        }

        private async Task DisposeTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _transaction?.Dispose();
            (_context as IDisposable)?.Dispose();
            _disposed = true;
        }

    }
}
