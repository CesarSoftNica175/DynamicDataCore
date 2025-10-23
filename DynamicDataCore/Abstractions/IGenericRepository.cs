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
        /// Retrieves an entity by its primary key identifier.
        /// </summary>
        /// <param name="id">The entity identifier.</param>
        /// <param name="asNoTracking">If true, disables entity tracking for performance optimization.</param>
        Task<OperationResult<T?>> RetrieveByIdAsync(int id, bool asNoTracking = true);

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

        // Insert
        Task<OperationResult<bool>> AddAsync(T entity);

        Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities);

        // Update
        OperationResult<bool> Update(T entity);

        OperationResult<bool> UpdateList(IEnumerable<T> entities);

        // Delete
        OperationResult<bool> Delete(T entity);

        OperationResult<bool> DeleteList(IEnumerable<T> entities);

        // Persistence
        Task<OperationResult<int>> SaveChangesAsync();


        // Bulk Operations
        Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities);

        Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities);

        Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities);

    }
}