namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>
/// The name of a stored procedure, always in <c>schema.name</c> form. This is the ONLY way to tell
/// <see cref="ISqlProcedureExecutor"/> what to run: no public API accepts free-form SQL text.
/// </summary>
/// <remarks>
/// Validated against <c>^[A-Za-z_]\w*\.[A-Za-z_]\w*$</c> (ASCII word characters, each part at most 128 chars).
/// The constructor is private; use <see cref="Create(string)"/> or <see cref="TryCreate(string?, out ProcedureName?)"/>.
/// </remarks>
public sealed record ProcedureName
{
    private ProcedureName(string schema, string name)
    {
        Schema = schema;
        Name = name;
    }

    /// <summary>Schema part (e.g. <c>dbo</c>).</summary>
    public string Schema { get; }

    /// <summary>Procedure part (e.g. <c>usp_GetOrders</c>).</summary>
    public string Name { get; }

    /// <summary>The validated <c>schema.name</c> form.</summary>
    public string FullName => $"{Schema}.{Name}";

    /// <summary>Parses <paramref name="value"/> or throws <see cref="ArgumentException"/>.</summary>
    public static ProcedureName Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryCreate(value, out var result)
            ? result!
            : throw new ArgumentException(
                "Procedure name must be in 'schema.name' form using only letters, digits and underscores (not starting with a digit).",
                nameof(value));
    }

    /// <summary>Builds a name from its two parts, applying the same validation.</summary>
    public static ProcedureName Create(string schema, string name)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(name);
        return Create($"{schema}.{name}");
    }

    /// <summary>Attempts to parse <paramref name="value"/>.</summary>
    public static bool TryCreate(string? value, out ProcedureName? result)
    {
        if (SqlIdentifier.TryParse(value, out var schema, out var name))
        {
            result = new ProcedureName(schema, name);
            return true;
        }

        result = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => FullName;
}
