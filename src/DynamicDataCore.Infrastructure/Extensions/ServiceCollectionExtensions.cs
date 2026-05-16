#pragma warning disable CS0618
using DynamicDataCore.Abstractions;
using DynamicDataCore.Infrastructure.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering DynamicDataCore infrastructure into the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core DynamicDataCore services using a consumer-supplied
    /// <see cref="IAppDbContext"/> already registered in the container.
    /// </summary>
    /// <remarks>
    /// The consumer is responsible for registering their <c>DbContext</c>
    /// (ideally with <c>AddDbContextPool</c> or <c>AddDbContext</c>) before calling this method.
    /// </remarks>
    public static IServiceCollection AddDynamicCoreInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWorkImpl>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepositoryImpl<>));
        services.AddScoped(typeof(IBaseGenericService<>), typeof(BaseGenericServiceImpl<>));

        return services;
    }

    /// <summary>
    /// Registers DynamicDataCore services with multi-database support.
    /// Use <paramref name="configure"/> to map logical keys to DbContext types.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddDynamicCoreInfrastructure(opt =>
    /// {
    ///     opt.AddDatabase&lt;PrimaryDbContext&gt;("Primary");
    ///     opt.AddDatabase&lt;ReportingDbContext&gt;("Reporting");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDynamicCoreInfrastructure(
        this IServiceCollection services,
        Action<DynamicCoreOptions> configure)
    {
        var options = new DynamicCoreOptions();
        configure(options);

        services.AddScoped<IUnitOfWork, UnitOfWorkImpl>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepositoryImpl<>));
        services.AddScoped(typeof(IBaseGenericService<>), typeof(BaseGenericServiceImpl<>));

        var keyToType = options.DatabaseMap as IReadOnlyDictionary<string, Type>
                        ?? new Dictionary<string, Type>(options.DatabaseMap);

        services.AddSingleton<IDbContextProvider>(sp => new PooledDbContextProvider(sp, keyToType));
        services.AddScoped<IBaseGenericServiceFactory, ScopedServiceFactory>();

        return services;
    }
}
#pragma warning restore CS0618
