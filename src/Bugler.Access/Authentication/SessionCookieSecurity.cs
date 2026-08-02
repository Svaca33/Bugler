using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Bugler.Access.Authentication;

/// <summary>What the address a server declares itself at says about the way in it offers.</summary>
internal enum PublicAddress
{
    /// <summary>Not an absolute http or https URL: the server has not been told where it is reachable.</summary>
    Unstated,

    /// <summary>Plain HTTP on a loopback address — a developer's machine, which no request leaves.</summary>
    Loopback,

    /// <summary>Plain HTTP on a name other people resolve. Everything sent to it travels in the clear.</summary>
    PlainHttp,

    /// <summary>HTTPS: something in front terminates TLS, so the browser can be held to it.</summary>
    Tls,
}

/// <summary>
/// How the Session cookie is minted, read off <c>Server:PublicBaseUrl</c>. Bugler is meant to sit
/// behind a proxy that terminates TLS and speaks plain HTTP to Kestrel, so from its own vantage
/// point no request ever arrives over HTTPS and it cannot tell a secured deployment from an exposed
/// one by looking at the request. The public address is the one thing it already knows about
/// itself — an operator has to state it correctly for reset links to work at all (ADR 0019).
/// </summary>
internal sealed record SessionCookieSecurity(PublicAddress Address, string PublicBaseUrl)
{
    /// <summary>The name where the browser is not being asked for TLS the deployment cannot offer.</summary>
    public const string PlainName = "bugler.session";

    /// <summary>
    /// The name where it can. The <c>__Host-</c> prefix has the browser refuse the cookie unless it
    /// carries <c>Secure</c>, is scoped to <c>/</c>, and names no <c>Domain</c> — so `Secure` stops
    /// being a promise this code makes and becomes one the browser enforces, and no sibling host
    /// under the same registrable domain can plant a cookie of this name.
    /// </summary>
    public const string HostLockedName = "__Host-" + PlainName;

    public bool RequiresSecure => Address is PublicAddress.Tls;

    public string CookieName => RequiresSecure ? HostLockedName : PlainName;

    public CookieSecurePolicy SecurePolicy =>
        RequiresSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

    public static SessionCookieSecurity Read(IConfiguration configuration) =>
        Read(configuration[$"{AccessOptions.ServerSectionName}:{nameof(AccessOptions.PublicBaseUrl)}"]);

    public static SessionCookieSecurity Read(string? publicBaseUrl)
    {
        var stated = publicBaseUrl?.Trim() ?? "";

        // Anything that is not an absolute http(s) URL says nothing about TLS, including the empty
        // string. Bugler does not guess: a Secure cookie invented from silence would be dropped by
        // the browser on a plain-HTTP deployment, and the sign-in would fail where no message about
        // a misconfigured setting can reach anybody.
        if (!Uri.TryCreate(stated, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            return new SessionCookieSecurity(PublicAddress.Unstated, stated);
        }

        if (address.Scheme == Uri.UriSchemeHttps)
        {
            return new SessionCookieSecurity(PublicAddress.Tls, stated);
        }

        // Plain HTTP on localhost is the development topology, not a mistake worth shouting about.
        return new SessionCookieSecurity(
            address.IsLoopback ? PublicAddress.Loopback : PublicAddress.PlainHttp, stated);
    }
}
