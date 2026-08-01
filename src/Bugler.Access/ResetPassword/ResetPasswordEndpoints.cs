using Bugler.Access.Users;
using Bugler.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Access.ResetPassword;

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

internal static class ResetPasswordEndpoints
{
    /// <summary>
    /// One answer for every outcome. Whoever asks must not learn whether the address belongs to an
    /// account — the sign-in form already refuses to say so, and a side door that did would make
    /// that refusal pointless.
    /// </summary>
    private const string SameAnswerToEverybody =
        "If an account uses that address, a link to set a new password is on its way to it.";

    /// <summary>
    /// Said to whoever presents a ticket that buys nothing, whatever is wrong with it. They hold
    /// the secret, so the distinction would leak nothing — but a single sentence is the one that
    /// says the useful thing: ask again.
    /// </summary>
    private const string LinkNoLongerWorks =
        "This link no longer works. Ask for a new one and use the newest mail.";

    public static async Task<IResult> Forgot(
        ForgotPasswordRequest request,
        AccessDbContext dbContext,
        IMailQueue mail,
        IOptions<AccessOptions> options,
        ISmtpSettingsSource smtpSettings,
        ILogger<ResetTicket> logger,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable(options.Value, await smtpSettings.GetCurrentAsync(cancellationToken)))
        {
            // Nothing about any User is given away by admitting this server cannot send mail —
            // and a button that always promises a link nobody ever receives is worse.
            return Results.NotFound("This server cannot send mail, so passwords cannot be reset by link.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.DeactivatedAt == null, cancellationToken);
        if (user is not null)
        {
            await IssueAndSendAsync(user, dbContext, mail, options.Value.PublicBaseUrl, logger, cancellationToken);
        }

        return Results.Accepted(value: SameAnswerToEverybody);
    }

    public static async Task<IResult> Reset(
        ResetPasswordRequest request,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        CancellationToken cancellationToken)
    {
        var fingerprint = ResetTickets.Fingerprint(request.Token ?? "");
        var ticket = await dbContext.ResetTickets
            .FirstOrDefaultAsync(t => t.Fingerprint == fingerprint, cancellationToken);
        var user = ticket is null
            ? null
            : await dbContext.Users.FirstOrDefaultAsync(u => u.Id == ticket.UserId, cancellationToken);

        var state = ticket is null
            ? (RedemptionDecision.TicketState?)null
            : new RedemptionDecision.TicketState(
                ticket.ExpiresAt, ticket.ConsumedAt, user is { DeactivatedAt: null });

        if (RedemptionDecision.Decide(state, DateTimeOffset.UtcNow) != Redemption.Accepted)
        {
            return Results.BadRequest(LinkNoLongerWorks);
        }

        // Only now, with a ticket that would have worked: a password Bugler refuses must not
        // spend the one thing standing between this person and their account.
        if (!Passwords.IsAcceptable(request.NewPassword))
        {
            return Results.BadRequest(Passwords.Requirement);
        }

        Passwords.Set(user!, request.NewPassword, hasher);
        ticket!.ConsumedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // The rolled stamp ends every Session of this User. None is spared: whoever just asked
        // for this link was, by their own account, holding none.
        return Results.NoContent();
    }

    /// <summary>
    /// Issues a Reset Ticket and hands the mail to the queue. Also the whole of the throttle: a
    /// mail sent minutes ago still works, so a second ask within the window issues nothing and
    /// quietly leaves the first one standing.
    /// </summary>
    internal static async Task<string?> IssueAsync(
        User user, AccessDbContext dbContext, bool respectInterval, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (respectInterval)
        {
            var recently = now - ResetTickets.MailInterval;
            var standing = await dbContext.ResetTickets.AnyAsync(
                t => t.UserId == user.Id
                    && t.ConsumedAt == null
                    && t.ExpiresAt > now
                    && t.CreatedAt > recently,
                cancellationToken);
            if (standing)
            {
                return null;
            }
        }

        // The newest mail is always the one that works (CONTEXT.md: Reset Ticket). Deleting the
        // rest is the tidying-up as well — nothing else ever has to sweep this table.
        await dbContext.ResetTickets.Where(t => t.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var secret = ResetTickets.NewSecret();
        dbContext.ResetTickets.Add(new ResetTicket
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Fingerprint = ResetTickets.Fingerprint(secret),
            CreatedAt = now,
            ExpiresAt = now + ResetTickets.Lifetime,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return secret;
    }

    internal static bool IsAvailable(AccessOptions access, SmtpSettings smtp) =>
        smtp.IsConfigured && access.PublicBaseUrl.Length > 0;

    private static async Task IssueAndSendAsync(
        User user,
        AccessDbContext dbContext,
        IMailQueue mail,
        string publicBaseUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var secret = await IssueAsync(user, dbContext, respectInterval: true, cancellationToken);
        if (secret is null)
        {
            return;
        }

        if (!mail.TryEnqueue(ResetMail.Compose(user.Email, secret, publicBaseUrl)))
        {
            // The secret exists only in this method: nothing can pick this up later, and the
            // caller was told the same sentence either way. Asking again in two minutes works.
            logger.LogWarning("The mail queue is full; a password reset link was not sent");
        }
    }
}
