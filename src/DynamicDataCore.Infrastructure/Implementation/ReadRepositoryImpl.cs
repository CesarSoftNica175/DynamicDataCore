using System.Linq.Expressions;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Common.Response;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Infrastructure.Implementation;

/// <summary>
/// EF Core implementation of <see cref="IReadRepository{T}"/>. Every query is <c>AsNoTracking</c>; the type has no
/// write members. Map the entity to a view with <c>ModelBuilder.ConfigureReadOnlyView</c> (keyless + ToView).
/// </summary>
public sealed class ReadRepositoryImpl<T> : IReadRepository<T> where T : class
{
    private readonly IAppDbContext _context;

    /// <summary>Creates the repository over the registered context.</summary>
    public ReadRepositoryImpl(IAppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private IQueryable<T> Query(Expression<Func<T, bool>>? filter)
    {
        var query = _context.Set<T>().AsNoTracking();
        return filter is null ? query : query.Where(filter);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = Query(filter);
        if (orderBy is not null) query = orderBy(query);
        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = Query(filter);
        if (orderBy is not null) query = orderBy(query);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default)
        => Query(filter).CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default)
        => Query(filter).AnyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<T>> PageAsync(
        OffsetPageRequest request,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(orderBy);
        if (request.Page < 1) throw new ArgumentOutOfRangeException(nameof(request), "Page must be >= 1.");
        if (request.PerPage < 1) throw new ArgumentOutOfRangeException(nameof(request), "PerPage must be >= 1.");

        var query = Query(filter);
        var total = await query.CountAsync(cancellationToken);
        var skip = (request.Page - 1) * request.PerPage;
        var data = await orderBy(query).Skip(skip).Take(request.PerPage).ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = data,
            Metadata = new PaginationMetadata
            {
                CurrentPage = request.Page,
                PerPage = request.PerPage,
                Total = total,
                LastPage = (int)Math.Ceiling(total / (double)request.PerPage),
                From = data.Count == 0 ? 0 : skip + 1,
                To = skip + data.Count
            },
            // Offset paging has no key cursor: when more rows exist, this carries the next page number.
            NextCursor = skip + data.Count < total ? (request.Page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null
        };
    }
}
