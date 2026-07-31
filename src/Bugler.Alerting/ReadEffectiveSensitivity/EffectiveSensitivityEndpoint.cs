using Bugler.Access.Contracts;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ReadEffectiveSensitivity;

public sealed record ServiceSensitivityDto(Guid ServiceId, bool Off);

public sealed record ListSensitivityResponse(IReadOnlyList<ServiceSensitivityDto> Items);

internal static class EffectiveSensitivityEndpoint
{
    /// <summary>
    /// Whether each visible Service currently alerts at all — the one bit Subscriptions needs to
    /// keep a reader from subscribing to silence. The numbers behind it stay in Admin: whether a
    /// Service is watched is no secret to someone already allowed to subscribe to it.
    /// </summary>
    public static async Task<IResult> Handle(
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        ICatalogReader catalogReader,
        CancellationToken cancellationToken)
    {
        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        var catalog = await catalogReader.GetServicesAsync(cancellationToken);
        var services = visible is null
            ? catalog
            : catalog.Where(s => visible.Contains(s.ApplicationId)).ToList();
        if (services.Count == 0)
        {
            return Results.Ok(new ListSensitivityResponse([]));
        }

        var effective = EffectiveSettings.Build(
            catalog,
            await dbContext.ApplicationSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.ServiceSettings.AsNoTracking().ToListAsync(cancellationToken));

        return Results.Ok(new ListSensitivityResponse(services
            .Select(s => new ServiceSensitivityDto(
                s.Id.Value, effective.SensitivityOf(s.Id) == Sensitivity.Off))
            .ToList()));
    }
}
