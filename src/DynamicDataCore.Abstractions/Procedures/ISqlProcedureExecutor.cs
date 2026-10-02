namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>
/// Runs stored procedures only. There is intentionally no overload that accepts SQL text: the procedure is
/// identified by a validated <see cref="ProcedureName"/> and data travels in typed <see cref="ProcedureParameters"/>.
/// </summary>
public interface ISqlProcedureExecutor
{
    /// <summary>Runs the procedure and maps its first result set to <typeparamref name="T"/>.</summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Runs the procedure and returns the single row (or default). Throws if more than one row is returned.</summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Runs the procedure without reading rows; returns rows affected, RETURN value and output parameters.</summary>
    Task<ProcedureResult> ExecuteAsync(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the procedure and exposes its result sets sequentially. Dispose the returned reader (it owns the connection).
    /// </summary>
    Task<IProcedureMultipleResult> QueryMultipleAsync(ProcedureName procedure, ProcedureParameters? parameters = null, CancellationToken cancellationToken = default);
}

/// <summary>Sequential reader over the result sets of one procedure call.</summary>
public interface IProcedureMultipleResult : IAsyncDisposable
{
    /// <summary>Reads the next result set as a list of <typeparamref name="T"/>.</summary>
    Task<IReadOnlyList<T>> ReadAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>Reads the next result set expecting at most one row.</summary>
    Task<T?> ReadSingleOrDefaultAsync<T>(CancellationToken cancellationToken = default);
}
