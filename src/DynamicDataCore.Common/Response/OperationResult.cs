using System;

namespace DynamicDataCore.Common.Response
{

    /// <summary>
    /// Description: Represents a standardized structure for operation results used across DynamicDataCore.
    /// <para></para>
    /// This generic class provides a unified way to return outcomes from service or repository operations,
    /// including success status, result data, exception details, and pagination metadata for lazy-loaded datasets.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    /// <typeparam name="T">Type of the data returned by the operation.</typeparam>
    public class OperationResult<T>
    {

        /// <summary>
        /// Description: Indicates whether the operation was executed successfully.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Description: Contains an informational or error message describing the operation result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Description: Unique identifier (TraceId) generated for correlation or debugging purposes.
        /// </summary>
        public string? TraceId { get; init; }

        /// <summary>
        /// Description: Represents the data returned by the operation if it succeeded.
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// Description: Contains the exception details if the operation failed.
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Description: Indicates whether the result set was obtained using lazy pagination.
        /// <para></para>
        /// When <c>true</c>, the Data property is expected to contain only a subset of records,
        /// and the <see cref="PaginationMetadata"/> property provides additional context.
        /// </summary>
        public bool IsPagedResult { get; init; }

        /// <summary>
        /// Description: Contains metadata about the pagination context associated with the data result.
        /// </summary>
        public PaginationMetadata? PaginationMetadata { get; init; }

        /// <summary>
        /// Description: Creates a successful operation result including optional data and message.
        /// <para></para>
        /// This method is commonly used for read or write operations that complete without exceptions.
        /// </summary>
        /// <param name="data">The data returned by the operation.</param>
        /// <param name="message">Optional message describing the operation result.</param>
        /// <param name="pagination">Optional pagination metadata for paginated results.</param>
        /// <param name="isPaged">Indicates whether the result is paginated (lazy loading).</param>
        /// <returns>A successful <see cref="OperationResult{T}"/> instance.</returns>
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

        /// <summary>
        /// Description: Creates a failed operation result containing an error message and optional exception.
        /// <para></para>
        /// This method automatically generates a new TraceId to help identify the failure instance.
        /// </summary>
        /// <param name="message">Error message describing the cause of the failure.</param>
        /// <param name="ex">Optional exception instance that caused the failure.</param>
        /// <returns>A failed <see cref="OperationResult{T}"/> instance with populated error details.</returns>
        public static OperationResult<T> Fail(string message, Exception? ex = null)
            => new()
            {
                Success = false,
                Message = message,
                Exception = ex,
                TraceId = Guid.NewGuid().ToString()
            };

    }
}