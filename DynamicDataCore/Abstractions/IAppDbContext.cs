using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DynamicDataCore.Abstractions
{
    public interface IAppDbContext
    {

        DbSet<T> Set<T>() where T : class;

        DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    }
}
