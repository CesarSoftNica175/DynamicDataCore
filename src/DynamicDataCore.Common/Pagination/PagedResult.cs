using DynamicDataCore.Common.Response;

namespace DynamicDataCore.Common.Pagination;

/// <summary>Unified result type returned by all pagination strategies.</summary>
/// <typeparam name="T">Entity type.</typeparam>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required PaginationMetadata Metadata { get; init; }

    /// <summary>Opaque cursor for the next page. Null when there are no more pages.</summary>
    public string? NextCursor { get; init; }

    /// <summary>True when there is at least one more page beyond the current result.</summary>
    public bool HasNextPage => NextCursor is not null;
}
