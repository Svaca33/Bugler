using Bugler.Registry.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.BrowseCatalog;

internal sealed class CatalogReader(RegistryDbContext dbContext) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogService>> GetServicesAsync(CancellationToken cancellationToken) =>
        await dbContext.Services
            .Join(
                dbContext.Applications,
                service => service.ApplicationId,
                application => application.Id,
                (service, application) => new CatalogService(
                    service.Id,
                    application.Id,
                    application.Name,
                    service.Namespace,
                    service.Environment,
                    service.Name))
            .ToListAsync(cancellationToken);
}
