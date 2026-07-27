using Bugler.Registry.Catalog;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.ManageApiKeys;

public sealed record ApiKeyDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

/// <summary>The only response that ever carries the plaintext key.</summary>
public sealed record IssuedApiKeyDto(Guid Id, string Plaintext);

internal static class AdminApiKeyEndpoints
{
    public static async Task<IResult> ListKeys(
        Guid id, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var instanceId = new InstanceId(id);
        if (!await dbContext.Instances.AnyAsync(i => i.Id == instanceId, cancellationToken))
        {
            return Results.NotFound();
        }

        var keys = await dbContext.ApiKeys
            .Where(k => k.InstanceId == instanceId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(k.Id, k.CreatedAt, k.RevokedAt))
            .ToListAsync(cancellationToken);
        return Results.Ok(keys);
    }

    public static async Task<IResult> IssueKey(
        Guid id, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var instanceId = new InstanceId(id);
        if (!await dbContext.Instances.AnyAsync(i => i.Id == instanceId, cancellationToken))
        {
            return Results.NotFound();
        }

        var plaintext = ApiKeyMaterial.GeneratePlaintext();
        var key = new ApiKey
        {
            Id = Guid.CreateVersion7(),
            InstanceId = instanceId,
            KeyHash = ApiKeyMaterial.Hash(plaintext),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.ApiKeys.Add(key);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new IssuedApiKeyDto(key.Id, plaintext));
    }

    public static async Task<IResult> RevokeKey(
        Guid id, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var key = await dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is null)
        {
            return Results.NotFound();
        }

        key.RevokedAt ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
