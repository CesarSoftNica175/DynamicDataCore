using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions
{
    public interface IUnitOfWork : IDisposable
    {

        IGenericRepository<T> Repository<T>() where T : class;

        // Proveedor
        string? GetDatabaseProviderName();

        // Transacciones
        Task BeginTransactionAsync();

        Task CommitTransactionAsync();

        Task RollbackTransactionAsync();

        // Guardar cambios
        Task<OperationResult<bool>> SaveChangesAsync();

        /// <summary>
        /// Indica si existe una transacción activa.
        /// </summary>
        bool HasActiveTransaction { get; }

    }
}
