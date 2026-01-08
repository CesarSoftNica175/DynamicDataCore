using System;
using System.Collections.Generic;
using System.Linq;
using DynamicDataCore.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Infraestructure.Implementation
{

    /// <summary>
    /// Description: Provides the default implementation of <see cref="IBaseGenericServiceFactory"/>,
    /// capable of creating service instances dynamically based on schema-to-DbContext mappings
    /// defined in configuration.
    /// <para>This enables multi-context applications to resolve services for different databases or schemas.</para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public class BaseGenericServiceFactoryImpl : IBaseGenericServiceFactory
    {

        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _contextTypeMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseGenericServiceFactoryImpl"/> class.
        /// </summary>
        /// <param name="serviceProvider">The application's dependency injection service provider.</param>
        /// <param name="configuration">The configuration instance containing the section <c>DbContextMappings</c>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the configuration section <c>DbContextMappings</c> is missing or empty.
        /// </exception>
        public BaseGenericServiceFactoryImpl(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;

            // Cargar mapeo desde configuración
            var mappings = configuration.GetSection("DbContextMappings").Get<Dictionary<string, string>>();

            if (mappings is null || mappings.Count == 0)
                throw new InvalidOperationException("No DbContext assignments were found in the configuration.");

            _contextTypeMap = mappings.ToDictionary(
                kvp => kvp.Key,
                kvp => Type.GetType(kvp.Value)
                    ?? throw new InvalidOperationException($"The type '{kvp.Value}' for the schema '{kvp.Key}' could not be loaded.")
            );
        }

        /// <inheritdoc/>
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
