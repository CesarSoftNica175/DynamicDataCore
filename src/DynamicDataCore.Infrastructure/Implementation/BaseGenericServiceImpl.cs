using System.Linq.Expressions;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Infrastructure.Implementation;

/// <summary>
/// Default service layer over <see cref="IUnitOfWork"/> providing CRUD, pagination, streaming,
/// and bulk operations for a single entity type.
/// </summary>
public sealed class BaseGenericServiceImpl<T> : IBaseGenericService<T> where T : class
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<T> _repository;

    public BaseGenericServiceImpl(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _repository = _unitOfWork.Repository<T>();
    }

    public IUnitOfWork GetUnitOfWork() => _unitOfWork;

    // ── Retrieval ────────────────────────────────────────────────────────────

    public Task<OperationResult<T?>> RetrieveByIdAsync(object id, CancellationToken cancellationToken = default)
        => _repository.RetrieveByIdAsync(id, cancellationToken: cancellationToken);

    public Task<OperationResult<IEnumerable<T>>> RetrieveAsync(CancellationToken cancellationToken = default)
        => _repository.RetrieveAsync(cancellationToken: cancellationToken);

    public Task<OperationResult<IEnumerable<T>>> RetrieveByFilterAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => _repository.RetrieveByFilterAsync(predicate, cancellationToken: cancellationToken);

    public async Task<OperationResult<List<T>>> RetrieveQueryableAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IQueryable<T>>? includes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _repository.RetrieveQueryable(predicate);
            if (includes is not null) query = includes(query);
            return OperationResult<List<T>>.Ok(await query.ToListAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return OperationResult<List<T>>.Fail("Error retrieving queryable list.", ex);
        }
    }

    public IAsyncEnumerable<T> StreamAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => _repository.StreamAsync(predicate, cancellationToken: cancellationToken);

    // ── Pagination ───────────────────────────────────────────────────────────

#pragma warning disable CS0618
    [Obsolete("Prefer RetrievePagedByOffsetAsync with explicit orderBy.")]
    public Task<OperationResult<IEnumerable<T>>> RetrievePagedAsync(
        int page = 1, int perPage = 30,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        => _repository.RetrievePagedAsync(page, perPage, predicate, cancellationToken: cancellationToken);
#pragma warning restore CS0618

    public Task<PagedResult<T>> RetrievePagedByOffsetAsync<TKey>(
        OffsetPageRequest request,
        Expression<Func<T, TKey>> orderBy,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        => _repository.RetrievePagedByOffsetAsync(request, orderBy, predicate, cancellationToken: cancellationToken);

    public Task<PagedResult<T>> RetrievePagedByKeysetAsync<TKey>(
        KeysetPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>
        => _repository.RetrievePagedByKeysetAsync(request, keySelector, predicate, cancellationToken: cancellationToken);

    public Task<PagedResult<T>> RetrievePagedBySeekAsync<TKey>(
        SeekPageRequest<TKey> request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>
        => _repository.RetrievePagedBySeekAsync(request, keySelector, predicate, cancellationToken: cancellationToken);

    public Task<PagedResult<T>> RetrievePagedByCursorAsync<TKey>(
        CursorPageRequest request,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TKey : IComparable<TKey>
        => _repository.RetrievePagedByCursorAsync(request, keySelector, predicate, cancellationToken: cancellationToken);

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<OperationResult<bool>> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var result = await _repository.AddAsync(entity, cancellationToken);
        return result.Success ? await CommitAsync(cancellationToken) : result;
    }

    public async Task<OperationResult<bool>> AddListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var result = await _repository.AddListAsync(entities, cancellationToken);
        return result.Success ? await CommitAsync(cancellationToken) : result;
    }

    public async Task<OperationResult<bool>> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var result = _repository.Update(entity);
        return result.Success ? await CommitAsync(cancellationToken) : result;
    }

    public async Task<OperationResult<bool>> UpdateListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var result = _repository.UpdateList(entities);
        return result.Success ? await CommitAsync(cancellationToken) : result;
    }

    public async Task<OperationResult<bool>> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var entityResult = await _repository.RetrieveByIdAsync(id, asNoTracking: false, cancellationToken: cancellationToken);
        if (!entityResult.Success || entityResult.Data is null)
            return OperationResult<bool>.Fail("Entity not found.");

        var deleteResult = _repository.Delete(entityResult.Data);
        return deleteResult.Success ? await CommitAsync(cancellationToken) : deleteResult;
    }

    public async Task<OperationResult<bool>> DeleteListAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var result = _repository.DeleteList(entities);
        return result.Success ? await CommitAsync(cancellationToken) : result;
    }

    // ── Bulk Operations ──────────────────────────────────────────────────────

    public Task<OperationResult<int>> BulkInsertAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default)
        => _repository.BulkInsertAsync(entities, batchSize, cancellationToken);

    public Task<OperationResult<int>> BulkUpdateAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default)
        => _repository.BulkUpdateAsync(entities, batchSize, cancellationToken);

    public Task<OperationResult<int>> BulkDeleteAsync(IEnumerable<T> entities, int batchSize = 500, CancellationToken cancellationToken = default)
        => _repository.BulkDeleteAsync(entities, batchSize, cancellationToken);

    private async Task<OperationResult<bool>> CommitAsync(CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result.Success
            ? OperationResult<bool>.Ok(true)
            : OperationResult<bool>.Fail(result.Message!, result.Exception);
    }
}
