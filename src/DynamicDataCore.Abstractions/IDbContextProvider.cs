using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Abstractions;

/// <summary>
/// Resolves a pooled <see cref="DbContext"/> instance by logical database key.
/// Prefer this over <see cref="IBaseGenericServiceFactory"/> for new code.
/// </summary>
public interface IDbContextProvider
{
    /// <summary>
    /// Creates (or retrieves from pool) a <typeparamref name="TContext"/> for the given <paramref name="databaseKey"/>.
    /// The caller is responsible for disposing the returned context.
    /// </summary>
    TContext CreateContext<TContext>(string databaseKey) where TContext : DbContext;

    /// <summary>Returns all registered database keys.</summary>
    IReadOnlyCollection<string> GetRegisteredKeys();
}
