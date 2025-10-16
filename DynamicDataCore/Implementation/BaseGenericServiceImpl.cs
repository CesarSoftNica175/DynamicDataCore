using System.Linq.Expressions;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Implementation
{
    public class BaseGenericServiceImpl<T> : IBaseGenericService<T> where T : class
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<T> _repository;

        public BaseGenericServiceImpl(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _repository = _unitOfWork.Repository<T>();
        }

        public IUnitOfWork GetUnitOfWork() => _unitOfWork;

        // 🔹 Lectura
        public Task<OperationResult<T?>> RetrieveByIdAsync(int id)
            => _repository.RetrieveByIdAsync(id);

        public Task<OperationResult<IEnumerable<T>>> RetrieveAsync()
            => _repository.RetrieveAsync();

        public Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate)
            => _repository.RetrieveByFilterAsync(predicate);

        public async Task<OperationResult<List<T>>> RetrieveQueryableAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IQueryable<T>>? includes = null
        )
        {
            try
            {
                var query = _repository.RetrieveQueryable(predicate);
                if (includes != null) query = includes(query);
                return OperationResult<List<T>>.Ok(await query.ToListAsync());
            }
            catch (Exception ex)
            {
                return OperationResult<List<T>>.Fail("Error retrieving queryable list.", ex);
            }
        }

        // 🔹 Escritura
        public async Task<OperationResult<bool>> AddAsync(T entity)
        {
            var result = await _repository.AddAsync(entity);
            if (!result.Success) return result;
            return await CommitAsync();
        }

        public async Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities)
        {
            var result = await _repository.AddListAsync(entities);
            if (!result.Success) return result;
            return await CommitAsync();
        }

        public async Task<OperationResult<bool>> UpdateAsync(T entity)
        {
            var result = _repository.Update(entity);
            if (!result.Success) return result;
            return await CommitAsync();
        }

        public async Task<OperationResult<bool>> UpdateListAsync(IEnumerable<T> entities)
        {
            var result = _repository.UpdateList(entities);
            if (!result.Success) return result;
            return await CommitAsync();
        }

        public async Task<OperationResult<bool>> DeleteAsync(int id)
        {
            var entityResult = await _repository.RetrieveByIdAsync(id, asNoTracking: false);
            if (!entityResult.Success || entityResult.Data == null)
                return OperationResult<bool>.Fail("Entity not found.");

            var deleteResult = _repository.Delete(entityResult.Data);
            if (!deleteResult.Success) return deleteResult;

            return await CommitAsync();
        }

        public async Task<OperationResult<bool>> DeleteListAsync(IEnumerable<T> entities)
        {
            var result = _repository.DeleteList(entities);
            if (!result.Success) return result;
            return await CommitAsync();
        }

        // 🔹 Bulk Operations
        public Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities)
            => _repository.BulkInsertAsync(entities);

        public Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities)
            => _repository.BulkUpdateAsync(entities);

        public Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities)
            => _repository.BulkDeleteAsync(entities);

        private async Task<OperationResult<bool>> CommitAsync()
        {
            var saveResult = await _unitOfWork.SaveChangesAsync();
            return saveResult.Success
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Fail(saveResult.Message!, saveResult.Exception);
        }

    }
}
