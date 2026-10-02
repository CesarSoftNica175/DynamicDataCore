using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Infrastructure.Extensions;

/// <summary>Helpers to map keyless entities to SQL views for <c>IReadRepository&lt;T&gt;</c>.</summary>
public static partial class ModelBuilderViewExtensions
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// Configures <typeparamref name="T"/> as keyless and mapped to the view <paramref name="viewName"/>
    /// (<c>HasNoKey().ToView(viewName, schema)</c>). Call it from <c>OnModelCreating</c> of the consumer's DbContext.
    /// </summary>
    public static ModelBuilder ConfigureReadOnlyView<T>(this ModelBuilder modelBuilder, string viewName, string schema = "dbo")
        where T : class
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        if (viewName is null || !IdentifierRegex().IsMatch(viewName))
            throw new ArgumentException("View name must contain only letters, digits and underscores.", nameof(viewName));
        if (schema is null || !IdentifierRegex().IsMatch(schema))
            throw new ArgumentException("Schema must contain only letters, digits and underscores.", nameof(schema));

        modelBuilder.Entity<T>().HasNoKey().ToView(viewName, schema);
        return modelBuilder;
    }
}
