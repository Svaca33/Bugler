namespace Bugler.Access.Users;

/// <summary>The permission for one User to read the telemetry of one Application.</summary>
public sealed class ApplicationGrant
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required ApplicationId ApplicationId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
