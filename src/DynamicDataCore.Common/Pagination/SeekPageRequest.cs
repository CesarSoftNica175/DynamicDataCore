namespace DynamicDataCore.Common.Pagination;

/// <summary>
/// Parameters for Last-Seen-ID (seek) pagination.
/// Client sends the last ID seen; server returns the next page starting after that ID.
/// </summary>
/// <typeparam name="TKey">Comparable key type used as the seek anchor.</typeparam>
public sealed record SeekPageRequest<TKey>(
    TKey? LastSeenId,
    int PerPage = 30,
    SortDirection Direction = SortDirection.Ascending);
