using Bugler.Registry.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.BrowseCatalog;

internal sealed class CatalogReader(RegistryDbContext dbContext) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogInstance>> GetInstancesAsync(CancellationToken cancellationToken) =>
        await dbContext.Instances
            .Join(
                dbContext.Applications,
                instance => instance.ApplicationId,
                application => application.Id,
                (instance, application) =>
                    new CatalogInstance(instance.Id, application.Id, instance.Name, application.Name))
            .ToListAsync(cancellationToken);
}
