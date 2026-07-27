using Bugler.Registry.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bugler.Registry.RetentionPolicy;

internal sealed class RetentionReader(
    RegistryDbContext dbContext,
    IOptions<RegistryOptions> options) : IRetentionReader
{
    public async Task<IReadOnlyList<InstanceRetention>> GetEffectiveRetentionsAsync(
        CancellationToken cancellationToken)
    {
        var defaultDays = options.Value.DefaultRetentionDays;
        return await dbContext.Instances
            .Select(i => new InstanceRetention(i.Id, i.RetentionDays ?? defaultDays))
            .ToListAsync(cancellationToken);
    }
}
