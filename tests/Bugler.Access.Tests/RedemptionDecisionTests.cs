using Bugler.Access.ResetPassword;

namespace Bugler.Access.Tests;

/// <summary>Every rule a presented Reset Ticket is judged by, stated without a database.</summary>
public class RedemptionDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_fingerprint_nobody_knows_buys_nothing() =>
        Assert.Equal(Redemption.Unknown, RedemptionDecision.Decide(ticket: null, Now));

    [Fact]
    public void A_live_ticket_of_an_active_user_is_accepted() =>
        Assert.Equal(Redemption.Accepted, RedemptionDecision.Decide(Ticket(), Now));

    [Fact]
    public void A_ticket_whose_hour_has_passed_is_expired() =>
        Assert.Equal(
            Redemption.Expired,
            RedemptionDecision.Decide(Ticket(expiresAt: Now.AddSeconds(-1)), Now));

    /// <summary>The boundary belongs to expiry: at the very instant it runs out, it has run out.</summary>
    [Fact]
    public void A_ticket_expiring_exactly_now_is_expired() =>
        Assert.Equal(Redemption.Expired, RedemptionDecision.Decide(Ticket(expiresAt: Now), Now));

    [Fact]
    public void A_spent_ticket_cannot_be_spent_again() =>
        Assert.Equal(
            Redemption.AlreadyUsed,
            RedemptionDecision.Decide(Ticket(consumedAt: Now.AddMinutes(-5)), Now));

    /// <summary>
    /// Spent beats expired: a ticket used at noon and presented at two was not too late, it was
    /// already gone. The distinction never reaches the caller but it is the truthful one to log.
    /// </summary>
    [Fact]
    public void A_ticket_both_spent_and_expired_reads_as_spent() =>
        Assert.Equal(
            Redemption.AlreadyUsed,
            RedemptionDecision.Decide(
                Ticket(expiresAt: Now.AddHours(-1), consumedAt: Now.AddHours(-2)), Now));

    /// <summary>A deactivated User does not get in through the back door their own password is refused at.</summary>
    [Fact]
    public void A_perfectly_good_ticket_of_a_deactivated_user_buys_nothing() =>
        Assert.Equal(
            Redemption.UserCannotSignIn,
            RedemptionDecision.Decide(Ticket(userIsActive: false), Now));

    private static RedemptionDecision.TicketState Ticket(
        DateTimeOffset? expiresAt = null, DateTimeOffset? consumedAt = null, bool userIsActive = true) =>
        new(expiresAt ?? Now.AddMinutes(30), consumedAt, userIsActive);
}
