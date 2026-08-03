using Bugler.Mail;

namespace Bugler.Access.ResetPassword;

/// <summary>
/// The words Access sends. They live here rather than in the transport because what a mail says
/// is the language of the context that sends it (ADR 0011).
/// </summary>
internal static class ResetMail
{
    public static MailMessage Compose(
        string toEmail, string secret, string publicBaseUrl, AccessMessages messages) =>
        new(
            toEmail,
            messages.ResetMailSubject,
            messages.ResetMailBody(Link(secret, publicBaseUrl)));

    /// <summary>
    /// The link points at the page, not at the API: a click from a mail is a GET, and what has to
    /// come back is a form asking for the new password.
    /// </summary>
    public static string Link(string secret, string publicBaseUrl) =>
        $"{publicBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(secret)}";
}
