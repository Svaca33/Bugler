namespace Bugler.Access.Users;

/// <summary>
/// One User's single-use permission to set a new password (CONTEXT.md: Reset Ticket). The secret
/// that proves it left in the mail and was never written down; what stays here is its fingerprint —
/// enough to recognise a ticket presented back, not enough to forge one.
/// </summary>
public sealed class ResetTicket
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }

    /// <summary>SHA-256 of the secret, exactly as an API key is kept in the Registry.</summary>
    public required byte[] Fingerprint { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Set the moment the ticket buys a new password; a second attempt finds it spent.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
