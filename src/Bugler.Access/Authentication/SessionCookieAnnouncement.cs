using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bugler.Access.Authentication;

/// <summary>
/// Says once, at startup, how the Session cookie is being minted. Nothing else about this fails
/// loudly — a Session cookie without <c>Secure</c> is served, accepted and used exactly like one
/// with it, and the only other way to see which of the two a deployment hands out is to read the
/// cookie's flags in a browser. The warning is spent on the two states that are actually wrong, so
/// that it still means something when it appears.
/// </summary>
internal sealed class SessionCookieAnnouncement(
    SessionCookieSecurity security,
    ILogger<SessionCookieAnnouncement> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        switch (security.Address)
        {
            case PublicAddress.Tls:
                logger.LogInformation(
                    "Session cookie is {CookieName}, carrying Secure: Server:PublicBaseUrl is {PublicBaseUrl}",
                    security.CookieName,
                    security.PublicBaseUrl);
                break;

            case PublicAddress.Loopback:
                logger.LogInformation(
                    "Session cookie is {CookieName}, without Secure: Server:PublicBaseUrl is {PublicBaseUrl}, "
                    + "a loopback address no request leaves this machine for",
                    security.CookieName,
                    security.PublicBaseUrl);
                break;

            case PublicAddress.PlainHttp:
                logger.LogWarning(
                    "Session cookie will travel over plain HTTP and can be read off the wire: "
                    + "Server:PublicBaseUrl is {PublicBaseUrl}. Put TLS in front of Bugler and state "
                    + "the https address there, or leave the UI reachable only from inside the network",
                    security.PublicBaseUrl);
                break;

            default:
                logger.LogWarning(
                    "Bugler has not been told where it is reachable, so the Session cookie cannot carry "
                    + "Secure and password reset is off. Set Server:PublicBaseUrl to the address people "
                    + "type; \"{PublicBaseUrl}\" is not an absolute http or https URL",
                    security.PublicBaseUrl);
                break;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
