using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Subscriptions;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ManageSubscriptions;

public sealed record SubscriptionsDto(
    IReadOnlyList<Guid> ApplicationIds, IReadOnlyList<Guid> ServiceIds);

public sealed record SetSubscriptionsRequest(
    IReadOnlyList<Guid> ApplicationIds, IReadOnlyList<Guid> ServiceIds);

internal static class SubscriptionEndpoints
{
    public static async Task<IResult> GetOwn(
        ClaimsPrincipal principal, AlertingDbContext dbContext, CancellationToken cancellationToken)
    {
        if (GetUserId(principal) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var own = await dbContext.Subscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        return Results.Ok(new SubscriptionsDto(
            own.Where(s => s.ApplicationId is not null).Select(s => s.ApplicationId!.Value.Value).ToList(),
            own.Where(s => s.ServiceId is not null).Select(s => s.ServiceId!.Value.Value).ToList()));
    }

    /// <summary>
    /// Replaces the caller's Subscriptions with the given set — every checkbox click sends the
    /// next full picture, so there is nothing to merge and repeating a request changes nothing.
    /// </summary>
    public static async Task<IResult> SetOwn(
        SetSubscriptionsRequest request,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IReadApplicationFocus readFocus,
        ICatalogReader catalogReader,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        if (GetUserId(principal) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var applicationIds = request.ApplicationIds.Distinct().Select(id => new ApplicationId(id)).ToList();
        var serviceIds = request.ServiceIds.Distinct().Select(id => new ServiceId(id)).ToList();

        // A Subscription may only point inside the caller's Visibility Scope, and a Service
        // subscription only at a registered Service. Null visibility = an Admin, unrestricted.
        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        var catalog = await catalogReader.GetServicesAsync(cancellationToken);
        var applicationOf = catalog.ToDictionary(s => s.Id, s => s.ApplicationId);

        if (visible is not null && applicationIds.Any(id => !visible.Contains(id)))
        {
            var messages = AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.BadRequest(messages.ApplicationOutsideVisibility);
        }

        foreach (var serviceId in serviceIds)
        {
            if (!applicationOf.TryGetValue(serviceId, out var applicationId))
            {
                var messages = AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken));
                return Results.BadRequest(messages.UnregisteredService);
            }

            if (visible is not null && !visible.Contains(applicationId))
            {
                var messages = AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken));
                return Results.BadRequest(messages.ServiceOutsideVisibility);
            }
        }

        var existing = await dbContext.Subscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        var wantedApplications = applicationIds.ToHashSet();
        var wantedServices = serviceIds.ToHashSet();

        // "The next full picture" is only ever a picture of what the sender could see. The panel
        // is drawn from the focused catalog, so a Subscription outside the caller's Focus is
        // missing from this request because it was never offered — not because it was unticked.
        // Silence about it must therefore mean nothing at all: it is kept, and speaks again when
        // the Focus widens (Access ADR 0004).
        var focused = await readFocus.GetFocusedApplicationsAsync(cancellationToken);

        foreach (var subscription in existing)
        {
            var stillWanted = subscription.ApplicationId is { } app
                ? wantedApplications.Remove(app)
                : subscription.ServiceId is { } service && wantedServices.Remove(service);
            if (stillWanted || !WasOnOffer(subscription, focused, applicationOf))
            {
                continue;
            }

            dbContext.Subscriptions.Remove(subscription);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var applicationId in wantedApplications)
        {
            dbContext.Subscriptions.Add(new Subscription
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                ApplicationId = applicationId,
                CreatedAt = now,
            });
        }

        foreach (var serviceId in wantedServices)
        {
            dbContext.Subscriptions.Add(new Subscription
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                ServiceId = serviceId,
                CreatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Whether this Subscription's Application stood among the ones the caller was shown, and so
    /// whether its absence from their request is a decision. A Subscription pointing at a Service
    /// that is no longer registered was on no offer either, and is left for the Deletion handlers.
    /// </summary>
    private static bool WasOnOffer(
        Subscription subscription,
        IReadOnlyCollection<ApplicationId>? focused,
        IReadOnlyDictionary<ServiceId, ApplicationId> applicationOf)
    {
        if (focused is null)
        {
            return true;
        }

        if (subscription.ApplicationId is { } application)
        {
            return focused.Contains(application);
        }

        return subscription.ServiceId is { } service
            && applicationOf.TryGetValue(service, out var owner)
            && focused.Contains(owner);
    }

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
