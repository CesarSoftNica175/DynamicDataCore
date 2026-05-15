namespace DynamicDataCore.Abstractions;

/// <summary>
/// Creates <see cref="IBaseGenericService{T}"/> instances scoped to a logical database key.
/// </summary>
/// <remarks>
/// Prefer injecting <see cref="IDbContextProvider"/> directly for new code.
/// This factory remains for backwards compatibility.
/// </remarks>
[Obsolete("Use IDbContextProvider + scoped IBaseGenericService<T> registration. Will be removed in v3.0.")]
public interface IBaseGenericServiceFactory
{
    /// <summary>
    /// Creates a service for entity <typeparamref name="T"/> bound to the specified <paramref name="databaseKey"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No DbContext registered for <paramref name="databaseKey"/>.</exception>
    IBaseGenericService<T> Create<T>(string databaseKey) where T : class;
}
