using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DynamicDataCore.Abstractions
{

    /// <summary>
    /// Description: Defines the base abstraction for the Entity Framework Core database context.
    /// <para></para>
    /// This interface allows DynamicDataCore to operate independently of specific DbContext implementations,
    /// providing a standardized contract for database operations such as querying, transaction handling, and persistence.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CesarSoftNica).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public interface IAppDbContext
    {

        /// <summary>
        /// Description: Gets the <see cref="DbSet{TEntity}"/> corresponding to the specified entity type.
        /// <para></para>
        /// This property allows performing CRUD operations and LINQ queries over entities managed by the context.
        /// <para></para>
        /// <author>Created By: César Adolfo Solís Alvarez (CesarSoftNica).</author>
        /// <para></para>
        /// <since>Creation Date: 17/10/2025</since>
        /// </summary>
        /// <typeparam name="T">Represents the entity type to be queried or modified.</typeparam>
        /// <returns>
        /// Returns a <see cref="DbSet{TEntity}"/> that can be used to query and manipulate data for the specified entity type.
        /// </returns>
        DbSet<T> Set<T>() where T : class;

        /// <summary>
        /// Description: Provides access to the underlying <see cref="DatabaseFacade"/> for managing transactions, 
        /// connections, and low-level database operations.
        /// <para></para>
        /// This property allows performing explicit transaction control or raw SQL execution when needed.
        /// <para></para>
        /// <author>Created By: César Adolfo Solís Alvarez (CesarSoftNica).</author>
        /// <para></para>
        /// <since>Creation Date: 17/10/2025</since>
        /// </summary>
        DatabaseFacade Database { get; }

        /// <summary>
        /// Description: Persists all pending changes to the database asynchronously.
        /// <para></para>
        /// It executes INSERT, UPDATE, or DELETE statements for all tracked entities that have been modified.
        /// <para></para>
        /// <author>Created By: César Adolfo Solís Alvarez (CesarSoftNica).</author>
        /// <para></para>
        /// <since>Creation Date: 17/10/2025</since>
        /// </summary>
        /// <param name="cancellationToken">
        /// A token used to cancel the asynchronous operation if necessary.
        /// </param>
        /// <returns>
        /// Returns an <see cref="int"/> representing the number of entities successfully written to the database.
        /// </returns>
        /// <exception cref="DbUpdateException">
        /// Thrown when an error occurs while saving changes to the database.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the operation is canceled before completion.
        /// </exception>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    }
}
