using System.Data.Common;
using System.Globalization;
using System.Reflection;

namespace DynamicDataCore.Infrastructure.Procedures;

/// <summary>
/// Minimal, internal row mapper (no third-party dependency). Maps a result set to scalars, types with a
/// parameterless constructor + settable properties, or types (records) with a constructor whose parameter names
/// match the column names. Column matching is case-insensitive; unmatched columns are ignored.
/// </summary>
internal static class RowMapper
{
    internal static Func<DbDataReader, T> CreateFactory<T>(DbDataReader reader)
    {
        var type = typeof(T);
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (IsScalar(underlying))
        {
            if (reader.FieldCount < 1)
                throw new InvalidOperationException("The result set has no columns to map to a scalar.");
            return r => (T)ConvertValue(r.GetValue(0), type, r.GetName(0))!;
        }

        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
            ordinals.TryAdd(reader.GetName(i), i);

        var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (ctor is not null || type.IsValueType)
            return CreateByProperties<T>(type, ordinals);

        return CreateByConstructor<T>(type, ordinals);
    }

    private static Func<DbDataReader, T> CreateByProperties<T>(Type type, Dictionary<string, int> ordinals)
    {
        var bindings = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .Select(p => (Property: p, Ordinal: ordinals.TryGetValue(p.Name, out var o) ? o : -1))
            .Where(b => b.Ordinal >= 0)
            .ToArray();

        return r =>
        {
            var instance = Activator.CreateInstance<T>();
            foreach (var (property, ordinal) in bindings)
                property.SetValue(instance, ConvertValue(r.GetValue(ordinal), property.PropertyType, property.Name));
            return instance;
        };
    }

    private static Func<DbDataReader, T> CreateByConstructor<T>(Type type, Dictionary<string, int> ordinals)
    {
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Type '{type.Name}' has no public constructor usable for mapping.");

        var parameters = ctor.GetParameters();
        var ordinalByParam = new int[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (ordinals.TryGetValue(parameters[i].Name!, out var o))
                ordinalByParam[i] = o;
            else if (parameters[i].HasDefaultValue)
                ordinalByParam[i] = -1;
            else
                throw new InvalidOperationException(
                    $"Constructor parameter '{parameters[i].Name}' of '{type.Name}' has no matching column in the result set.");
        }

        return r =>
        {
            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                args[i] = ordinalByParam[i] < 0
                    ? parameters[i].DefaultValue
                    : ConvertValue(r.GetValue(ordinalByParam[i]), parameters[i].ParameterType, parameters[i].Name!);
            return (T)ctor.Invoke(args);
        };
    }

    private static bool IsScalar(Type t)
        => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)
           || t == typeof(DateTimeOffset) || t == typeof(Guid) || t == typeof(TimeSpan) || t == typeof(byte[])
           || t == typeof(DateOnly) || t == typeof(TimeOnly);

    private static object? ConvertValue(object value, Type target, string columnName)
    {
        var underlying = Nullable.GetUnderlyingType(target);
        if (value is DBNull)
        {
            if (!target.IsValueType || underlying is not null) return null;
            throw new InvalidOperationException($"Column '{columnName}' is NULL but the target type '{target.Name}' is not nullable.");
        }

        var t = underlying ?? target;
        if (t.IsInstanceOfType(value)) return value;

        if (t.IsEnum)
            return value is string s ? Enum.Parse(t, s, ignoreCase: true) : Enum.ToObject(t, value);
        if (t == typeof(DateOnly) && value is DateTime dt) return DateOnly.FromDateTime(dt);
        if (t == typeof(TimeOnly) && value is TimeSpan ts) return TimeOnly.FromTimeSpan(ts);
        if (t == typeof(Guid) && value is string g) return Guid.Parse(g);

        return Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
    }
}
