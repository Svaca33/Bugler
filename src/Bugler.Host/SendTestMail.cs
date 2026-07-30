using System.Security.Claims;
using Bugler.Mail;

namespace Bugler.Host;

/// <summary>Where the test message went, so the screen can say it rather than guess.</summary>
public sealed record TestMailResult(string SentTo);

/// <summary>
/// Proves the mail path out of this deployment while somebody is still standing at the screen.
/// Everything else Bugler sends leaves through a queue and reports a refusal to the container log
/// alone, so a relay that will not carry Bugler's mail looks exactly like a quiet week — until the
/// first real incident goes unannounced weeks later.
///
/// A deployment concern rather than any context's business, so it lives here beside /health: Mail
/// stays the transport that never learns what a message means (ADR 0011).
/// </summary>
internal static class SendTestMail
{
    private const string Body =
        """
        This is a test message from Bugler.

        Somebody signed in as an administrator asked for it, to confirm this server can send mail
        at all. If it reached you, alerts and password-reset links will reach their recipients too.
        """;

    public static async Task<IResult> Handle(
        ClaimsPrincipal principal,
        IMailSender sender,
        CancellationToken cancellationToken)
    {
        // Their own address, never one they type: a form that mails anywhere is a relay for
        // whoever gets hold of an admin session.
        var address = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(address))
        {
            return Results.Problem(
                title: "This session carries no address to send to.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            // Sent rather than queued: waiting for the outcome is the entire point.
            await sender.SendAsync(
                new MailMessage(address, "Bugler test message", Body), cancellationToken);
            return Results.Ok(new TestMailResult(address));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever the mail server said, said back. An operator setting this up needs the
            // refusal itself — "sending failed" would send them to the container log anyway.
            return Results.Problem(
                title: "The message could not be sent.",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
