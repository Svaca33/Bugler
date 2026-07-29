using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ManageAlertingSettings;

public sealed record AlertingDefaultsDto(Sensitivity Sensitivity, int QuietWindowMinutes);

/// <summary>The webhook is a secret: after saving, only its host ever comes back.</summary>
public sealed record ChatWebhookDto(string Domain);

public sealed record ServiceAlertingOverrideDto(
    Guid ServiceId, Sensitivity? Sensitivity, int? QuietWindowMinutes);

public sealed record ApplicationAlertingDto(
    Guid ApplicationId,
    Sensitivity? Sensitivity,
    int? QuietWindowMinutes,
    ChatWebhookDto? ChatWebhook,
    IReadOnlyList<ServiceAlertingOverrideDto> ServiceOverrides,
    AlertingDefaultsDto Defaults);

public sealed record SetApplicationAlertingRequest(Sensitivity? Sensitivity, int? QuietWindowMinutes);

public sealed record SetChatWebhookRequest(string? Url);

public sealed record SetServiceAlertingRequest(Sensitivity? Sensitivity, int? QuietWindowMinutes);

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
                s.ServiceId.Value, s.Sensitivity, s.QuietWindowMinutes))
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
        CancellationToken cancellationToken)
    {
        if (request.QuietWindowMinutes is < 1)
        {
            return Results.BadRequest("The quiet window must be at least 1 minute.");
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

        if (request.Sensitivity is null && request.QuietWindowMinutes is null)
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
            overrides.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await CloseNewlyMutedAsync(
            dbContext, catalogReader, registered.ApplicationId, cancellationToken, catalog);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Sensitivity turning Off acts immediately: the Application's now-muted Services get their
    /// open Episodes closed silently, inside the same transaction as the settings change (which
    /// is saved before this runs, so the queries here see it). The periodic closer stays as the
    /// net for a detect run racing this request.
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

        var effective = EffectiveSettings.Build(applicationServices, applicationSettings, serviceSettings);
        await SilentClose.ApplyAsync(
            dbContext, effective.ServicesEffectivelyOff(applicationId), DateTimeOffset.UtcNow,
            cancellationToken);
    }

    private static ChatWebhookDto? ToWebhookDto(string? url) =>
        url is null ? null : new ChatWebhookDto(new Uri(url).Host);
}
