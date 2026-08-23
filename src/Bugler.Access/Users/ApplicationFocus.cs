namespace Bugler.Access.Users;

/// <summary>
/// One Application a User has chosen to attend to (see CONTEXT.md: Focus). A row per choice
/// rather than a list on the User, so an Application's Deletion is swept exactly as a grant is —
/// and so "attending to nothing" needs no marker: it is simply no rows.
/// </summary>
public sealed class ApplicationFocus
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required ApplicationId ApplicationId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
