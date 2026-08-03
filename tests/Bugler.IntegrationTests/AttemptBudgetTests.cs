using System.Net;
using System.Net.Http.Json;

namespace Bugler.IntegrationTests;

/// <summary>
/// The Attempt Budget as an anonymous caller meets it: what a spent one answers, that it is spent
/// per address rather than per caller, and that an address belonging to nobody is refused exactly
/// as one belonging to a User is (ADR 0021).
/// </summary>
public sealed class AttemptBudgetTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task An_eleventh_guess_at_one_address_is_refused_rather_than_answered()
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await GuessAsync(BuglerHarness.AdminEmail)).StatusCode);
        }

        var refused = await GuessAsync(BuglerHarness.AdminEmail);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var wait = refused.Headers.RetryAfter?.Delta;
        Assert.True(wait > TimeSpan.Zero, "a refusal must say when the address may ask again");
    }

    /// <summary>
    /// The budget is the address's, not the endpoint's: guessing one account to exhaustion must
    /// leave everybody else signing in, or one spiteful caller would close the door on the server.
    /// </summary>
    [Fact]
    public async Task Exhausting_one_address_leaves_every_other_address_answered()
    {
        for (var attempt = 1; attempt <= 11; attempt++)
        {
            await GuessAsync(BuglerHarness.AdminEmail);
        }

        var elsewhere = await _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = "someone.else@bugler.test", password = "WrongPass123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, elsewhere.StatusCode);
    }

    /// <summary>
    /// The refusal must not become the answer the sign-in form refuses to give: an address nobody
    /// holds runs out of budget on the same attempt, so the 429 says nothing about the account
    /// behind it.
    /// </summary>
    [Fact]
    public async Task An_address_belonging_to_nobody_is_refused_on_the_same_attempt()
    {
        const string stranger = "nobody@bugler.test";

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await GuessAsync(stranger)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await GuessAsync(stranger)).StatusCode);
    }

    /// <summary>
    /// Asking for a reset link is counted apart from signing in, and more tightly — its budget is
    /// paced to the mail interval rather than to somebody's memory of their password.
    /// </summary>
    [Fact]
    public async Task A_fourth_ask_for_a_reset_link_is_refused()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await AskForLinkAsync()).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await AskForLinkAsync()).StatusCode);

        // The two budgets are separate: too many asks must not cost somebody the password they
        // turned out to remember.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GuessAsync(BuglerHarness.AdminEmail)).StatusCode);
    }

    /// <summary>
    /// A password longer than one can be set to cannot belong to any account, and the hasher never
    /// sees it — but the caller is told only what a wrong password is told.
    /// </summary>
    [Fact]
    public async Task A_password_longer_than_any_that_can_be_set_is_refused_like_a_wrong_one()
    {
        var response = await _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = BuglerHarness.AdminEmail, password = new string('x', 4096) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Each guess arrives on its own connection, the way a guesser's would.</summary>
    private Task<HttpResponseMessage> GuessAsync(string email) =>
        _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = "NotThePassword123!" });

    private Task<HttpResponseMessage> AskForLinkAsync() =>
        _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/password/forgot", new { email = BuglerHarness.AdminEmail });
}
