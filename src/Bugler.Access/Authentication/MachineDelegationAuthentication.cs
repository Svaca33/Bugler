using System.Security.Claims;
using System.Text.Encodings.Web;
using Bugler.Access.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Access.Authentication;

/// <summary>
/// Holds a presented Machine Delegation answerable to the User behind it (ADR 0029). Nothing here is
/// trusted from the day the Secret was issued: the account is read back on every request, so
/// deactivation and deletion end the Machine Delegation at once, and the Admin role — which decides whether
/// the Visibility Scope is "everything" — is read from the row rather than frozen into anything.
///
/// The scheme is named by the policy the MCP endpoints ask for and by nothing else, so a Machine Delegation
/// Secret presented to the app's own REST API authenticates nothing, and a Session cookie arriving
/// at the machine door authenticates nothing either.
/// </summary>
internal sealed class MachineDelegationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AccessDbContext dbContext) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MachineDelegation";

    /// <summary>The one Application a narrowed Machine Delegation may read, carried on the principal.</summary>
    public const string ApplicationClaim = "access.delegated_application";

    /// <summary>
    /// How stale <see cref="MachineDelegation.LastUsedAt"/> is allowed to be. An agent asks in bursts, and
    /// the column exists so somebody can tell a live Machine Delegation from a dead one — a resolution of
    /// minutes answers that, where a write per request would put one on the read path for nothing.
    /// </summary>
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Machine-facing English, outside the catalogues, like <c>/health</c> and the same-origin
    /// refusal (ADR 0024). Whoever reads it is an agent relaying it to a person who is not looking
    /// at Bugler, so it says what happened and where to go — a bare 401 would reach them as
    /// "the tool is broken".
    /// </summary>
    private const string Missing =
        "This request must carry a Bugler machine delegation: Authorization: Bearer blgrd_…";

    private const string Rejected =
        "This machine delegation is not valid: it has expired, been revoked, or belongs to an " +
        "account that can no longer sign in. Issue a new one in Bugler under your account.";

    private string _refusal = Missing;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryReadSecret(out var secret))
        {
            _refusal = Missing;
            return AuthenticateResult.NoResult();
        }

        _refusal = Rejected;
        var now = DateTimeOffset.UtcNow;
        var fingerprint = MachineDelegationMaterial.Fingerprint(secret);

        var held = await dbContext.MachineDelegations
            .Where(d => d.Fingerprint == fingerprint)
            .Join(dbContext.Users, d => d.UserId, u => u.Id, (d, u) => new PresentedMachineDelegation(
                d.Id, d.UserId, u.Email, u.IsAdmin, u.DeactivatedAt, d.ApplicationId, d.ExpiresAt,
                d.RevokedAt, d.LastUsedAt))
            .FirstOrDefaultAsync(Context.RequestAborted);

        // One answer for every way of being invalid. Which way it was is the holder's business and
        // not a stranger's: an unknown Secret and a revoked one must read the same from outside.
        if (held is null
            || held.RevokedAt is not null
            || held.ExpiresAt <= now
            || held.DeactivatedAt is not null)
        {
            return AuthenticateResult.Fail(Rejected);
        }

        await NoteUseAsync(held, now);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, held.UserId.ToString()),
            new(ClaimTypes.Email, held.Email),
        };

        if (held.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AuthEndpoints.AdminRole));
        }

        // Present only when the Machine Delegation was narrowed. Absent means "whatever its User may read",
        // which is not the same as "everything" — GrantedVisibility still decides that.
        if (held.ApplicationId is { } application)
        {
            claims.Add(new Claim(ApplicationClaim, application.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>
    /// Says why in the body rather than leaving a bare status code: the reader is a tool, and a
    /// tool repeats what it is told.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{Scheme.Name}\"";
        Response.ContentType = "text/plain; charset=utf-8";
        await Response.WriteAsync(_refusal, Context.RequestAborted);
    }

    private bool TryReadSecret(out string secret)
    {
        secret = "";
        var header = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";

        if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = header[bearer.Length..].Trim();
        if (!MachineDelegationMaterial.LooksLikeSecret(candidate))
        {
            return false;
        }

        secret = candidate;
        return true;
    }

    private async Task NoteUseAsync(PresentedMachineDelegation held, DateTimeOffset now)
    {
        if (held.LastUsedAt is { } last && now - last < LastUsedResolution)
        {
            return;
        }

        await dbContext.MachineDelegations
            .Where(d => d.Id == held.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.LastUsedAt, now), Context.RequestAborted);
    }

    /// <summary>What the Machine Delegation is answerable to, read back in the one query it already made.</summary>
    private sealed record PresentedMachineDelegation(
        Guid Id,
        Guid UserId,
        string Email,
        bool IsAdmin,
        DateTimeOffset? DeactivatedAt,
        SharedKernel.ApplicationId? ApplicationId,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt,
        DateTimeOffset? LastUsedAt);
}
