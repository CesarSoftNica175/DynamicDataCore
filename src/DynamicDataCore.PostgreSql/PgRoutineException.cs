using Npgsql;

namespace DynamicDataCore.PostgreSql;

/// <summary>
/// A PostgreSQL error raised while running a routine. Business rules written as
/// <c>RAISE EXCEPTION ... USING ERRCODE = 'P0001', HINT = 'some_code'</c> surface here with
/// <see cref="IsBusinessRule"/> = <c>true</c> and the hint in <see cref="Code"/>, so callers can map them
/// to HTTP status codes without parsing messages.
/// </summary>
public sealed class PgRoutineException : Exception
{
    /// <summary>Creates the exception from a driver error.</summary>
    public PgRoutineException(PostgresException inner)
        : base(inner?.MessageText ?? throw new ArgumentNullException(nameof(inner)), inner)
    {
        SqlState = inner.SqlState;
        Code = string.IsNullOrWhiteSpace(inner.Hint) ? inner.SqlState : inner.Hint;
        Detail = inner.Detail;
        ConstraintName = inner.ConstraintName;
    }

    /// <summary>The five-character SQLSTATE (e.g. <c>P0001</c>, <c>23505</c>).</summary>
    public string SqlState { get; }

    /// <summary>The routine's <c>HINT</c> when it set one (its stable error code); otherwise the SQLSTATE.</summary>
    public string Code { get; }

    /// <summary>The server's <c>DETAIL</c>, if any.</summary>
    public string? Detail { get; }

    /// <summary>The violated constraint for integrity errors, if any.</summary>
    public string? ConstraintName { get; }

    /// <summary><c>true</c> for errors the routine raised on purpose (<c>RAISE EXCEPTION</c>, SQLSTATE <c>P0001</c>).</summary>
    public bool IsBusinessRule => SqlState == PostgresErrorCodes.RaiseException;

    /// <summary><c>true</c> for unique-constraint violations (SQLSTATE <c>23505</c>).</summary>
    public bool IsUniqueViolation => SqlState == PostgresErrorCodes.UniqueViolation;
}
