namespace Bugler.Host;

/// <summary>
/// What stops a page on another origin from spending a visitor's Session (ADR 0025). Every request
/// on the App surface that is not a safe method must carry <see cref="HeaderName"/>; Bugler's own
/// UI sets it on every call, and a cross-site page cannot set it at all — a form cannot send
/// headers, and a script that tries buys a preflight that no CORS policy here will answer.
///
/// Mounted beside the security headers, and for the same reason: it speaks to browsers, and no
/// browser reaches 4317 or 4318. The OTLP surfaces are authenticated by API key and never by a
/// cookie, so nothing there is at stake.
/// </summary>
public static class SameOriginMutations
{
    /// <summary>
    /// The header a mutation names itself with. No <c>X-</c> prefix (RFC 6648), and no <c>Sec-</c>
    /// either — that space is reserved for headers the browser sets and a script may not.
    /// </summary>
    public const string HeaderName = "Bugler-Request";

    /// <summary>
    /// English and outside the message catalogues on purpose. The UI sends the header on every
    /// call, so nobody signed in ever reads this sentence; whoever does is holding a script, and
    /// this is machine-facing text like <c>/health</c> (ADR 0024).
    /// </summary>
    public const string Refusal =
        "This request must carry the Bugler-Request header. Any non-empty value will do.";

    /// <summary>
    /// The check is for presence, not for a value. Everything it is worth stands on the fact that a
    /// foreign page cannot set <em>any</em> header of Bugler's choosing; comparing what is inside
    /// one would add nothing but a second way to get it wrong.
    /// </summary>
    public static IApplicationBuilder UseSameOriginMutations(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsSafe(context.Request.Method)
                || !string.IsNullOrEmpty(context.Request.Headers[HeaderName]))
            {
                await next(context);
                return;
            }

            // 403 rather than 401: the SPA answers a 401 by treating the Session as gone and
            // sending the person to the sign-in page, which would turn a missing header into a
            // loop through a door that was never locked.
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(Refusal, context.RequestAborted);
        });

    /// <summary>
    /// Named rather than denied: a method Bugler has never heard of is a mutation until somebody
    /// says otherwise, so nothing new arrives already exempt.
    /// </summary>
    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);
}
