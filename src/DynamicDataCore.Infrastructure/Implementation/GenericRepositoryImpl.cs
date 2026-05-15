using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Infrastructure.Implementation;

/// <summary>
/// EF Core implementation of <see cref="IGenericRepository{T}"/>.
/// Supports offset, keyset, seek, and cursor pagination strategies plus async streaming.
/// </summary>
public sealed class GenericRepositoryImpl<T> : IGenericRepository<T> where T : class
{
    private readonly IAppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepositoryImpl(IAppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    // ── Retrieval ────────────────────────────────────────────────────────────

    public async Task<OperationResult<T?>> RetrieveByIdAsync(object id, bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbSet.FindAsync([id], cancellationToken);
            if (entity is null)
                return OperationResult<T?>.Fail($"Entity with id '{id}' not found.");

            if (asNoTracking && _context is DbContext dbCtx)
                dbCtx.Entry(entity).State = EntityState.Detached;

            return OperationResult<T?>.Ok(entity);
        }
        catch (Exception ex)
        {
            return OperationResult<T?>.Fail("Error retrieving entity by id.", ex);
        }
    }

    public async Task<OperationResult<IEnumerable<T>>> RetrieveAsync(bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
            return OperationResult<IEnumerable<T>>.Ok(await query.ToListAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return OperationResult<IEnumerable<T>>.Fail("Error retrieving entities.", ex);
        }
    }

    public async Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
            return OperationResult<IEnumerable<T>>.Ok(await query.Where(predicate).ToListAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return OperationResult<IEnumerable<T>>.Fail("Error filtering entities.", ex);
        }
    }

    public IQueryable<T> RetrieveQueryable(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true)
    {
        var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
        return predicate is null ? query : query.Where(predicate);
    }

    public async IAsyncEnumerable<T> StreamAsync(
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
        if (predicate is not null) query = query.Where(predicate);

        await foreach (var item in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return item;
    }

    // ── Pagination ───────────────────────────────────────────────────────────

#pragma warning disable CS0618
    [Obsolete("Prefer RetrievePagedByOffsetAsync with explicit orderBy.")]
    public async Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(
        int page = 1, int perPage = 30,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
            if (predicate is not null) query = query.Where(predicate);

            var total = await query.CountAsync(cancellationToken);
            var skip = (page - 1) * perPage;
            var data = await query.Skip(skip).Take(perPage).ToListAsync(cancellationToken);

            var pagination = new PaginationMetadata
            {
                CurrentPage = page,
                PerPage = perPage,
                Total = total,
                LastPage = (int)Math.Ceiling(total / (double)perPage),
                From = skip + 1,
                To = skip + data.Count
            };

            return OperationResult<IEnumerable<T>>.Ok(data, "Paged retrieval successful.", pagination, isPaged: true);
        }
        catch (Exception ex)
        {
            return OperationResult<IEnumerable<T>>.Fail("Error retrieving paginated entities.", ex);
        }
    }
#pragma warning restore CS0618

    public async Task<PagedResult<T>> RetrievePagedByOffsetAsync<TKey>(
        OffsetPageRequest request,
        Expression<Func<T, TKey>> orderBy,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
        if (predicate is not null) query = query.Where(predicate);

        var total = await query.CountAsync(cancellationToken);
        var skip = (request.Page - 1) * request.PerPage;
        var data = await query.OrderBy(orderBy).Skip(skip).Take(request.PerPage).ToListAsync(cancellationToken);

        var lastKey = data.Count > 0 ? (object?)orderBy.Compile()(data[^1]) : null;

        return new PagedResult<T>
        {
            Items = data,
            Metadata = new PaginationMetadata
            {
                CurrentPage = request.Page,
                PerPage = request.PerPage,
                Total = total,
                LastPage = (int)Math.Ceiling(total / (double)request.PerPage),
                From = skip + 1,
                To = skip + data.Count
            },
            NextCursor = skip + data.Count < total ? EncodeCursor(lastKey) : null
        };
    }

    public async Task<PagedResult<T>> RetrievePagedByKeysetAsync<TKey>(
        KeysetPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>
    {
        var query = asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();
        if (predicate is not null) query = query.Where(predicate);

        if (request.After is not null)
        {
            var comparison = request.Direction == SortDirection.Ascending
                ? BuildComparison(keySelector, request.After, ExpressionType.GreaterThan)
                : BuildComparison(keySelector, request.After, ExpressionType.LessThan);
            query = query.Where(comparison);
        }

        query = request.Direction == SortDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);

        // Fetch one extra to detect hasNextPage without a COUNT query
        var data = await query.Take(request.PerPage + 1).ToListAsync(cancellationToken);
        var hasNext = data.Count > request.PerPage;
        if (hasNext) data.RemoveAt(data.Count - 1);

        var nextCursor = hasNext && data.Count > 0
            ? EncodeCursor(keySelector.Compile()(data[^1]))
            : null;

        return new PagedResult<T>
        {
            Items = data,
            Metadata = new PaginationMetadata { PerPage = request.PerPage, Total = -1 },
            NextCursor = nextCursor
        };
    }

    public async Task<PagedResult<T>> RetrievePagedBySeekAsync<TKey>(
        SeekPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>
    {
        var keysetRequest = new KeysetPageRequest<TKey>(
            After: request.LastSeenId,
            PerPage: request.PerPage,
            Direction: request.Direction);

        return await RetrievePagedByKeysetAsync(keysetRequest, keySelector, predicate, asNoTracking, cancellationToken);
    }

    public async Task<PagedResult<T>> RetrievePagedByCursorAsync<TKey>(
        CursorPageRequest request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>
    {
        var after = request.Cursor is null ? default : DecodeCursor<TKey>(request.Cursor);
        var keysetRequest = new KeysetPageRequest<TKey>(After: after, PerPage: request.PerPage);
        return await RetrievePagedByKeysetAsync(keysetRequest, keySelector, predicate, asNoTracking, cancellationToken);
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<OperationResult<bool>> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbSet.AddAsync(entity, cancellationToken);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail("Error adding entity.", ex);
        }
    }

    public async Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbSet.AddRangeAsync(entities, cancellationToken);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail("Error adding entities.", ex);
        }
    }

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

    public async Task<OperationResult<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return OperationResult<int>.Ok(await _context.SaveChangesAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return OperationResult<int>.Fail("Error saving changes.", ex);
        }
    }

    // ── Bulk Operations via Channel producer-consumer ─────────────────────────

    public Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default)
        => ProcessInBatchesAsync(entities, batch => _dbSet.AddRangeAsync(batch, cancellationToken), batchSize, cancellationToken);

    public Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default)
        => ProcessInBatchesAsync(entities, batch => { _dbSet.UpdateRange(batch); return Task.CompletedTask; }, batchSize, cancellationToken);

    public Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default)
        => ProcessInBatchesAsync(entities, batch => { _dbSet.RemoveRange(batch); return Task.CompletedTask; }, batchSize, cancellationToken);

    private async Task<OperationResult<int>> ProcessInBatchesAsync(
        IEnumerable<T> entities,
        Func<IReadOnlyList<T>, Task> batchAction,
        int batchSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var channel = Channel.CreateBounded<IReadOnlyList<T>>(
                new BoundedChannelOptions(4) { FullMode = BoundedChannelFullMode.Wait, SingleWriter = true, SingleReader = true });

            var producer = Task.Run(async () =>
            {
                var batch = new List<T>(batchSize);
                foreach (var entity in entities)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    batch.Add(entity);
                    if (batch.Count >= batchSize)
                    {
                        await channel.Writer.WriteAsync(batch.AsReadOnly(), cancellationToken);
                        batch = new List<T>(batchSize);
                    }
                }
                if (batch.Count > 0)
                    await channel.Writer.WriteAsync(batch.AsReadOnly(), cancellationToken);
                channel.Writer.Complete();
            }, cancellationToken);

            var count = 0;
            await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await batchAction(batch);
                await _context.SaveChangesAsync(cancellationToken);
                count += batch.Count;
            }

            await producer;
            return OperationResult<int>.Ok(count);
        }
        catch (Exception ex)
        {
            return OperationResult<int>.Fail("Bulk operation failed.", ex);
        }
    }

    // ── Expression helpers ───────────────────────────────────────────────────

    private static Expression<Func<T, bool>> BuildComparison<TKey>(
        Expression<Func<T, TKey>> keySelector,
        TKey pivot,
        ExpressionType comparison)
    {
        var param = keySelector.Parameters[0];
        var body = Expression.MakeBinary(comparison, keySelector.Body, Expression.Constant(pivot, typeof(TKey)));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static string EncodeCursor<TValue>(TValue value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));

    private static TValue? DecodeCursor<TValue>(string cursor)
    {
        try
        {
            return JsonSerializer.Deserialize<TValue>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
        }
        catch
        {
            return default;
        }
    }
}
