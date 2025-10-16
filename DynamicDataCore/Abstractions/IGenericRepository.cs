using System.Linq.Expressions;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions
{
    public interface IGenericRepository<T> where T : class
    {

        // 🔹 Recuperación
        Task<OperationResult<T?>> RetrieveByIdAsync(int id, bool asNoTracking = true);

        Task<OperationResult<IEnumerable<T>>> RetrieveAsync(bool asNoTracking = true);

        Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true);

        IQueryable<T> RetrieveQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true);

        // 🔹 Inserción
        Task<OperationResult<bool>> AddAsync(T entity);

        Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities);

        // 🔹 Actualización
        OperationResult<bool> Update(T entity);

        OperationResult<bool> UpdateList(IEnumerable<T> entities);

        // 🔹 Eliminación
        OperationResult<bool> Delete(T entity);

        OperationResult<bool> DeleteList(IEnumerable<T> entities);

        // 🔹 Persistencia
        Task<OperationResult<int>> SaveChangesAsync();


        // 🔹 Operaciones masivas (bulk)
        Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities);

        Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities);

        Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities);

    }
}
