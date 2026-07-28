using Bugler.Registry.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bugler.Registry.RetentionPolicy;

internal sealed class RetentionReader(
    RegistryDbContext dbContext,
    IOptions<RegistryOptions> options) : IRetentionReader
{
    public async Task<IReadOnlyList<ServiceRetention>> GetEffectiveRetentionsAsync(
        CancellationToken cancellationToken)
    {
        var defaultDays = options.Value.DefaultRetentionDays;
        return await dbContext.Services
            .Select(s => new ServiceRetention(s.Id, s.RetentionDays ?? defaultDays))
            .ToListAsync(cancellationToken);
    }
}
