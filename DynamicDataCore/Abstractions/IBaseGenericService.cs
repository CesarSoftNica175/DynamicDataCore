using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions
{

    /// <summary>
    /// Description: Defines the contract for a generic base service that provides 
    /// CRUD operations, transactional support, and pagination capabilities 
    /// for any entity type within the DynamicDataCore framework.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    /// <typeparam name="T">Represents the entity type associated with the service.</typeparam>
    public interface IBaseGenericService<T> where T : class
    {

        /// <summary>
        /// Description: Retrieves the underlying <see cref="IUnitOfWork"/> instance 
        /// used by this service to coordinate repository and transaction operations.
        /// </summary>
        /// <returns>The <see cref="IUnitOfWork"/> instance in use.</returns>
        IUnitOfWork GetUnitOfWork();

        /// <summary>
        /// Description: Retrieves an entity by its primary key identifier asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to retrieve.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the entity if found, otherwise null.</returns>
        Task<OperationResult<T?>> RetrieveByIdAsync(int id);

        /// <summary>
        /// Description: Retrieves all entities of type <typeparamref name="T"/> asynchronously.
        /// </summary>
        /// <returns>An <see cref="OperationResult{T}"/> containing the list of entities.</returns>
        Task<OperationResult<IEnumerable<T>>> RetrieveAsync();

        /// <summary>
        /// Description: Retrieves entities that satisfy the specified filter expression asynchronously.
        /// </summary>
        /// <param name="predicate">A filter condition expressed as a LINQ expression.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the filtered entities.</returns>
        Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Description: Retrieves entities as a queryable list with optional filters and 
        /// include expressions for eager loading related entities.
        /// </summary>
        /// <param name="predicate">Optional filter expression for conditional retrieval.</param>
        /// <param name="includes">Optional function to include navigation properties.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the result as a list.</returns>
        Task<OperationResult<List<T>>> RetrieveQueryableAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IQueryable<T>>? includes = null
        );

        /// <summary>
        /// Description: Retrieves a paginated list of entities asynchronously, supporting lazy data loading.
        /// </summary>
        /// <param name="page">The current page number (default is 1).</param>
        /// <param name="perPage">The number of records per page (default is 30).</param>
        /// <param name="predicate">Optional filter expression to limit results.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> containing a collection of entities 
        /// and pagination metadata via <see cref="PaginationMetadata"/>.
        /// </returns>
        Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(
            int page = 1,
            int perPage = 30,
            Expression<Func<T, bool>>? predicate = null
        );

        /// <summary>
        /// Description: Adds a single entity asynchronously.
        /// </summary>
        /// <param name="entity">The entity instance to add.</param>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        Task<OperationResult<bool>> AddAsync(T entity);

        /// <summary>
        /// Description: Adds a collection of entities asynchronously.
        /// </summary>
        /// <param name="entities">The collection of entities to add.</param>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities);

        /// <summary>
        /// Description: Updates a single entity asynchronously.
        /// </summary>
        /// <param name="entity">The entity instance to update.</param>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        Task<OperationResult<bool>> UpdateAsync(T entity);

        /// <summary>
        /// Description: Updates a collection of entities asynchronously.
        /// </summary>
        /// <param name="entities">The entities to update.</param>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        Task<OperationResult<bool>> UpdateListAsync(IEnumerable<T> entities);

        /// <summary>
        /// Description: Deletes an entity based on its unique identifier asynchronously.
        /// </summary>
        /// <param name="id">The identifier of the entity to delete.</param>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        Task<OperationResult<bool>> DeleteAsync(int id);

        /// <summary>
        /// Description: Deletes a collection of entities asynchronously.
        /// </summary>
        /// <param name="entities">The collection of entities to delete.</param>
        /// <returns>An <see cref="OperationResult{T}"/> indicating success or failure.</returns>
        Task<OperationResult<bool>> DeleteListAsync(IEnumerable<T> entities);

        /// <summary>
        /// Description: Performs a bulk insert operation for a collection of entities asynchronously.
        /// </summary>
        /// <param name="entities">The entities to insert.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the number of affected records.</returns>
        Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities);

        /// <summary>
        /// Description: Performs a bulk update operation for a collection of entities asynchronously.
        /// </summary>
        /// <param name="entities">The entities to update.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the number of affected records.</returns>
        Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities);

        /// <summary>
        /// Description: Performs a bulk delete operation for a collection of entities asynchronously.
        /// </summary>
        /// <param name="entities">The entities to delete.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the number of affected records.</returns>
        Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities);

    }
}
