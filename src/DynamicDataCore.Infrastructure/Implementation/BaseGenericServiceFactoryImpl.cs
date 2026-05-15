#pragma warning disable CS0618
using DynamicDataCore.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Infrastructure.Implementation;

/// <summary>
/// Scoped factory that creates <see cref="IBaseGenericService{T}"/> instances per database key.
/// Uses <see cref="IServiceScopeFactory"/> to create isolated scopes, avoiding captive-dependency issues.
/// </summary>
/// <remarks>This class exists for backwards compatibility. Prefer <see cref="IDbContextProvider"/> for new code.</remarks>
[Obsolete("Use IDbContextProvider + scoped IBaseGenericService<T> registration. Will be removed in v3.0.")]
public sealed class ScopedServiceFactory : IBaseGenericServiceFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextProvider _contextProvider;

    public ScopedServiceFactory(IServiceScopeFactory scopeFactory, IDbContextProvider contextProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
    }

    /// <inheritdoc/>
    public IBaseGenericService<T> Create<T>(string databaseKey) where T : class
    {
        if (!_contextProvider.GetRegisteredKeys().Contains(databaseKey))
            throw new InvalidOperationException($"No DbContext registered for database key '{databaseKey}'.");

        // Each call creates a short-lived scope to avoid lifetime issues with Scoped DbContexts
        var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return new BaseGenericServiceImpl<T>(unitOfWork);
    }
}
#pragma warning restore CS0618
