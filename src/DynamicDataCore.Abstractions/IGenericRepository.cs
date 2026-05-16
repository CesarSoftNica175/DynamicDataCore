using System.Linq.Expressions;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Abstractions;

/// <summary>Generic repository contract for CRUD, pagination, streaming, and bulk operations.</summary>
/// <typeparam name="T">Entity type.</typeparam>
public interface IGenericRepository<T> where T : class
{
    // ── Retrieval ────────────────────────────────────────────────────────────

    Task<OperationResult<T?>> RetrieveByIdAsync(object id, bool asNoTracking = true, CancellationToken cancellationToken = default);

    Task<OperationResult<IEnumerable<T>>> RetrieveAsync(bool asNoTracking = true, CancellationToken cancellationToken = default);

    Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true, CancellationToken cancellationToken = default);

    IQueryable<T> RetrieveQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true);

    /// <summary>Streams entities lazily without materializing the full result set.</summary>
    IAsyncEnumerable<T> StreamAsync(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true, CancellationToken cancellationToken = default);

    // ── Pagination ───────────────────────────────────────────────────────────

    /// <summary>Offset pagination (Skip/Take). Prefer <c>RetrievePagedByOffsetAsync</c> which requires an explicit orderBy for deterministic results.</summary>
    [Obsolete("Prefer RetrievePagedByOffsetAsync with explicit orderBy. Will be removed in v3.0.")]
    Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(int page = 1, int perPage = 30, Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true, CancellationToken cancellationToken = default);

    /// <summary>Offset pagination with mandatory ordering for deterministic results.</summary>
    Task<PagedResult<T>> RetrievePagedByOffsetAsync<TKey>(
        OffsetPageRequest request,
        Expression<Func<T, TKey>> orderBy,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keyset pagination: fetches the next page using a sortable key comparison (&gt; or &lt;).
    /// Much more efficient than OFFSET on large tables.
    /// </summary>
    Task<PagedResult<T>> RetrievePagedByKeysetAsync<TKey>(
        KeysetPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>;

    /// <summary>
    /// Last-Seen-ID (seek) pagination. Client provides the last ID seen; server returns rows after it.
    /// Equivalent to keyset on the primary key — optimised for id-ordered feeds.
    /// </summary>
    Task<PagedResult<T>> RetrievePagedBySeekAsync<TKey>(
        SeekPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>;

    /// <summary>
    /// Opaque-cursor pagination. Returns an encoded <see cref="PagedResult{T}.NextCursor"/> for the next page.
    /// Deterministic and stable even under concurrent inserts.
    /// </summary>
    Task<PagedResult<T>> RetrievePagedByCursorAsync<TKey>(
        CursorPageRequest request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>;

    // ── Mutations ────────────────────────────────────────────────────────────

    Task<OperationResult<bool>> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    OperationResult<bool> Update(T entity);
    OperationResult<bool> UpdateList(IEnumerable<T> entities);
    OperationResult<bool> Delete(T entity);
    OperationResult<bool> DeleteList(IEnumerable<T> entities);
    Task<OperationResult<int>> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ── Bulk Operations ──────────────────────────────────────────────────────

    Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default);
    Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default);
    Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default);
}
