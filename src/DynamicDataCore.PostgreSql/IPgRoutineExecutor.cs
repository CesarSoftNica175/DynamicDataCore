using DynamicDataCore.Abstractions.Procedures;

namespace DynamicDataCore.PostgreSql;

/// <summary>
/// Runs PostgreSQL functions and procedures only. There is intentionally no overload that accepts SQL text:
/// the routine is identified by a validated <see cref="ProcedureName"/> (<c>schema.name</c>) and data travels
/// in typed <see cref="PgParameters"/>. Calls run on the connection and transaction of the scoped
/// <c>IAppDbContext</c>, so inside <c>IUnitOfWork.BeginTransactionAsync</c> they commit or roll back together
/// with EF Core changes. Errors surface as <see cref="PgRoutineException"/>.
/// </summary>
public interface IPgRoutineExecutor
{
    /// <summary>Runs <c>SELECT * FROM routine(...)</c> and maps the rows to <typeparamref name="T"/>.</summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Like <see cref="QueryAsync{T}"/> but returns the single row (or default). Throws if there is more than one.</summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a function that returns one <c>json</c>/<c>jsonb</c>/<c>text</c> value and deserializes it to
    /// <typeparamref name="T"/> with System.Text.Json (web defaults, case-insensitive). Returns default for SQL NULL.
    /// </summary>
    Task<T?> JsonAsync<T>(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Runs a function for its side effects (<c>SELECT routine(...)</c>), discarding the result (e.g. <c>void</c> functions).</summary>
    Task ExecuteFunctionAsync(ProcedureName routine, PgParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Runs a stored procedure with <c>CALL routine(...)</c>.</summary>
    Task CallAsync(ProcedureName procedure, PgParameters? parameters = null, CancellationToken cancellationToken = default);
}
