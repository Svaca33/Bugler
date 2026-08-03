using System.Net;
using System.Net.Http.Json;
using Bugler.Host;

namespace Bugler.IntegrationTests;

/// <summary>
/// That a mutation without <c>Bugler-Request</c> is refused, and a read without it is not (ADR
/// 0025). What the header stands in for cannot be reproduced from a test client — no
/// <see cref="HttpClient"/> is subject to a preflight — so what is asserted is the half that lives
/// on this side: the rule holds with no exceptions, on the anonymous doors as much as behind the
/// Session, and it never touches a read.
/// </summary>
public sealed class SameOriginMutationTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    /// <summary>
    /// The anonymous doors are in for the sharpest reason of all: a cross-site POST to
    /// <c>setup</c> on an unclaimed server would hand somebody an Admin account, and one to
    /// <c>login</c> signs a visitor into an account that is not theirs.
    /// </summary>
    [Theory]
    [InlineData("/api/auth/setup")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/password/forgot")]
    public async Task An_anonymous_mutation_that_does_not_name_itself_is_refused(string path)
    {
        var unnamed = _harness.CreateUnnamedClient();

        var response = await unnamed.PostAsJsonAsync(path, new { email = "somebody@bugler.test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(SameOriginMutations.Refusal, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// A body-less mutation behind a Session — the shape with nothing else standing in the way,
    /// since a JSON body is at least an obstacle a plain form cannot clear. The refusal is a 403
    /// and not a 401 for a reason the UI would feel: a 401 tells the SPA the Session is gone and
    /// sends the person to sign in, which from a missing header would loop.
    /// </summary>
    [Fact]
    public async Task A_signed_in_mutation_that_does_not_name_itself_is_refused()
    {
        var client = _harness.CreateAnonymousClient();
        (await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email = BuglerHarness.AdminEmail, password = BuglerHarness.AdminPassword }))
            .EnsureSuccessStatusCode();

        // Signed in and holding the cookie, it now stops naming what it sends.
        client.DefaultRequestHeaders.Remove(SameOriginMutations.HeaderName);

        var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // And the Session it holds is untouched — the door was never locked, only unnamed.
        client.DefaultRequestHeaders.Add(SameOriginMutations.HeaderName, "1");
        (await client.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
    }

    /// <summary>Reads are safe methods and are asked for nothing, signed in or not.</summary>
    [Theory]
    [InlineData("/api/auth/status")]
    [InlineData("/health")]
    public async Task A_read_is_never_asked_to_name_itself(string path)
    {
        var response = await _harness.CreateUnnamedClient().GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The check runs before routing, so it covers a method nobody mounted an endpoint for. A
    /// future verb arrives guarded rather than exempt.
    /// </summary>
    [Fact]
    public async Task A_mutation_of_a_path_that_has_no_endpoint_is_refused_too()
    {
        var response = await _harness.CreateUnnamedClient()
            .PostAsync("/nothing-is-mounted-here", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Presence is the test; the value is never read, so no value can be the wrong one.</summary>
    [Fact]
    public async Task Any_non_empty_value_names_a_request()
    {
        var client = _harness.CreateUnnamedClient();
        client.DefaultRequestHeaders.Add(SameOriginMutations.HeaderName, "whatever-the-caller-likes");

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = BuglerHarness.AdminEmail, password = BuglerHarness.AdminPassword });

        response.EnsureSuccessStatusCode();
    }
}
