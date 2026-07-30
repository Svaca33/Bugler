namespace Bugler.Access.Users;

/// <summary>A person with a local Bugler account.</summary>
public sealed class User
{
    public required Guid Id { get; init; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Rolled whenever the password is written. A Session carries the stamp it was minted from,
    /// so changing the password ends every Session that came before it (CONTEXT.md: Session).
    /// </summary>
    public required Guid SecurityStamp { get; set; }

    public string? DisplayName { get; set; }
    public required bool IsAdmin { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}
