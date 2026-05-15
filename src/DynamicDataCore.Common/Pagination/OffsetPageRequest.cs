namespace DynamicDataCore.Common.Pagination;

/// <summary>Parameters for traditional offset-based (Skip/Take) pagination.</summary>
public sealed record OffsetPageRequest(int Page = 1, int PerPage = 30);
