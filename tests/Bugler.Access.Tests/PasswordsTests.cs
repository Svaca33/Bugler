using Bugler.Access.Users;
using Microsoft.AspNetCore.Identity;

namespace Bugler.Access.Tests;

/// <summary>
/// The single gate every password goes through: what it accepts, and that it never writes a hash
/// without rolling the Security Stamp beside it.
/// </summary>
public class PasswordsTests
{
    private static readonly PasswordHasher<User> Hasher = new();

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Length_alone_decides_acceptance(int length, bool accepted) =>
        Assert.Equal(accepted, Passwords.IsAcceptable(new string('x', length)));

    /// <summary>
    /// The upper bound guards the anonymous reset path: without it, anyone could hand the hasher
    /// a megabyte to chew on.
    /// </summary>
    [Fact]
    public void A_password_far_over_the_maximum_is_refused() =>
        Assert.False(Passwords.IsAcceptable(new string('x', 100_000)));

    [Fact]
    public void A_missing_password_is_refused() => Assert.False(Passwords.IsAcceptable(null));

    [Fact]
    public void Setting_a_password_stores_a_hash_that_verifies()
    {
        var user = NewUser();

        Passwords.Set(user, "correct horse battery", Hasher);

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            Hasher.VerifyHashedPassword(user, user.PasswordHash, "correct horse battery"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            Hasher.VerifyHashedPassword(user, user.PasswordHash, "correct horse batteries"));
    }

    [Fact]
    public void Setting_a_password_rolls_the_security_stamp()
    {
        var user = NewUser();
        Passwords.Set(user, "the first password", Hasher);
        var before = user.SecurityStamp;

        Passwords.Set(user, "the second password", Hasher);

        Assert.NotEqual(Guid.Empty, before);
        Assert.NotEqual(before, user.SecurityStamp);
    }

    /// <summary>
    /// Even re-setting the very same password ends the other Sessions. What matters is that a
    /// password was written, not that it turned out to be a different one.
    /// </summary>
    [Fact]
    public void Setting_the_same_password_again_still_rolls_the_stamp()
    {
        var user = NewUser();
        Passwords.Set(user, "unchanged password", Hasher);
        var before = user.SecurityStamp;

        Passwords.Set(user, "unchanged password", Hasher);

        Assert.NotEqual(before, user.SecurityStamp);
    }

    private static User NewUser() => new()
    {
        Id = Guid.CreateVersion7(),
        Email = "someone@bugler.test",
        PasswordHash = "",
        SecurityStamp = Guid.Empty,
        IsAdmin = false,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
