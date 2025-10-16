using System.Linq.Expressions;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Implementation
{
    public class GenericRepositoryImpl<T> : IGenericRepository<T> where T : class
    {

        protected readonly IAppDbContext _context;
        protected readonly DbSet<T> _dbSet;

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
