using Bugler.Ai;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Host;

/// <summary>
/// AI settings as the admin screen saved them, read fresh at every ask so a save applies to the
/// very next completion. While nothing was ever saved there, defers to the Ai configuration
/// section — whole, never field by field (ADR 0027, on ADR 0014's terms).
/// </summary>
internal sealed class StoredAiSettingsSource(
    IServiceScopeFactory scopeFactory,
    ConfigurationAiSettingsSource fallback) : IAiSettingsSource
{
    public async ValueTask<AiSettings> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // A scope per ask: the callers are singletons (the transport, background loops) and the
        // DbContext must not be.
        await using var scope = scopeFactory.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServerDbContext>()
            .AiSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return stored is null
            ? await fallback.GetCurrentAsync(cancellationToken)
            : new AiSettings(
                stored.Provider, stored.BaseUrl, stored.ApiKey, stored.Model, stored.PatienceSeconds);
    }
}
