using System.Text.RegularExpressions;

namespace DynamicDataCore.Abstractions.Procedures;

/// <summary>
/// Shared validation for two-part SQL object names (<c>schema.name</c>). Only ASCII letters, digits and
/// underscores are accepted, so a validated name can never carry SQL syntax (quotes, brackets, spaces, semicolons...).
/// </summary>
internal static partial class SqlIdentifier
{
    internal const int MaxPartLength = 128;

    // Equivalent to ^[A-Za-z_]\w*\.[A-Za-z_]\w*$ restricted to ASCII, and anchored with \z
    // so a trailing newline is NOT accepted (a plain '$' would allow it).
    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TwoPart();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex OnePart();

    internal static bool TryParse(string? value, out string schema, out string name)
    {
        schema = name = string.Empty;
        if (value is null) return false;

        var m = TwoPart().Match(value);
        if (!m.Success) return false;

        var s = m.Groups[1].Value;
        var n = m.Groups[2].Value;
        if (s.Length > MaxPartLength || n.Length > MaxPartLength) return false;

        schema = s;
        name = n;
        return true;
    }

    internal static bool IsValidPart(string? value)
        => value is { Length: > 0 and <= MaxPartLength } && OnePart().IsMatch(value);
}
