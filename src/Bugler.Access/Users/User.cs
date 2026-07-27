namespace Bugler.Access.Users;

/// <summary>A person with a local Bugler account.</summary>
public sealed class User
{
    public required Guid Id { get; init; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public required bool IsAdmin { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}
