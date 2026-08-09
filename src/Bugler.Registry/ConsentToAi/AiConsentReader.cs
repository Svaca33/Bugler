using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.ConsentToAi;

internal sealed class AiConsentReader(RegistryDbContext dbContext) : IAiConsentReader
{
    public async ValueTask<bool> HasConsentAsync(
        ApplicationId applicationId, CancellationToken cancellationToken) =>
        await dbContext.Applications
            .Where(a => a.Id == applicationId)
            .Select(a => a.AiConsent)
            .FirstOrDefaultAsync(cancellationToken);
}
