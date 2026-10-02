using System.Data;

namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>Direction of a stored procedure parameter.</summary>
public enum ProcedureParameterDirection
{
    /// <summary>Value sent to the procedure.</summary>
    Input,
    /// <summary>Value returned by the procedure.</summary>
    Output,
    /// <summary>Value sent and returned.</summary>
    InputOutput,
    /// <summary>The procedure's integer RETURN value.</summary>
    ReturnValue
}

/// <summary>Column definition for a table-valued parameter built from <see cref="TableValuedRows"/>.</summary>
public sealed record TableValueColumn(string Name, SqlDbType DbType, int? Size = null, byte? Precision = null, byte? Scale = null);

/// <summary>
/// Rows for a table-valued parameter, without exposing any provider type: column metadata plus object arrays
/// in column order. The executor converts them to the driver's native representation.
/// </summary>
public sealed class TableValuedRows
{
    /// <summary>Creates the row set.</summary>
    public TableValuedRows(IReadOnlyList<TableValueColumn> columns, IEnumerable<object?[]> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        if (columns.Count == 0)
            throw new ArgumentException("At least one column is required.", nameof(columns));
        foreach (var c in columns)
            if (!SqlIdentifier.IsValidPart(c.Name))
                throw new ArgumentException($"Invalid column name '{c.Name}'.", nameof(columns));

        Columns = columns;
        Rows = rows;
    }

    /// <summary>Column definitions.</summary>
    public IReadOnlyList<TableValueColumn> Columns { get; }

    /// <summary>Row values, one array per row, in <see cref="Columns"/> order.</summary>
    public IEnumerable<object?[]> Rows { get; }
}

/// <summary>An immutable, validated parameter. Create instances through <see cref="ProcedureParameters"/>.</summary>
public sealed record ProcedureParameter
{
    internal ProcedureParameter() { }

    /// <summary>Parameter name including the leading '@'.</summary>
    public required string Name { get; init; }

    /// <summary>SQL type.</summary>
    public required SqlDbType DbType { get; init; }

    /// <summary>Direction.</summary>
    public required ProcedureParameterDirection Direction { get; init; }

    /// <summary>Input value (null is sent as SQL NULL). For TVPs: a <see cref="DataTable"/> or <see cref="TableValuedRows"/>.</summary>
    public object? Value { get; init; }

    /// <summary>Size for variable-length types (-1 = MAX).</summary>
    public int? Size { get; init; }

    /// <summary>Precision for decimal/numeric.</summary>
    public byte? Precision { get; init; }

    /// <summary>Scale for decimal/numeric/time-like types.</summary>
    public byte? Scale { get; init; }

    /// <summary>Table type for TVPs.</summary>
    public TableTypeName? TableType { get; init; }
}
