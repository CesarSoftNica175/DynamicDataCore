using System.Linq.Expressions;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions;

/// <summary>Generic service contract for CRUD, pagination, and bulk operations on a single entity type.</summary>
/// <typeparam name="T">Entity type.</typeparam>
public interface IBaseGenericService<T> where T : class
{
    IUnitOfWork GetUnitOfWork();

    // ── Retrieval ────────────────────────────────────────────────────────────

    Task<OperationResult<T?>> RetrieveByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<OperationResult<IEnumerable<T>>> RetrieveAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<OperationResult<List<T>>> RetrieveQueryableAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IQueryable<T>>? includes = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streams entities without buffering the full result set in memory.</summary>
    IAsyncEnumerable<T> StreamAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    // ── Pagination ───────────────────────────────────────────────────────────

    [Obsolete("Prefer RetrievePagedByOffsetAsync with explicit orderBy. Will be removed in v3.0.")]
    Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(int page = 1, int perPage = 30, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task<PagedResult<T>> RetrievePagedByOffsetAsync<TKey>(
        OffsetPageRequest request,
        Expression<Func<T, TKey>> orderBy,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<T>> RetrievePagedByKeysetAsync<TKey>(
        KeysetPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>;

    Task<PagedResult<T>> RetrievePagedBySeekAsync<TKey>(
        SeekPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>;

    Task<PagedResult<T>> RetrievePagedByCursorAsync<TKey>(
        CursorPageRequest request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>;

    // ── Mutations ────────────────────────────────────────────────────────────

    Task<OperationResult<bool>> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> UpdateListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> DeleteAsync(object id, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> DeleteListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    // ── Bulk Operations ──────────────────────────────────────────────────────

    Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default);
    Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default);
    Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default);
}
