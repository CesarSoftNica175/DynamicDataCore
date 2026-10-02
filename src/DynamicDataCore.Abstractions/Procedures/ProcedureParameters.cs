using System.Data;
using System.Text.RegularExpressions;

namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>
/// Fluent builder of strongly typed procedure parameters. Every parameter declares its <see cref="SqlDbType"/>;
/// values are always sent as parameters, never concatenated into SQL.
/// </summary>
public sealed partial class ProcedureParameters
{
    private readonly List<ProcedureParameter> _items = [];

    [GeneratedRegex(@"^@?([A-Za-z_][A-Za-z0-9_]{0,127})\z", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    /// <summary>Starts an empty builder.</summary>
    public static ProcedureParameters Create() => new();

    /// <summary>The parameters added so far.</summary>
    public IReadOnlyList<ProcedureParameter> Items => _items;

    /// <summary>Adds an input parameter.</summary>
    public ProcedureParameters Input(string name, SqlDbType dbType, object? value, int? size = null, byte? precision = null, byte? scale = null)
    {
        RejectStructured(dbType);
        return Add(name, dbType, ProcedureParameterDirection.Input, value, size, precision, scale);
    }

    /// <summary>Adds an output parameter. Variable-length types require <paramref name="size"/> (use -1 for MAX).</summary>
    public ProcedureParameters Output(string name, SqlDbType dbType, int? size = null, byte? precision = null, byte? scale = null)
    {
        RejectStructured(dbType);
        return Add(name, dbType, ProcedureParameterDirection.Output, null, size, precision, scale);
    }

    /// <summary>Adds an input/output parameter.</summary>
    public ProcedureParameters InputOutput(string name, SqlDbType dbType, object? value, int? size = null, byte? precision = null, byte? scale = null)
    {
        RejectStructured(dbType);
        return Add(name, dbType, ProcedureParameterDirection.InputOutput, value, size, precision, scale);
    }

    /// <summary>Captures the procedure's integer RETURN value (at most one per call).</summary>
    public ProcedureParameters ReturnValue(string name = "@ReturnValue")
        => Add(name, SqlDbType.Int, ProcedureParameterDirection.ReturnValue, null, null, null, null);

    /// <summary>Adds a table-valued parameter from a <see cref="DataTable"/>.</summary>
    public ProcedureParameters Structured(string name, DataTable table, TableTypeName tableType)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(tableType);
        return Add(name, SqlDbType.Structured, ProcedureParameterDirection.Input, table, null, null, null, tableType);
    }

    /// <summary>Adds a table-valued parameter from provider-agnostic rows.</summary>
    public ProcedureParameters Structured(string name, TableValuedRows rows, TableTypeName tableType)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(tableType);
        return Add(name, SqlDbType.Structured, ProcedureParameterDirection.Input, rows, null, null, null, tableType);
    }

    private static void RejectStructured(SqlDbType dbType)
    {
        if (dbType == SqlDbType.Structured)
            throw new ArgumentException("Use Structured(...) for table-valued parameters.", nameof(dbType));
    }

    private ProcedureParameters Add(
        string name, SqlDbType dbType, ProcedureParameterDirection direction, object? value,
        int? size, byte? precision, byte? scale, TableTypeName? tableType = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        var m = NameRegex().Match(name);
        if (!m.Success)
            throw new ArgumentException($"Invalid parameter name '{name}'.", nameof(name));

        var normalized = "@" + m.Groups[1].Value;

        if (_items.Any(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Parameter '{normalized}' was already added.", nameof(name));

        if (direction == ProcedureParameterDirection.ReturnValue && _items.Any(p => p.Direction == ProcedureParameterDirection.ReturnValue))
            throw new InvalidOperationException("Only one return value parameter is allowed.");

        if (size is < -1 or 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive or -1 (MAX).");

        if (direction is ProcedureParameterDirection.Output or ProcedureParameterDirection.InputOutput
            && size is null && IsVariableLength(dbType))
            throw new ArgumentException($"Output parameter '{normalized}' of type {dbType} requires a size (-1 for MAX).", nameof(size));

        if ((precision is not null || scale is not null) && dbType is not (SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney
            or SqlDbType.Time or SqlDbType.DateTime2 or SqlDbType.DateTimeOffset))
            throw new ArgumentException($"Precision/scale do not apply to {dbType}.", nameof(precision));

        _items.Add(new ProcedureParameter
        {
            Name = normalized,
            DbType = dbType,
            Direction = direction,
            Value = value,
            Size = size,
            Precision = precision,
            Scale = scale,
            TableType = tableType
        });
        return this;
    }

    private static bool IsVariableLength(SqlDbType t) => t is SqlDbType.NVarChar or SqlDbType.VarChar or SqlDbType.VarBinary
        or SqlDbType.NChar or SqlDbType.Char or SqlDbType.Binary;
}
