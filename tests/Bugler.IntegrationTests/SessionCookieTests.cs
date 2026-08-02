using System.Net.Http.Json;

namespace Bugler.IntegrationTests;

/// <summary>
/// What a server that knows it stands behind TLS actually puts on the wire. The decision itself is
/// stated without a database in Access's own tests; this is the wiring — that it reaches the
/// Set-Cookie header of a real sign-in, on a request that arrives over plain HTTP exactly as it
/// would behind a proxy that terminates TLS. The other half of the pair lives in
/// <see cref="AccessTests"/>, whose harness is a loopback http server (ADR 0019).
/// </summary>
public sealed class SessionCookieTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await BuglerHarness.StartAsync(publicBaseUrl: "https://bugler.test");

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_server_reachable_over_https_mints_a_Session_the_browser_will_not_send_in_the_clear()
    {
        var setCookie = await SignInAndReadSessionCookieAsync();

        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The __Host- prefix is what makes the flags above the browser's rule rather than this code's
    /// promise: it refuses the cookie outright unless Secure is set and the scope is host-only.
    /// </summary>
    [Fact]
    public async Task The_Session_cookie_is_locked_to_the_host_it_was_minted_by()
    {
        var setCookie = await SignInAndReadSessionCookieAsync();

        Assert.StartsWith("__Host-bugler.session=", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Secure cookie is only worth anything if the sign-in it protects still works. This is also
    /// the guard on the harness itself: a client whose base address disagreed with the server's
    /// public address would silently stop sending the Session back.
    /// </summary>
    [Fact]
    public async Task The_Session_still_signs_its_holder_in()
    {
        var response = await _harness.Client.GetAsync("/api/auth/me");

        response.EnsureSuccessStatusCode();
    }

    private async Task<string> SignInAndReadSessionCookieAsync()
    {
        var response = await _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = BuglerHarness.AdminEmail, password = BuglerHarness.AdminPassword });
        response.EnsureSuccessStatusCode();

        return Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("__Host-bugler.session=", StringComparison.Ordinal));
    }
}
