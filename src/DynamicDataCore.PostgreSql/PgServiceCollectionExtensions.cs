using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamicDataCore.PostgreSql;

/// <summary>DI registration for the PostgreSQL routine executor.</summary>
public static class PgServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPgRoutineExecutor"/> (scoped). Requires <c>IAppDbContext</c> to be registered
    /// (as for the rest of DynamicDataCore) with a DbContext configured through <c>UseNpgsql</c>.
    /// </summary>
    public static IServiceCollection AddDynamicDataCorePostgreSql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IPgRoutineExecutor, PgRoutineExecutor>();
        return services;
    }
}
