namespace Bugler.Access.ResetPassword;

/// <summary>Why a presented Reset Ticket was or was not allowed to buy a new password.</summary>
internal enum Redemption
{
    /// <summary>No ticket carries this fingerprint — never issued, or already replaced by a newer one.</summary>
    Unknown,

    /// <summary>Issued, but its hour has passed.</summary>
    Expired,

    /// <summary>Already spent. A Reset Ticket buys exactly one password.</summary>
    AlreadyUsed,

    /// <summary>The User behind it cannot sign in, so neither can their ticket let anyone in.</summary>
    UserCannotSignIn,

    Accepted,
}

/// <summary>
/// The whole rule for redeeming a Reset Ticket, kept apart from the database so every branch can
/// be stated plainly. The caller answers the same way to everything but <see cref="Redemption.Accepted"/> —
/// whoever holds the secret learns nothing from the distinction, and the log is clearer for it.
/// </summary>
internal static class RedemptionDecision
{
    public static Redemption Decide(TicketState? ticket, DateTimeOffset now)
    {
        if (ticket is not { } state)
        {
            return Redemption.Unknown;
        }

        // Spent before expired: a ticket used at 12:00 and presented again at 14:00 was not
        // "too late", it was already gone.
        if (state.ConsumedAt is not null)
        {
            return Redemption.AlreadyUsed;
        }

        if (state.ExpiresAt <= now)
        {
            return Redemption.Expired;
        }

        // A deactivated User does not get in through the back door either: the ticket may be
        // perfectly valid and still buy nothing, exactly as their password would.
        return state.UserIsActive ? Redemption.Accepted : Redemption.UserCannotSignIn;
    }

    /// <summary>What redemption needs to know about a ticket and the User behind it.</summary>
    public readonly record struct TicketState(
        DateTimeOffset ExpiresAt, DateTimeOffset? ConsumedAt, bool UserIsActive);
}
