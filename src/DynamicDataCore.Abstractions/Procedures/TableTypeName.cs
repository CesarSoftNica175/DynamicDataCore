namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>
/// The name of a user-defined table type (TVP), always <c>schema.name</c>, validated like <see cref="ProcedureName"/>.
/// </summary>
public sealed record TableTypeName
{
    private TableTypeName(string schema, string name)
    {
        Schema = schema;
        Name = name;
    }

    /// <summary>Schema part.</summary>
    public string Schema { get; }

    /// <summary>Type part.</summary>
    public string Name { get; }

    /// <summary>The validated <c>schema.name</c> form.</summary>
    public string FullName => $"{Schema}.{Name}";

    /// <summary>Parses <paramref name="value"/> or throws <see cref="ArgumentException"/>.</summary>
    public static TableTypeName Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryCreate(value, out var result)
            ? result!
            : throw new ArgumentException(
                "Table type name must be in 'schema.name' form using only letters, digits and underscores (not starting with a digit).",
                nameof(value));
    }

    /// <summary>Attempts to parse <paramref name="value"/>.</summary>
    public static bool TryCreate(string? value, out TableTypeName? result)
    {
        if (SqlIdentifier.TryParse(value, out var schema, out var name))
        {
            result = new TableTypeName(schema, name);
            return true;
        }

        result = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => FullName;
}
