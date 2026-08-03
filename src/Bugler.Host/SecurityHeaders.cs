namespace Bugler.Host;

/// <summary>
/// What every response on the App surface tells the browser it may do with what it just received.
/// Rendering text somebody else wrote is not an edge case in Bugler, it is the product: every Log
/// Record body, attribute value and span name on screen arrived from a monitored application. No
/// XSS exists today — telemetry reaches the DOM as React text nodes, which escape — but a policy is
/// not for the XSS a codebase has, it is for the one a later change introduces, and it bounds what
/// that one could reach (ADR 0022).
///
/// Only browsers enforce this, so it speaks for the UI alone and says nothing about the REST API or
/// the OTLP surfaces, whose callers are not browsers. It is scoped to the App surface for the same
/// reason the static files are: no browser ever reaches 4317 or 4318.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// <para>
    /// <c>script-src 'self'</c> is the half that stops an XSS, and it costs nothing: the built
    /// index.html carries no inline script and no event-handler attribute, only external hashed
    /// chunks. <c>base-uri 'none'</c> guards it, since the app never uses a <c>&lt;base&gt;</c> and
    /// an injected one could aim every relative script path elsewhere.
    /// </para>
    /// <para>
    /// <c>style-src</c> keeps <c>'unsafe-inline'</c>, and is meant to. Radix's Dialog and Select
    /// reach the DOM through react-remove-scroll, which appends a <c>&lt;style&gt;</c> element when
    /// either opens; removing <c>'unsafe-inline'</c> would silently break every one of them, and
    /// buying it back needs a per-request nonce spliced into an index.html that is a static file
    /// today. What it would buy is small: an inline style executes nothing, and CSS exfiltrates only
    /// by fetching, which <c>default-src 'self'</c> already refuses.
    /// </para>
    /// <para>
    /// <c>font-src</c> admits <c>data:</c> because the bundler inlines the self-hosted IBM Plex
    /// faces into the stylesheet and offers no way not to. A font executes nothing; <c>data:</c>
    /// stays out of the directives where it would matter.
    /// </para>
    /// <para>
    /// <c>frame-ancestors 'none'</c> is the modern spelling of X-Frame-Options, and the case for it
    /// is concrete: an Admin lured into clicking through an invisible frame, with Deletion
    /// irreversible and never partial. There is no <c>upgrade-insecure-requests</c> — Bugler emits
    /// no header about a transport it does not hold (ADR 0019), and everything it loads is
    /// same-origin and relative anyway.
    /// </para>
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "base-uri 'none'; " +
        "object-src 'none'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    /// <summary>
    /// The address carries the Filter, so it carries search terms and attribute values. Nothing in
    /// the UI links outward today, and this keeps it that way should something ever do.
    /// </summary>
    public const string ReferrerPolicy = "same-origin";

    /// <summary>
    /// Applies to every response the branch it is mounted in handles — documents, bundles, REST
    /// answers, 404s alike. A rule with no exceptions cannot be missed by whoever adds the next
    /// endpoint, and nosniff earns its place on the API answers most of all: a JSON body full of
    /// telemetry is a better thing to have read as HTML than index.html is.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = ReferrerPolicy;
            return next(context);
        });
}
