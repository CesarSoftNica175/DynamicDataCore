namespace DynamicDataCore.Abstractions
{

    /// <summary>
    /// Description: Defines the contract for a factory responsible for creating 
    /// <see cref="IBaseGenericService{T}"/> instances dynamically based on the schema or database context.
    /// <para>This allows flexible resolution of service instances tied to different data contexts.</para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public interface IBaseGenericServiceFactory
    {

        /// <summary>
        /// Creates a new instance of a <see cref="IBaseGenericService{T}"/> for the specified schema name.
        /// </summary>
        /// <typeparam name="T">The entity type to manage with the created service.</typeparam>
        /// <param name="schemaName">The schema or context identifier from which to resolve the DbContext.</param>
        /// <returns>An instance of <see cref="IBaseGenericService{T}"/> bound to the corresponding context.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no DbContext mapping is found for the provided schema name.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown if the resolved DbContext does not implement <see cref="IAppDbContext"/>.
        /// </exception>
        IBaseGenericService<T> Create<T>(string schemaName) where T : class;

    }
}
