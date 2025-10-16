using DynamicDataCore.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Implementation
{
    public class BaseGenericServiceFactoryImpl : IBaseGenericServiceFactory
    {

        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _contextTypeMap;

        public BaseGenericServiceFactoryImpl(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;

            // Cargar mapeo desde configuración
            var mappings = configuration.GetSection("DbContextMappings").Get<Dictionary<string, string>>();

            if (mappings is null || mappings.Count == 0)
                throw new InvalidOperationException("No se encontraron mapeos de DbContext en la configuración.");

            _contextTypeMap = mappings.ToDictionary(
                kvp => kvp.Key,
                kvp => Type.GetType(kvp.Value)
                    ?? throw new InvalidOperationException($"No se pudo cargar el tipo '{kvp.Value}' para el esquema '{kvp.Key}'.")
            );
        }

        public IBaseGenericService<T> Create<T>(string schemaName) where T : class
        {
            if (!_contextTypeMap.TryGetValue(schemaName, out var contextType))
                throw new InvalidOperationException($"No DbContext mapping found for schema '{schemaName}'");

            var contextInstance = _serviceProvider.GetRequiredService(contextType);

            if (contextInstance is not IAppDbContext appDbContext)
                throw new InvalidCastException($"Resolved context does not implement IAppDbContext: {contextType.Name}");

            var unitOfWork = new UnitOfWorkImpl(appDbContext);
            return new BaseGenericServiceImpl<T>(unitOfWork);
        }

    }
}
