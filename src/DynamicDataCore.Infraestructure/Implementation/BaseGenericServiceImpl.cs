using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Infraestructure.Implementation
{

    /// <summary>
    /// Description: Provides a base implementation of generic service operations for CRUD and bulk data management.
    /// <para>This class acts as an abstraction layer between repositories and higher-level business logic.</para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    /// <typeparam name="T">The entity type to operate on.</typeparam>
    public class BaseGenericServiceImpl<T> : IBaseGenericService<T> where T : class
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<T> _repository;

        /// <summary>
        /// Initializes a new instance of <see cref="BaseGenericServiceImpl{T}"/>.
        /// </summary>
        /// <param name="unitOfWork">The unit of work instance responsible for managing transactions and repositories.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="unitOfWork"/> is null.</exception>
        public BaseGenericServiceImpl(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _repository = _unitOfWork.Repository<T>();
        }

        /// <inheritdoc/>
        public IUnitOfWork GetUnitOfWork() => _unitOfWork;

        /// <inheritdoc/>
        public Task<OperationResult<T?>> RetrieveByIdAsync(object id)
            => _repository.RetrieveByIdAsync(id);

        /// <inheritdoc/>
        public Task<OperationResult<IEnumerable<T>>> RetrieveAsync()
            => _repository.RetrieveAsync();

        /// <inheritdoc/>
        public Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate)
            => _repository.RetrieveByFilterAsync(predicate);

        /// <inheritdoc/>
        public async Task<OperationResult<List<T>>> RetrieveQueryableAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IQueryable<T>>? includes = null
        )
        {
            try
            {
                IQueryable<T> query = _repository.RetrieveQueryable(predicate);
                if (includes != null)
                    query = includes(query);

                var list = await query.ToListAsync();
                return OperationResult<List<T>>.Ok(list);
            }
            catch (Exception ex)
            {
                return OperationResult<List<T>>.Fail("Error retrieving queryable list.", ex);
            }
        }

        /// <inheritdoc/>
        public Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(
            int page = 1,
            int perPage = 30,
            Expression<Func<T, bool>>? predicate = null
        )
            => _repository.RetrievePagedAsync(page, perPage, predicate);

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> AddAsync(T entity)
        {
            var result = await _repository.AddAsync(entity);
            return result.Success ? await CommitAsync() : result;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities)
        {
            var result = await _repository.AddListAsync(entities);
            return result.Success ? await CommitAsync() : result;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> UpdateAsync(T entity)
        {
            var result = _repository.Update(entity);
            return result.Success ? await CommitAsync() : result;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> UpdateListAsync(IEnumerable<T> entities)
        {
            var result = _repository.UpdateList(entities);
            return result.Success ? await CommitAsync() : result;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> DeleteAsync(object id)
        {
            var entityResult = await _repository.RetrieveByIdAsync(id, asNoTracking: false);

            if (!entityResult.Success || entityResult.Data == null)
                return OperationResult<bool>.Fail("Entity not found.");

            var deleteResult = _repository.Delete(entityResult.Data);

            return deleteResult.Success 
                ? await CommitAsync() 
                : deleteResult;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> DeleteListAsync(IEnumerable<T> entities)
        {
            var result = _repository.DeleteList(entities);
            return result.Success ? await CommitAsync() : result;
        }

        /// <inheritdoc/>
        public Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities)
            => _repository.BulkInsertAsync(entities);

        /// <inheritdoc/>
        public Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities)
            => _repository.BulkUpdateAsync(entities);

        /// <inheritdoc/>
        public Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities)
            => _repository.BulkDeleteAsync(entities);

        /// <summary>
        /// Commits all pending changes using the current <see cref="IUnitOfWork"/>.
        /// </summary>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        private async Task<OperationResult<bool>> CommitAsync()
        {
            var saveResult = await _unitOfWork.SaveChangesAsync();
            return saveResult.Success
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Fail(saveResult.Message!, saveResult.Exception);
        }

    }
}
