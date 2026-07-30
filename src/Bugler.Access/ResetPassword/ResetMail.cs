using Bugler.Mail;

namespace Bugler.Access.ResetPassword;

/// <summary>
/// The words Access sends. They live here rather than in the transport because what a mail says
/// is the language of the context that sends it (ADR 0011).
/// </summary>
internal static class ResetMail
{
    public static MailMessage Compose(string toEmail, string secret, string publicBaseUrl) =>
        new(
            toEmail,
            "[Bugler] Set a new password",
            string.Join(
                "\n",
                "Somebody asked to set a new password for this Bugler account.",
                "",
                "Set it here — the link works once and stops working in an hour:",
                Link(secret, publicBaseUrl),
                "",
                "If this wasn't you, ignore this mail. Nothing has changed and your",
                "password still works."));

    /// <summary>
    /// The link points at the page, not at the API: a click from a mail is a GET, and what has to
    /// come back is a form asking for the new password.
    /// </summary>
    public static string Link(string secret, string publicBaseUrl) =>
        $"{publicBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(secret)}";
}
