using Bugler.Registry.Catalog;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.ApiKeyValidation;

internal sealed class ApiKeyValidator(RegistryDbContext dbContext) : IApiKeyValidator
{
    public async ValueTask<InstanceId?> ValidateAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (!apiKey.StartsWith(ApiKeyMaterial.Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var hash = ApiKeyMaterial.Hash(apiKey);
        var instanceId = await dbContext.ApiKeys
            .Where(k => k.KeyHash == hash && k.RevokedAt == null)
            .Select(k => (InstanceId?)k.InstanceId)
            .FirstOrDefaultAsync(cancellationToken);

        return instanceId;
    }
}
