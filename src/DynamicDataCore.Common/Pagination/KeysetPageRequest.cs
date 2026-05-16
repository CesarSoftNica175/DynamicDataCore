namespace DynamicDataCore.Common.Pagination;

/// <summary>
/// Parameters for keyset (seek) pagination using a sortable key column.
/// Eliminates OFFSET overhead; ideal for large, append-heavy tables.
/// </summary>
/// <typeparam name="TKey">Comparable key type (int, Guid, DateTimeOffset, etc.).</typeparam>
public sealed record KeysetPageRequest<TKey>(
    TKey? After,
    int PerPage = 30,
    SortDirection Direction = SortDirection.Ascending);
