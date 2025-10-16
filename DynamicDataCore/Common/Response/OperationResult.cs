namespace DynamicDataCore.Common.Response
{
    public class OperationResult<T>
    {

        public bool Success { get; init; }

        public string? Message { get; init; }

        public string? TraceId { get; init; }

        public T? Data { get; init; }

        public Exception? Exception { get; init; }

        public static OperationResult<T> Ok(T? data, string? message = null)
            => new() { Success = true, Data = data, Message = message, TraceId = Guid.NewGuid().ToString() };

        public static OperationResult<T> Fail(string message, Exception? ex = null)
            => new() { Success = false, Message = message, Exception = ex, TraceId = Guid.NewGuid().ToString() };

    }
}
