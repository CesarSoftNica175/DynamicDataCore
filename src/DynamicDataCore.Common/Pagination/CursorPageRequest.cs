namespace DynamicDataCore.Common.Pagination;

/// <summary>
/// Parameters for opaque-cursor pagination.
/// The cursor encodes ordering state (base64) and is returned in <see cref="PagedResult{T}.NextCursor"/>.
/// </summary>
public sealed record CursorPageRequest(string? Cursor, int PerPage = 30);
