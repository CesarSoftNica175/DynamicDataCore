using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Implementation
{

    /// <summary>
    /// Description: Provides the default Entity Framework implementation of the <see cref="IGenericRepository{T}"/>.
    /// <para></para>
    /// This class encapsulates standard CRUD operations, LINQ-based queries, and paginated (lazy) retrievals
    /// while maintaining consistent error handling and structured operation results.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    /// <typeparam name="T">Entity type managed by this repository implementation.</typeparam>
    public class GenericRepositoryImpl<T> : IGenericRepository<T> where T : class
    {

        protected readonly IAppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericRepositoryImpl{T}"/> class.
        /// </summary>
        /// <param name="context">Database context implementing <see cref="IAppDbContext"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided context is null.</exception>
        public GenericRepositoryImpl(IAppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }

        // 🔹 Recuperación
        public async Task<OperationResult<T?>> RetrieveByIdAsync(int id, bool asNoTracking = true)
        {
            try
            {
                var entity = await _dbSet.FindAsync(id);
                if (entity == null)
                    return OperationResult<T?>.Fail($"Entity with id {id} not found.");

                if (asNoTracking)
                    _context.Set<T>().Entry(entity).State = EntityState.Detached;

                return OperationResult<T?>.Ok(entity);
            }
            catch (Exception ex)
            {
                return OperationResult<T?>.Fail("Error retrieving entity by id.", ex);
            }
        }

        public async Task<OperationResult<IEnumerable<T>>> RetrieveAsync(bool asNoTracking = true)
        {
            try
            {
                var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
                var list = await query.ToListAsync();
                return OperationResult<IEnumerable<T>>.Ok(list);
            }
            catch (Exception ex)
            {
                return OperationResult<IEnumerable<T>>.Fail("Error retrieving entities.", ex);
            }
        }

        public async Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true)
        {
            try
            {
                var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
                var filtered = await query.Where(predicate).ToListAsync();
                return OperationResult<IEnumerable<T>>.Ok(filtered);
            }
            catch (Exception ex)
            {
                return OperationResult<IEnumerable<T>>.Fail("Error filtering entities.", ex);
            }
        }

        public IQueryable<T> RetrieveQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true)
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
            return predicate != null ? query.Where(predicate) : query;
        }

        /// <summary>
        /// Retrieves a paginated (lazy) collection of entities with optional filtering.
        /// </summary>
        /// <param name="page">The current page number (default = 1).</param>
        /// <param name="perPage">Number of entities per page (default = 30).</param>
        /// <param name="predicate">Optional LINQ predicate to filter results.</param>
        /// <param name="asNoTracking">Indicates whether EF tracking is disabled for query optimization.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing a paginated dataset and metadata.</returns>
        public async Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(
            int page = 1,
            int perPage = 30,
            Expression<Func<T, bool>>? predicate = null,
            bool asNoTracking = true)
        {
            try
            {
                var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet;

                if (predicate != null)
                    query = query.Where(predicate);

                var total = await query.CountAsync();
                var skip = (page - 1) * perPage;
                var data = await query.Skip(skip).Take(perPage).ToListAsync();

                var pagination = new PaginationMetadata
                {
                    CurrentPage = page,
                    PerPage = perPage,
                    Total = total,
                    LastPage = (int)Math.Ceiling(total / (double)perPage),
                    From = skip + 1,
                    To = skip + data.Count
                };

                return OperationResult<IEnumerable<T>>.Ok(
                    data,
                    message: "Paged retrieval successful.",
                    pagination: pagination,
                    isPaged: true
                );
            }
            catch (Exception ex)
            {
                return OperationResult<IEnumerable<T>>.Fail("Error retrieving paginated entities.", ex);
            }
        }

        // 🔹 Inserción
        public async Task<OperationResult<bool>> AddAsync(T entity)
        {
            try
            {
                await _dbSet.AddAsync(entity);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error adding entity.", ex);
            }
        }

        public async Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities)
        {
            try
            {
                await _dbSet.AddRangeAsync(entities);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error adding entities.", ex);
            }
        }

        // 🔹 Actualización
        public OperationResult<bool> Update(T entity)
        {
            try
            {
                _dbSet.Update(entity);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error updating entity.", ex);
            }
        }

        public OperationResult<bool> UpdateList(IEnumerable<T> entities)
        {
            try
            {
                _dbSet.UpdateRange(entities);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error updating entities.", ex);
            }
        }

        // 🔹 Eliminación
        public OperationResult<bool> Delete(T entity)
        {
            try
            {
                _dbSet.Remove(entity);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error deleting entity.", ex);
            }
        }

        public OperationResult<bool> DeleteList(IEnumerable<T> entities)
        {
            try
            {
                _dbSet.RemoveRange(entities);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error deleting entities.", ex);
            }
        }

        // 🔹 Persistencia
        public async Task<OperationResult<int>> SaveChangesAsync()
        {
            try
            {
                var result = await _context.SaveChangesAsync();
                return OperationResult<int>.Ok(result);
            }
            catch (Exception ex)
            {
                return OperationResult<int>.Fail("Error saving changes.", ex);
            }
        }

        // 🔹 Bulk Operations (placeholder)
        public Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities)
            => Task.FromResult(OperationResult<int>.Fail("BulkInsert not implemented."));

        public Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities)
            => Task.FromResult(OperationResult<int>.Fail("BulkUpdate not implemented."));

        public Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities)
            => Task.FromResult(OperationResult<int>.Fail("BulkDelete not implemented."));

    }
}