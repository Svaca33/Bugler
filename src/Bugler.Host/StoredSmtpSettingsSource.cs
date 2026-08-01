using Bugler.Mail;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Host;

/// <summary>
/// SMTP settings as the admin screen saved them, read fresh at every ask so a save applies to the
/// very next send. While nothing was ever saved there, defers to the Mail:Smtp configuration
/// section — whole, never field by field (ADR 0014).
/// </summary>
internal sealed class StoredSmtpSettingsSource(
    IServiceScopeFactory scopeFactory,
    ConfigurationSmtpSettingsSource fallback) : ISmtpSettingsSource
{
    public async ValueTask<SmtpSettings> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // A scope per ask: the callers are singletons (the transport, background loops) and the
        // DbContext must not be.
        await using var scope = scopeFactory.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServerDbContext>()
            .SmtpSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return stored is null
            ? await fallback.GetCurrentAsync(cancellationToken)
            : new SmtpSettings(
                stored.Host, stored.Port, stored.Security, stored.Username, stored.Password, stored.From);
    }
}
