using System.Security.Cryptography;
using System.Text;

namespace Bugler.Access.Users;

/// <summary>
/// The Secret a Machine Delegation is proven by: the same shape as an API Key's plaintext, under a prefix
/// of its own. The prefixes are disjoint on purpose — <c>blgrd_</c> does not begin with
/// <c>blgr_</c>, because the two credentials open opposite doors and a leaked string should say
/// which one it is (ADR 0029). Secret scanners key off exactly this.
/// </summary>
public static class MachineDelegationMaterial
{
    public const string Prefix = "blgrd_";

    /// <summary>What a Machine Delegation is worth holding for unless its issuer asks for less.</summary>
    public const int DefaultLifetimeDays = 90;

    public static string GenerateSecret() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Fingerprint(string secret) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    /// <summary>
    /// Cheap enough to run before touching the database, so a stray Session cookie or an API Key
    /// pasted into the wrong client never costs a query.
    /// </summary>
    public static bool LooksLikeSecret(string? candidate) =>
        candidate is not null
        && candidate.StartsWith(Prefix, StringComparison.Ordinal)
        && candidate.Length is > 16 and < 200;
}
