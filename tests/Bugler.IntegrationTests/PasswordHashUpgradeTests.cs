using System.Buffers.Binary;
using System.Net.Http.Json;
using Bugler.Access.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bugler.IntegrationTests;

/// <summary>
/// Raising <see cref="Passwords.IterationCount"/> does nothing for passwords already stored — their
/// hashes carry the count they were written under. Signing in is the one moment the password is in
/// hand to write it again, and this is what has to be true when it does: the account still works,
/// the stored hash is at today's count, and nobody's other Sessions have been quietly ended.
/// </summary>
public sealed class PasswordHashUpgradeTests : IAsyncLifetime
{
    private const string Email = "upgraded@bugler.test";
    private const string Password = "UpgradedPass123!";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Signing_in_rewrites_a_hash_left_at_an_older_iteration_count()
    {
        var elsewhere = await _harness.CreateUserClientAsync(Email, Password, _harness.ApplicationId);
        await StoreHashAtAsync(100_000);
        Assert.Equal(100_000, await StoredIterationCountAsync());

        var here = _harness.CreateAnonymousClient();
        (await here.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password }))
            .EnsureSuccessStatusCode();

        Assert.Equal(Passwords.IterationCount, await StoredIterationCountAsync());
        // The Stamp did not move, so the Session opened before the rewrite reads on.
        (await elsewhere.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
        (await here.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
    }

    /// <summary>The rewrite is not a password change: the password itself is untouched.</summary>
    [Fact]
    public async Task The_password_still_signs_in_afterwards()
    {
        await _harness.CreateUserClientAsync(Email, Password, _harness.ApplicationId);
        await StoreHashAtAsync(100_000);

        (await _harness.CreateAnonymousClient()
                .PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password }))
            .EnsureSuccessStatusCode();

        (await _harness.CreateAnonymousClient()
                .PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password }))
            .EnsureSuccessStatusCode();
    }

    /// <summary>Writes this account's password as a hash made at the given iteration count.</summary>
    private async Task StoreHashAtAsync(int iterationCount)
    {
        var user = new User
        {
            Id = await _harness.FindUserIdAsync(Email),
            Email = Email,
            PasswordHash = "",
            SecurityStamp = Guid.Empty,
            IsAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var hasher = new PasswordHasher<User>(
            Options.Create(new PasswordHasherOptions { IterationCount = iterationCount }));

        await _harness.ExecuteSqlAsync(
            $"UPDATE access.users SET password_hash = '{hasher.HashPassword(user, Password)}' " +
            $"WHERE email = '{Email}'");
    }

    /// <summary>
    /// Identity's V3 format, read where it is written: a marker byte, the PRF, and then the
    /// iteration count, each of the last two a big-endian 32-bit number.
    /// </summary>
    private async Task<int> StoredIterationCountAsync()
    {
        var stored = await _harness.WaitForRowsAsync(
            $"SELECT password_hash FROM access.users WHERE email = '{Email}'", expectedCount: 1);
        var bytes = Convert.FromBase64String(stored.Single());

        Assert.Equal(0x01, bytes[0]);
        return BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(5, 4));
    }
}
