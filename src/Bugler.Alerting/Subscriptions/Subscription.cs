using Bugler.SharedKernel;

namespace Bugler.Alerting.Subscriptions;

/// <summary>
/// A User's standing personal request to be mailed Alerts and All Clears (see CONTEXT.md) — for
/// one Application (all its Services, present and future) or for one Service, never both. Access
/// is not checked here: a Subscription is dormant, not dead, while its User cannot read the
/// Application, and the check happens at delivery time.
/// </summary>
public sealed class Subscription
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public ApplicationId? ApplicationId { get; init; }
    public ServiceId? ServiceId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
