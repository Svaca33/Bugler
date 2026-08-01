using Bugler.Alerting.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ManageAlertingSettings;

/// <summary>Null means inherit — the row goes, so "absent = inherit" stays the single truth.</summary>
public sealed record SetFingerprintQuietWindowRequest(int? QuietWindowMinutes);

/// <summary>
/// The Quiet Window of one kind of trouble in one Service (ADR 0004), addressed through an
/// Episode: the Episode names the (Service, Fingerprint) pair, so no caller ever handles a
/// 300-character Fingerprint as a value. What is set outlives the Episode it was set from and
/// governs every later Episode of that kind — which is why any Episode will do, open or closed.
/// </summary>
internal static class FingerprintQuietWindowEndpoint
{
    public static async Task<IResult> Set(
        Guid id,
        SetFingerprintQuietWindowRequest request,
        AlertingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.QuietWindowMinutes is { } requested
            && (requested < 1 || requested > FingerprintQuietWindow.MaxMinutes))
        {
            return Results.BadRequest(
                $"The quiet window must be between 1 and {FingerprintQuietWindow.MaxMinutes} minutes.");
        }

        var episode = await dbContext.Episodes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (episode is null)
        {
            return Results.NotFound();
        }

        if (episode.Fingerprint.Length == 0)
        {
            // Episodes from before ADR 0002 carry no Fingerprint and never take new matches
            // (that ADR's third consequence), so a window here would be configuration for
            // trouble that can no longer recur.
            return Results.BadRequest(
                "This episode predates grouping by kind of trouble and cannot carry a quiet window.");
        }

        var existing = await dbContext.FingerprintQuietWindows.FirstOrDefaultAsync(
            w => w.ServiceId == episode.ServiceId && w.Fingerprint == episode.Fingerprint,
            cancellationToken);

        if (request.QuietWindowMinutes is not { } minutes)
        {
            if (existing is not null)
            {
                dbContext.FingerprintQuietWindows.Remove(existing);
            }
        }
        else if (existing is null)
        {
            dbContext.FingerprintQuietWindows.Add(new FingerprintQuietWindow
            {
                ServiceId = episode.ServiceId,
                Fingerprint = episode.Fingerprint,
                ApplicationId = episode.ApplicationId,
                QuietWindowMinutes = minutes,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.QuietWindowMinutes = minutes;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
