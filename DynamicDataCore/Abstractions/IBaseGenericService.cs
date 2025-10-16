using System.Linq.Expressions;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions
{
    public interface IBaseGenericService<T> where T : class
    {

        IUnitOfWork GetUnitOfWork();

        // 🔹 Lecturas
        Task<OperationResult<T?>> RetrieveByIdAsync(int id);

        Task<OperationResult<IEnumerable<T>>> RetrieveAsync();

        Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate);

        Task<OperationResult<List<T>>> RetrieveQueryableAsync(Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IQueryable<T>>? includes = null);

        // 🔹 Escrituras
        Task<OperationResult<bool>> AddAsync(T entity);

        Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities);

        Task<OperationResult<bool>> UpdateAsync(T entity);

        Task<OperationResult<bool>> UpdateListAsync(IEnumerable<T> entities);

        Task<OperationResult<bool>> DeleteAsync(int id);

        Task<OperationResult<bool>> DeleteListAsync(IEnumerable<T> entities);

        // 🔹 Operaciones masivas
        Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities);

        Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities);

        Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities);

    }
}
