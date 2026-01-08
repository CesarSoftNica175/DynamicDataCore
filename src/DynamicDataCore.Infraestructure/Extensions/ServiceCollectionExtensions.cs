using DynamicDataCore.Abstractions;
using DynamicDataCore.Infraestructure.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Infraestructure.Extensions
{

    /// <summary>
    /// Description: Provides extension methods for registering all core DynamicDataCore infrastructure
    /// services into the application's Dependency Injection container.
    /// <para>
    /// This includes default bindings for repositories, unit of work, base services,
    /// and the factory responsible for dynamically creating generic service instances.
    /// </para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public static class ServiceCollectionExtensions
    {

        /// <summary>
        /// Registers the core infrastructure components of <c>DynamicDataCore</c> into the
        /// provided <see cref="IServiceCollection"/> instance.
        /// <para>
        /// This method wires up the generic repository pattern, unit of work, and dynamic
        /// service factory for seamless data access integration.
        /// </para>
        /// </summary>
        /// <param name="services">
        /// The <see cref="IServiceCollection"/> into which the dependencies are to be registered.
        /// </param>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> instance, allowing for fluent chaining.
        /// </returns>
        /// <remarks>
        /// Registrations:
        /// <list type="bullet">
        /// <item><description><see cref="IUnitOfWork"/> → <see cref="UnitOfWorkImpl"/></description></item>
        /// <item><description><see cref="IGenericRepository{T}"/> → <see cref="GenericRepositoryImpl{T}"/></description></item>
        /// <item><description><see cref="IBaseGenericService{T}"/> → <see cref="BaseGenericServiceImpl{T}"/></description></item>
        /// <item><description><see cref="IBaseGenericServiceFactory"/> → <see cref="BaseGenericServiceFactoryImpl"/></description></item>
        /// </list>
        /// </remarks>
        public static IServiceCollection AddDynamicCoreInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWorkImpl>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepositoryImpl<>));
            services.AddScoped(typeof(IBaseGenericService<>), typeof(BaseGenericServiceImpl<>));
            services.AddSingleton<IBaseGenericServiceFactory, BaseGenericServiceFactoryImpl>();

            return services;
        }

    }
}
