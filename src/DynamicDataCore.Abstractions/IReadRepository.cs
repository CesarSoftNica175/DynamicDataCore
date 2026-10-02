using System.Linq.Expressions;
using DynamicDataCore.Common.Pagination;

namespace DynamicDataCore.Abstractions;

/// <summary>
/// Read-only access to keyless entities (typically SQL views). Always no-tracking; there are no write members.
/// </summary>
/// <typeparam name="T">Keyless entity mapped to a view.</typeparam>
public interface IReadRepository<T> where T : class
{
    /// <summary>Lists rows matching <paramref name="filter"/>, optionally ordered.</summary>
    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>First row matching <paramref name="filter"/> (ordered when <paramref name="orderBy"/> is given), or null.</summary>
    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts rows matching <paramref name="filter"/>.</summary>
    Task<int> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>True when at least one row matches <paramref name="filter"/>.</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>Offset pagination; <paramref name="orderBy"/> is mandatory so pages are deterministic.</summary>
    Task<PagedResult<T>> PageAsync(
        OffsetPageRequest request,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default);
}
