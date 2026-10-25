using System.Text.Json;
using System.Text.RegularExpressions;
using NpgsqlTypes;

namespace DynamicDataCore.PostgreSql;

/// <summary>One typed parameter of a PostgreSQL routine call.</summary>
public sealed record PgParameter(string Name, NpgsqlDbType DbType, object? Value);

/// <summary>
/// Fluent builder of strongly typed routine parameters. Parameters are passed to PostgreSQL by NAME
/// (<c>fn(p_name =&gt; $1)</c>), so each name must match the routine's argument name, and order does not matter.
/// Values travel only as <see cref="Npgsql.NpgsqlParameter"/> values; they are never concatenated into SQL.
/// </summary>
public sealed partial class PgParameters
{
    private readonly List<PgParameter> _items = [];

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,62}\z", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    /// <summary>Starts an empty builder.</summary>
    public static PgParameters Create() => new();

    /// <summary>The parameters added so far, in insertion order.</summary>
    public IReadOnlyList<PgParameter> Items => _items;

    /// <summary>Adds an input parameter. A <c>null</c> value is sent as SQL NULL.</summary>
    public PgParameters Input(string name, NpgsqlDbType dbType, object? value)
        => Add(name, dbType, value);

    /// <summary>Serializes <paramref name="value"/> with System.Text.Json and sends it as <c>jsonb</c>.</summary>
    public PgParameters Json(string name, object? value, JsonSerializerOptions? options = null)
        => Add(name, NpgsqlDbType.Jsonb, value is null ? null : JsonSerializer.Serialize(value, options));

    /// <summary>Sends a pre-serialized JSON document as <c>jsonb</c>.</summary>
    public PgParameters JsonText(string name, string? json)
        => Add(name, NpgsqlDbType.Jsonb, json);

    /// <summary>Adds a <c>uuid</c> parameter.</summary>
    public PgParameters Uuid(string name, Guid? value) => Add(name, NpgsqlDbType.Uuid, value);

    /// <summary>Adds a <c>text</c> parameter.</summary>
    public PgParameters Text(string name, string? value) => Add(name, NpgsqlDbType.Text, value);

    /// <summary>Adds an <c>integer</c> parameter.</summary>
    public PgParameters Int(string name, int? value) => Add(name, NpgsqlDbType.Integer, value);

    /// <summary>Adds a <c>bigint</c> parameter.</summary>
    public PgParameters BigInt(string name, long? value) => Add(name, NpgsqlDbType.Bigint, value);

    /// <summary>Adds a <c>numeric</c> parameter.</summary>
    public PgParameters Numeric(string name, decimal? value) => Add(name, NpgsqlDbType.Numeric, value);

    /// <summary>Adds a <c>boolean</c> parameter.</summary>
    public PgParameters Bool(string name, bool? value) => Add(name, NpgsqlDbType.Boolean, value);

    /// <summary>
    /// Adds a <c>timestamp without time zone</c> parameter. The value is forced to
    /// <see cref="DateTimeKind.Unspecified"/> because Npgsql rejects <c>Kind=Utc</c> for that column type.
    /// </summary>
    public PgParameters Timestamp(string name, DateTime? value)
        => Add(name, NpgsqlDbType.Timestamp, value is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Unspecified) : null);

    /// <summary>Adds a <c>timestamp with time zone</c> parameter. The value must be UTC.</summary>
    public PgParameters TimestampTz(string name, DateTimeOffset? value)
        => Add(name, NpgsqlDbType.TimestampTz, value?.UtcDateTime);

    /// <summary>Adds a <c>date</c> parameter.</summary>
    public PgParameters Date(string name, DateOnly? value) => Add(name, NpgsqlDbType.Date, value);

    private PgParameters Add(string name, NpgsqlDbType dbType, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.StartsWith('@') ? name[1..] : name;
        if (!NameRegex().IsMatch(normalized))
            throw new ArgumentException($"Invalid parameter name '{name}'.", nameof(name));

        if (_items.Any(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Parameter '{normalized}' was already added.", nameof(name));

        _items.Add(new PgParameter(normalized, dbType, value));
        return this;
    }
}
