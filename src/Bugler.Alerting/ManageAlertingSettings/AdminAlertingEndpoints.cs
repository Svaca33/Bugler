using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Alerting.WatchHealthChecks;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ManageAlertingSettings;

public sealed record AlertingDefaultsDto(Sensitivity Sensitivity, int QuietWindowMinutes);

/// <summary>The webhook is a secret: after saving, only its host ever comes back.</summary>
public sealed record ChatWebhookDto(string Domain);

public sealed record ServiceAlertingOverrideDto(
    Guid ServiceId, Sensitivity? Sensitivity, int? QuietWindowMinutes, string? HealthCheckUrl);

public sealed record ApplicationAlertingDto(
    Guid ApplicationId,
    Sensitivity? Sensitivity,
    int? QuietWindowMinutes,
    ChatWebhookDto? ChatWebhook,
    IReadOnlyList<ServiceAlertingOverrideDto> ServiceOverrides,
    AlertingDefaultsDto Defaults);

public sealed record SetApplicationAlertingRequest(Sensitivity? Sensitivity, int? QuietWindowMinutes);

public sealed record SetChatWebhookRequest(string? Url);

public sealed record SetServiceAlertingRequest(
    Sensitivity? Sensitivity, int? QuietWindowMinutes, string? HealthCheckUrl);

/// <summary>What one trial probe saw, so a wrong address shows itself now rather than at 3am.</summary>
public sealed record HealthCheckProbeDto(bool Alive, string Detail);

/// <summary>The answer to saving a Service's settings; the probe is null when no address is set.</summary>
public sealed record SetServiceAlertingResponse(HealthCheckProbeDto? HealthCheck);

internal static class AdminAlertingEndpoints
{
    public static async Task<ApplicationAlertingDto> GetApplicationAlerting(
        Guid applicationId, AlertingDbContext dbContext, CancellationToken cancellationToken)
    {
        var id = new ApplicationId(applicationId);
        var settings = await dbContext.ApplicationSettings
            .FirstOrDefaultAsync(s => s.ApplicationId == id, cancellationToken);
        var overrides = await dbContext.ServiceSettings
            .Where(s => s.ApplicationId == id)
            .Select(s => new ServiceAlertingOverrideDto(
                s.ServiceId.Value, s.Sensitivity, s.QuietWindowMinutes, s.HealthCheckUrl))
            .ToListAsync(cancellationToken);

        return new ApplicationAlertingDto(
            applicationId,
            settings?.Sensitivity,
            settings?.QuietWindowMinutes,
            ToWebhookDto(settings?.ChatWebhookUrl),
            overrides,
            new AlertingDefaultsDto(AlertingDefaults.Sensitivity, AlertingDefaults.QuietWindowMinutes));
    }

    public static async Task<IResult> SetApplicationAlerting(
        Guid applicationId,
        SetApplicationAlertingRequest request,
        AlertingDbContext dbContext,
        ICatalogReader catalogReader,
        CancellationToken cancellationToken)
    {
        if (request.QuietWindowMinutes is < 1)
        {
            return Results.BadRequest("The quiet window must be at least 1 minute.");
        }

        var id = new ApplicationId(applicationId);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var settings = await dbContext.ApplicationSettings
            .FirstOrDefaultAsync(s => s.ApplicationId == id, cancellationToken);
        if (settings is null)
        {
            settings = new ApplicationAlertingSettings { ApplicationId = id };
            dbContext.ApplicationSettings.Add(settings);
        }

        settings.Sensitivity = request.Sensitivity;
        settings.QuietWindowMinutes = request.QuietWindowMinutes;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await CloseNewlyMutedAsync(dbContext, catalogReader, id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    public static async Task<IResult> SetChatWebhook(
        Guid applicationId,
        SetChatWebhookRequest request,
        AlertingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim();
        if (url is not null
            && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                || parsed.Scheme != Uri.UriSchemeHttps
                || url.Length > 1000))
        {
            return Results.BadRequest("The webhook must be an absolute https URL.");
        }

        var id = new ApplicationId(applicationId);
        var settings = await dbContext.ApplicationSettings
            .FirstOrDefaultAsync(s => s.ApplicationId == id, cancellationToken);
        if (settings is null)
        {
            if (url is null)
            {
                return Results.NoContent(); // Clearing what was never set.
            }

            settings = new ApplicationAlertingSettings { ApplicationId = id };
            dbContext.ApplicationSettings.Add(settings);
        }

        settings.ChatWebhookUrl = url;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    public static async Task<IResult> SetServiceAlerting(
        Guid serviceId,
        SetServiceAlertingRequest request,
        AlertingDbContext dbContext,
        ICatalogReader catalogReader,
        HealthProbe healthProbe,
        CancellationToken cancellationToken)
    {
        if (request.QuietWindowMinutes is < 1)
        {
            return Results.BadRequest("The quiet window must be at least 1 minute.");
        }

        var healthCheckUrl = string.IsNullOrWhiteSpace(request.HealthCheckUrl)
            ? null
            : request.HealthCheckUrl.Trim();
        // Deliberately looser than the Chat Webhook's rule: a self-hosted Bugler usually shares a
        // network with what it watches, so http://backend:8080/health is the ordinary case rather
        // than the exception, and demanding https would kill the feature where it belongs.
        if (healthCheckUrl is not null
            && (!Uri.TryCreate(healthCheckUrl, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
                || healthCheckUrl.Length > 500))
        {
            return Results.BadRequest("The health check must be an absolute http or https URL.");
        }

        var id = new ServiceId(serviceId);
        var catalog = await catalogReader.GetServicesAsync(cancellationToken);
        var registered = catalog.FirstOrDefault(s => s.Id == id);
        if (registered is null)
        {
            return Results.NotFound();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var overrides = await dbContext.ServiceSettings
            .FirstOrDefaultAsync(s => s.ServiceId == id, cancellationToken);

        if (request.Sensitivity is null && request.QuietWindowMinutes is null && healthCheckUrl is null)
        {
            // Nothing overridden: the row goes, so "absent = inherit" stays the single truth.
            if (overrides is not null)
            {
                dbContext.ServiceSettings.Remove(overrides);
            }
        }
        else
        {
            if (overrides is null)
            {
                overrides = new ServiceAlertingSettings
                {
                    ServiceId = id,
                    ApplicationId = registered.ApplicationId,
                };
                dbContext.ServiceSettings.Add(overrides);
            }

            overrides.Sensitivity = request.Sensitivity;
            overrides.QuietWindowMinutes = request.QuietWindowMinutes;
            overrides.HealthCheckUrl = healthCheckUrl;
            overrides.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await CloseNewlyMutedAsync(
            dbContext, catalogReader, registered.ApplicationId, cancellationToken, catalog);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Outside the transaction: the settings are saved either way, and an address that cannot
        // be reached right now is still the address the admin meant. The answer only reports.
        var probe = healthCheckUrl is null
            ? null
            : ToProbeDto(await healthProbe.ProbeAsync(healthCheckUrl, cancellationToken));
        return Results.Ok(new SetServiceAlertingResponse(probe));
    }

    /// <summary>
    /// A Watch turning off acts immediately: the Application's Services whose Sensitivity is now
    /// Off, and those whose health check address is now gone, get that Watch's open Episodes
    /// closed silently — inside the same transaction as the settings change (which is saved
    /// before this runs, so the queries here see it). Each Watch is closed on its own, so muting
    /// logs never touches an Episode the health check is still feeding. The periodic closer stays
    /// as the net for a sweep racing this request.
    /// </summary>
    private static async Task CloseNewlyMutedAsync(
        AlertingDbContext dbContext,
        ICatalogReader catalogReader,
        ApplicationId applicationId,
        CancellationToken cancellationToken,
        IReadOnlyList<CatalogService>? catalog = null)
    {
        catalog ??= await catalogReader.GetServicesAsync(cancellationToken);
        var applicationServices = catalog.Where(s => s.ApplicationId == applicationId).ToList();
        if (applicationServices.Count == 0)
        {
            return;
        }

        var applicationSettings = await dbContext.ApplicationSettings
            .Where(s => s.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);
        var serviceSettings = await dbContext.ServiceSettings
            .Where(s => s.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);

        var fingerprintWindows = await dbContext.FingerprintQuietWindows
            .Where(w => w.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);

        var effective = EffectiveSettings.Build(
            applicationServices, applicationSettings, serviceSettings, fingerprintWindows);
        var now = DateTimeOffset.UtcNow;
        await SilentClose.ApplyAsync(
            dbContext, effective.ServicesEffectivelyOff(applicationId), Watch.Logs, now,
            cancellationToken);
        await SilentClose.ApplyAsync(
            dbContext, effective.ServicesWithoutHealthCheck(applicationId), Watch.HealthCheck, now,
            cancellationToken);
    }

    private static ChatWebhookDto? ToWebhookDto(string? url) =>
        url is null ? null : new ChatWebhookDto(new Uri(url).Host);

    private static HealthCheckProbeDto ToProbeDto(ProbeOutcome outcome) =>
        new(outcome.Alive, outcome.Detail);
}
