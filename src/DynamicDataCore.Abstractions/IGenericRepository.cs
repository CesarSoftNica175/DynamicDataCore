using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions
{

    /// <summary>
    /// Description: Defines a generic repository contract for performing CRUD and query operations on entities.
    /// <para></para>
    /// This abstraction enables separation of persistence logic from business layers, supporting reusable
    /// and testable data access with optional pagination and tracking behaviors.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    /// <typeparam name="T">The entity type handled by the repository.</typeparam>
    public interface IGenericRepository<T> where T : class
    {

        // Retrieval Operations

        /// <summary>
        /// Retrieves an entity by its primary key value.
        /// Supports any key type (int, Guid, string, etc.).
        /// </summary>
        /// <param name="id">The entity identifier.</param>
        /// <param name="asNoTracking">If true, disables entity tracking for performance optimization.</param>
        Task<OperationResult<T?>> RetrieveByIdAsync(object id, bool asNoTracking = true);

        /// <summary>
        /// Retrieves all entities from the underlying data source.
        /// </summary>
        /// <param name="asNoTracking">If true, disables entity tracking for better read performance.</param>
        Task<OperationResult<IEnumerable<T>>> RetrieveAsync(bool asNoTracking = true);

        /// <summary>
        /// Retrieves entities that match the provided filter predicate.
        /// </summary>
        /// <param name="predicate">The filter expression used to query the dataset.</param>
        /// <param name="asNoTracking">If true, disables tracking to improve read performance.</param>
        Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true);

        /// <summary>
        /// Retrieves a queryable collection for advanced LINQ composition or projections.
        /// </summary>
        /// <param name="predicate">Optional predicate to filter entities.</param>
        /// <param name="asNoTracking">Determines whether entities are tracked by the DbContext.</param>
        IQueryable<T> RetrieveQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true);

        /// <summary>
        /// Retrieves entities in a paginated (lazy) manner with optional filtering.
        /// </summary>
        /// <param name="page">The page number to retrieve.</param>
        /// <param name="perPage">The number of items per page.</param>
        /// <param name="predicate">Optional filter predicate for the dataset.</param>
        /// <param name="asNoTracking">Specifies whether EF tracking should be disabled.</param>
        Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(int page = 1, int perPage = 30, Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true);

        /// <summary>
        /// Asynchronously adds the specified entity to the data store.
        /// </summary>
        /// <param name="entity">The entity to add. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an OperationResult indicating
        /// whether the entity was successfully added.</returns>
        Task<OperationResult<bool>> AddAsync(T entity);

        /// <summary>
        /// Asynchronously adds a collection of entities to the data store.
        /// </summary>
        /// <param name="entities">The collection of entities to add. Cannot be null. Each entity in the collection will be added to the data
        /// store.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an OperationResult indicating
        /// whether the entities were successfully added.</returns>
        Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities);

        /// <summary>
        /// Updates the specified entity in the data store.
        /// </summary>
        /// <param name="entity">The entity to update. Cannot be null.</param>
        /// <returns>An OperationResult containing a value that is <see langword="true"/> if the update was successful;
        /// otherwise, <see langword="false"/>. The result may also include error information if the operation fails.</returns>
        OperationResult<bool> Update(T entity);

        /// <summary>
        /// Updates the specified collection of entities in the data store.
        /// </summary>
        /// <param name="entities">The collection of entities to update. Cannot be null. Each entity in the collection must have a valid
        /// identifier.</param>
        /// <returns>An OperationResult object containing a Boolean value that is <see langword="true"/> if all entities were
        /// successfully updated; otherwise, <see langword="false"/>. The result may also include error information if
        /// the update fails.</returns>
        OperationResult<bool> UpdateList(IEnumerable<T> entities);

        /// <summary>
        /// Deletes the specified entity from the data store.
        /// </summary>
        /// <param name="entity">The entity to be deleted. Cannot be null.</param>
        /// <returns>An OperationResult containing a value of <see langword="true"/> if the entity was successfully deleted;
        /// otherwise, <see langword="false"/>. The result also includes information about the success or failure of the
        /// operation.</returns>
        OperationResult<bool> Delete(T entity);

        /// <summary>
        /// Deletes a collection of entities from the data store.
        /// </summary>
        /// <param name="entities">The collection of entities to delete. Cannot be null. Each entity in the collection must not be null.</param>
        /// <returns>An OperationResult containing a value of <see langword="true"/> if all entities were successfully deleted;
        /// otherwise, <see langword="false"/>. The OperationResult also includes information about the success or
        /// failure of the operation.</returns>
        OperationResult<bool> DeleteList(IEnumerable<T> entities);

        /// <summary>
        /// Asynchronously saves all pending changes to the underlying data store.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation. The task result contains an <see
        /// cref="OperationResult{T}"/> with the number of state entries written to the data store.</returns>
        Task<OperationResult<int>> SaveChangesAsync();

        /// <summary>
        /// Asynchronously inserts a collection of entities into the data store in a single bulk operation.
        /// </summary>
        /// <param name="entities">The collection of entities to insert. Cannot be null or contain null elements.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an OperationResult holding the
        /// number of entities successfully inserted.</returns>
        Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities);

        /// <summary>
        /// Asynchronously updates a collection of entities in bulk.
        /// </summary>
        /// <param name="entities">The collection of entities to update. Cannot be null. Each entity in the collection will be updated in the
        /// data store.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an OperationResult holding the
        /// number of entities successfully updated.</returns>
        Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities);

        /// <summary>
        /// Deletes a collection of entities from the data store asynchronously in a single bulk operation.
        /// </summary>
        /// <param name="entities">The collection of entities to delete. Cannot be null or contain null elements.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an OperationResult with the
        /// number of entities successfully deleted.</returns>
        Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities);

    }
}