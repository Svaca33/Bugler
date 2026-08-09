using Bugler.SharedKernel;

namespace Bugler.Access.Users;

/// <summary>
/// A User's own reading, lent to a tool on their machine (CONTEXT.md: Machine Delegation). The Secret that
/// proves it was shown once at issue and never written down; what stays here is its fingerprint —
/// enough to recognise a Machine Delegation presented back, not enough to forge one, exactly as a Reset
/// Ticket keeps its own (ADR 0029).
/// </summary>
public sealed class MachineDelegation
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }

    /// <summary>
    /// What its holder called it, so a list of them can be told apart — the laptop, the desktop,
    /// the machine in the office. Bugler never reads it for anything.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>SHA-256 of the Secret, exactly as an API Key and a Reset Ticket are kept.</summary>
    public required byte[] Fingerprint { get; init; }

    /// <summary>
    /// The one Application this Machine Delegation was narrowed to, or null for the whole of its User's
    /// Visibility Scope. A narrowing only ever subtracts: what the User may not read, this cannot
    /// read either, and the Application named here is checked against their Scope at every query
    /// rather than trusted from the day it was issued.
    /// </summary>
    public ApplicationId? ApplicationId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When it stops being worth presenting. Not forever, because the Secret lives in a
    /// configuration file on somebody's laptop and time is what bounds a leak nobody noticed.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// When it was last presented, written back coarsely (see MachineDelegationAuthentication). Without it
    /// a list of Machine Delegations only grows: nobody can tell which of three lines is the dead one.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Set when its User withdrew it, or when an Admin did — the narrower answer to a lost laptop
    /// than deactivating the person it belongs to.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
