using DynamicDataCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Infrastructure.Implementation;

/// <summary>
/// Resolves pooled <see cref="DbContext"/> instances by logical database key using
/// registered <see cref="IDbContextFactory{TContext}"/> services.
/// </summary>
public sealed class PooledDbContextProvider : IDbContextProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, Type> _keyToContextType;

    public PooledDbContextProvider(IServiceProvider serviceProvider, IReadOnlyDictionary<string, Type> keyToContextType)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _keyToContextType = keyToContextType ?? throw new ArgumentNullException(nameof(keyToContextType));
    }

    /// <inheritdoc/>
    public TContext CreateContext<TContext>(string databaseKey) where TContext : DbContext
    {
        if (!_keyToContextType.TryGetValue(databaseKey, out var registeredType))
            throw new InvalidOperationException($"No DbContext registered for database key '{databaseKey}'. Check AddDynamicCoreInfrastructure configuration.");

        if (registeredType != typeof(TContext))
            throw new InvalidOperationException(
                $"Database key '{databaseKey}' is registered for {registeredType.Name}, not {typeof(TContext).Name}.");

        var factory = _serviceProvider.GetService<IDbContextFactory<TContext>>();
        if (factory is not null)
            return factory.CreateDbContext();

        // Fallback: resolve directly from DI (consumer did not use AddDbContextFactory)
        return _serviceProvider.GetRequiredService<TContext>();
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetRegisteredKeys()
        => (IReadOnlyCollection<string>)_keyToContextType.Keys;
}
