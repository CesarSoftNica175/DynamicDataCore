using DynamicDataCore.Abstractions;
using DynamicDataCore.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Extensions
{
    public static class ServiceCollectionExtensions
    {

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
