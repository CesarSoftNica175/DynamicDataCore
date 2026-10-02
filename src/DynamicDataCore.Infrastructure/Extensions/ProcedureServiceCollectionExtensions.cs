using DynamicDataCore.Abstractions;
using DynamicDataCore.Abstractions.Procedures;
using DynamicDataCore.Infrastructure.Implementation;
using DynamicDataCore.Infrastructure.Procedures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamicDataCore.Infrastructure.Extensions;

/// <summary>DI registration for the stored-procedure executor and read-only view repositories (v2.1).</summary>
public static class ProcedureServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISqlProcedureExecutor"/> (singleton, stateless) reading the connection string named
    /// <paramref name="connectionStringName"/> from <c>IConfiguration</c> (<c>ConnectionStrings:{name}</c>, user secrets,
    /// environment variables, etc.). Nothing is stored in the repo. Resolution throws if the string is missing.
    /// </summary>
    public static IServiceCollection AddDynamicDataCoreProcedures(this IServiceCollection services, string connectionStringName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

        services.TryAddSingleton<ISqlProcedureExecutor>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"Connection string '{connectionStringName}' was not found in configuration.");
            return new SqlProcedureExecutor(connectionString);
        });

        return services;
    }

    /// <summary>
    /// Registers the open-generic <see cref="IReadRepository{T}"/> (scoped). Requires <see cref="IAppDbContext"/>
    /// to be registered, as for the rest of DynamicDataCore.
    /// </summary>
    public static IServiceCollection AddDynamicDataCoreReadRepositories(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped(typeof(IReadRepository<>), typeof(ReadRepositoryImpl<>));
        return services;
    }
}
