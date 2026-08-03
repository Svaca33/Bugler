using Microsoft.AspNetCore.Identity;

namespace Bugler.Access.Users;

/// <summary>
/// The one way a password is written. Every path that sets one — the first Admin at setup, an
/// Admin creating an account, a Password Change, a Password Reset — comes through here, because
/// each of them must also roll the Security Stamp and none of them may forget to.
/// </summary>
internal static class Passwords
{
    public const int MinimumLength = 8;

    /// <summary>
    /// An upper bound so an anonymous caller cannot hand the hasher a megabyte to chew on. There
    /// is no rule beyond length: forced complexity produces Password123! and a note under the
    /// keyboard.
    /// </summary>
    public const int MaximumLength = 256;

    /// <summary>
    /// What one password guess costs whoever stole the table. Identity V3 is PBKDF2-HMAC-SHA512,
    /// and 210 000 is the figure OWASP names for it today; the framework's own default is 100 000.
    /// The count is written inside each hash, so raising this leaves every stored password
    /// verifiable at the count it was written under — and <see cref="Rehash"/> is what eventually
    /// brings those up to this one.
    /// </summary>
    /// <remarks>
    /// Measured at 112 ms per verify on a 2026 developer laptop, against 54 ms at the default.
    /// Both sit under the floor an anonymous refusal is held for (ADR 0023), which is what makes
    /// this affordable; a figure that did not would be a decision about that floor as well.
    /// </remarks>
    public const int IterationCount = 210_000;

    public static bool IsAcceptable(string? password) =>
        password is not null && password.Length is >= MinimumLength and <= MaximumLength;

    /// <summary>
    /// Writes the hash and rolls the stamp together — the second is what ends the Sessions the old
    /// password left behind. The caller saves.
    /// </summary>
    public static void Set(User user, string password, IPasswordHasher<User> hasher)
    {
        user.PasswordHash = hasher.HashPassword(user, password);
        user.SecurityStamp = Guid.CreateVersion7();
    }

    /// <summary>
    /// The same password written again, because its hash was made under a cheaper setting than
    /// <see cref="IterationCount"/>. Deliberately does not touch the Security Stamp: the Stamp says
    /// which generation of Sessions counts, and nothing about who may hold one has changed. Rolling
    /// it here would mean that raising a constant signs everybody out of everywhere on their next
    /// sign-in, which is a thing to do on purpose or not at all. The caller saves.
    /// </summary>
    public static void Rehash(User user, string password, IPasswordHasher<User> hasher) =>
        user.PasswordHash = hasher.HashPassword(user, password);
}
