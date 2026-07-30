using System.Security.Cryptography;
using System.Text;

namespace Bugler.Access.ResetPassword;

/// <summary>
/// The secret half of a Reset Ticket: how one is made and how one is recognised again. Same shape
/// as an API key in the Registry — 32 random bytes in the hand of whoever holds it, a SHA-256
/// fingerprint in the database. No slow KDF: the secret carries 256 bits of its own entropy, so
/// there is no weak password underneath for one to protect.
/// </summary>
internal static class ResetTickets
{
    /// <summary>
    /// How long a ticket is worth presenting. An hour covers "the mail arrived, I'll do it after
    /// coffee" and does not cover finding the link in an inbox next week.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// The shortest gap between two mails to one account. Its point is not to slow an attacker
    /// guessing accounts — it is that anyone who knows a colleague's address could otherwise fill
    /// their inbox by holding down a button.
    /// </summary>
    public static readonly TimeSpan MailInterval = TimeSpan.FromMinutes(2);

    public static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Fingerprint(string secret) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(secret));
}
