using System.Net;
using System.Net.Http.Json;

namespace Bugler.IntegrationTests;

/// <summary>
/// Setting up an unclaimed server is a check followed by an insert, and the first User of a server
/// becomes its Admin — so the two together have to be one decision. The unique index on the e-mail
/// does not make them one: two requests carrying two different addresses both read an empty table.
/// </summary>
public sealed class ServerClaimTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartUnclaimedAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Only_one_of_two_people_claiming_at_once_becomes_Admin()
    {
        var first = _harness.CreateAnonymousClient();
        var second = _harness.CreateAnonymousClient();

        // Whether these two truly overlap is up to the scheduler, and the assertion holds either
        // way: overlapping, one is refused by the serialization failure; sequential, by the check.
        var answers = await Task.WhenAll(
            first.PostAsJsonAsync(
                "/api/auth/setup", new { email = "first@bugler.test", password = "FirstPass123!" }),
            second.PostAsJsonAsync(
                "/api/auth/setup", new { email = "second@bugler.test", password = "SecondPass123!" }));

        Assert.Single(answers, answer => answer.IsSuccessStatusCode);
        Assert.Single(answers, answer => answer.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT count(*) FROM access.users WHERE is_admin", expected: 1));
    }

    /// <summary>The plain path, uncontested: the server is claimed once and never again.</summary>
    [Fact]
    public async Task A_claimed_server_cannot_be_claimed_again()
    {
        var client = _harness.CreateAnonymousClient();
        (await client.PostAsJsonAsync(
                "/api/auth/setup", new { email = "owner@bugler.test", password = "OwnerPass123!" }))
            .EnsureSuccessStatusCode();

        var again = await _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/setup", new { email = "latecomer@bugler.test", password = "LatePass123!" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>
    /// A refused setup leaves nothing behind — the transaction that would have written the Admin
    /// is the same one the check ran in.
    /// </summary>
    [Fact]
    public async Task A_setup_refused_for_its_password_claims_nothing()
    {
        var client = _harness.CreateAnonymousClient();

        var refused = await client.PostAsJsonAsync(
            "/api/auth/setup", new { email = "hopeful@bugler.test", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT count(*) FROM access.users", expected: 0));
        (await client.PostAsJsonAsync(
                "/api/auth/setup", new { email = "hopeful@bugler.test", password = "LongEnough123!" }))
            .EnsureSuccessStatusCode();
    }
}
