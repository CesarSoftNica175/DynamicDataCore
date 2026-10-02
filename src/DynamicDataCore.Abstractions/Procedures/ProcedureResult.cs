using System.Globalization;

namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>Outcome of <see cref="ISqlProcedureExecutor.ExecuteAsync"/>.</summary>
public sealed record ProcedureResult
{
    /// <summary>Rows affected as reported by SQL Server (-1 when the procedure uses SET NOCOUNT ON).</summary>
    public required int RowsAffected { get; init; }

    /// <summary>The RETURN value, or null when no return value parameter was declared.</summary>
    public int? ReturnValue { get; init; }

    /// <summary>Output / input-output values keyed by parameter name (with '@'). DBNull is normalized to null.</summary>
    public required IReadOnlyDictionary<string, object?> OutputValues { get; init; }

    /// <summary>Reads an output value converted to <typeparamref name="T"/>; null maps to <c>default</c>.</summary>
    public T? GetOutput<T>(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var key = name.StartsWith('@') ? name : "@" + name;
        if (!OutputValues.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"No output value named '{key}'.");
        if (value is null) return default;

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)(value.GetType() == target ? value : Convert.ChangeType(value, target, CultureInfo.InvariantCulture));
    }
}
