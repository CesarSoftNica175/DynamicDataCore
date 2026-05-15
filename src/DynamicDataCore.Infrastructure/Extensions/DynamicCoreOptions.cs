using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Infrastructure.Extensions;

/// <summary>
/// Configuration for multi-database DynamicDataCore registration.
/// Maps logical database keys to DbContext types.
/// </summary>
public sealed class DynamicCoreOptions
{
    internal Dictionary<string, Type> DatabaseMap { get; } = [];

    /// <summary>
    /// Registers a DbContext type under the given logical <paramref name="key"/>.
    /// The consumer must independently register <typeparamref name="TContext"/> with
    /// <c>AddDbContext</c> or <c>AddDbContextPool</c>.
    /// </summary>
    public DynamicCoreOptions AddDatabase<TContext>(string key) where TContext : DbContext
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Database key must not be null or whitespace.", nameof(key));

        DatabaseMap[key] = typeof(TContext);
        return this;
    }
}
