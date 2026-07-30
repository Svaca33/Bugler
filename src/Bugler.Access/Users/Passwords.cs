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

    public static string Requirement =>
        $"A password of {MinimumLength} to {MaximumLength} characters is required.";

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
}
