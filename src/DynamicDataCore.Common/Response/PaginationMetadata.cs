namespace DynamicDataCore.Common.Response;

/// <summary>Metadata for offset-based paginated responses.</summary>
public sealed record PaginationMetadata
{
    public int CurrentPage { get; init; }
    public int From { get; init; }
    public int LastPage { get; init; }
    public int PerPage { get; init; }
    public int To { get; init; }
    public int Total { get; init; }
}
