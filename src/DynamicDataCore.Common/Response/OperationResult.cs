namespace DynamicDataCore.Common.Response;

/// <summary>Standardized result envelope for all service and repository operations.</summary>
/// <typeparam name="T">Type of the data payload.</typeparam>
public sealed record OperationResult<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public T? Data { get; init; }
    public Exception? Exception { get; init; }
    public bool IsPagedResult { get; init; }
    public PaginationMetadata? PaginationMetadata { get; init; }

    public static OperationResult<T> Ok(
        T? data,
        string? message = null,
        PaginationMetadata? pagination = null,
        bool isPaged = false)
        => new()
        {
            Success = true,
            Data = data,
            Message = message,
            IsPagedResult = isPaged,
            PaginationMetadata = pagination
        };

    public static OperationResult<T> Fail(string message, Exception? ex = null)
        => new()
        {
            Success = false,
            Message = message,
            Exception = ex,
            TraceId = Guid.NewGuid().ToString()
        };
}
