using Bugler.Access.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

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

    /// <summary>
    /// The exception to the rule above, and the reason it is a separate method: the same password
    /// written again at a higher iteration count says nothing new about who may hold a Session, so
    /// raising the count must not sign anybody out of anywhere.
    /// </summary>
    [Fact]
    public void Rehashing_writes_a_new_hash_and_leaves_the_stamp_alone()
    {
        var user = NewUser();
        Passwords.Set(user, "unchanged password", Hasher);
        var (stamp, hash) = (user.SecurityStamp, user.PasswordHash);

        Passwords.Rehash(user, "unchanged password", Hasher);

        Assert.Equal(stamp, user.SecurityStamp);
        // A fresh salt every time, so the same password never writes the same bytes twice.
        Assert.NotEqual(hash, user.PasswordHash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            Hasher.VerifyHashedPassword(user, user.PasswordHash, "unchanged password"));
    }

    /// <summary>
    /// What the whole upgrade path stands on: a hash written under the old count still verifies,
    /// and says so by asking to be written again rather than by failing.
    /// </summary>
    [Fact]
    public void A_hash_from_a_lower_iteration_count_verifies_and_asks_to_be_rewritten()
    {
        var user = NewUser();
        var older = new PasswordHasher<User>(
            Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }));
        user.PasswordHash = older.HashPassword(user, "correct horse battery");

        var atTodaysCount = new PasswordHasher<User>(
            Options.Create(new PasswordHasherOptions { IterationCount = Passwords.IterationCount }));

        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            atTodaysCount.VerifyHashedPassword(user, user.PasswordHash, "correct horse battery"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            atTodaysCount.VerifyHashedPassword(user, user.PasswordHash, "wrong horse battery"));
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
