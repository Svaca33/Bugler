using Bugler.Host;

namespace Bugler.IntegrationTests;

/// <summary>
/// That the policy actually reaches the wire, and on more than the one response somebody thought
/// to check. The App surface is the unit it is scoped to, so what is asserted here is that every
/// kind of answer that surface produces carries it — a REST answer, the untagged health probe, and
/// a path no endpoint claimed at all (ADR 0022).
///
/// What cannot be asserted here is the other half: that the OTLP surfaces carry nothing. The
/// harness is a <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{T}"/>, whose
/// connections have no port in the surface map and are unrestricted by design — the same gap
/// <c>UseListenerSurfaces</c> already documents. The scoping rides on that one predicate, which the
/// static files have always ridden on too.
/// </summary>
public sealed class SecurityHeadersTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Theory]
    // A REST answer, carrying exactly the telemetry the policy exists for.
    [InlineData("/api/auth/me")]
    // Untagged, and served on every surface — on this one it answers under the policy like the rest.
    [InlineData("/health")]
    // No endpoint produced this one. The headers are the surface's, not any endpoint's.
    [InlineData("/nothing-is-mounted-here")]
    public async Task Every_answer_the_app_surface_gives_is_served_under_the_policy(string path)
    {
        var response = await _harness.Client.GetAsync(path);

        Assert.Equal(
            SecurityHeaders.ContentSecurityPolicy,
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(
            SecurityHeaders.ReferrerPolicy,
            Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    /// <summary>
    /// The directive the whole policy is for, asserted on its own so that widening anything else
    /// cannot quietly widen this. Every other directive is a bound on what an injected script could
    /// reach; this is the one that stops there being one.
    /// </summary>
    [Fact]
    public void A_script_may_come_only_from_Bugler_itself()
    {
        Assert.Equal("script-src 'self'", DirectiveIn(SecurityHeaders.ContentSecurityPolicy, "script-src"));
    }

    /// <summary>
    /// Inline styles are admitted deliberately and inline scripts are not, which is only worth
    /// anything while the two stay told apart: <c>'unsafe-inline'</c> belongs to exactly one
    /// directive, and a fallback wide enough to hand it to scripts would undo the point.
    /// </summary>
    [Fact]
    public void Nothing_but_style_src_is_allowed_to_be_inline()
    {
        var relaxed = SecurityHeaders.ContentSecurityPolicy
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(directive => directive.Contains("'unsafe-inline'", StringComparison.Ordinal));

        Assert.Equal("style-src 'self' 'unsafe-inline'", Assert.Single(relaxed));
    }

    /// <summary>
    /// A page Bugler serves is never a frame in somebody else's page. Deletion is irreversible and
    /// never partial, so an Admin clicking through an invisible frame is the case this answers.
    /// </summary>
    [Fact]
    public void No_page_of_Buglers_may_be_framed()
    {
        Assert.Equal(
            "frame-ancestors 'none'", DirectiveIn(SecurityHeaders.ContentSecurityPolicy, "frame-ancestors"));
    }

    private static string DirectiveIn(string policy, string name) =>
        Assert.Single(
            policy.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            directive => directive.StartsWith($"{name} ", StringComparison.Ordinal));
}
