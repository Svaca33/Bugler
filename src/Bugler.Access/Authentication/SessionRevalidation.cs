using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bugler.Access.Authentication;

/// <summary>
/// Holds an issued Session answerable to the database it was minted from. A User who is deactivated
/// or deleted after signing in loses their Session on the next request, a Session minted before the
/// password changed loses it too, and the Admin role is read back rather than trusted from the
/// cookie it was frozen into at sign-in.
/// </summary>
internal static class SessionRevalidation
{
    /// <summary>When the Session was last read back, carried in the cookie alongside the claims.</summary>
    private const string ValidatedAtKey = "access.validated_at";

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
        {
            return;
        }

        var services = context.HttpContext.RequestServices;
        var interval = TimeSpan.FromSeconds(
            services.GetRequiredService<IOptions<AccessOptions>>().Value.SessionRevalidationSeconds);
        var now = DateTimeOffset.UtcNow;

        if (!IsDue(context.Properties, interval, now))
        {
            return;
        }

        var userId = AuthEndpoints.GetUserId(context.Principal);
        var current = userId is null
            ? null
            : await services.GetRequiredService<AccessDbContext>().Users
                .AsNoTracking()
                .Where(u => u.Id == userId && u.DeactivatedAt == null)
                .Select(u => new AccountState(u.IsAdmin, u.SecurityStamp))
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        // Gone, deactivated, or carrying the stamp of a password that has since been replaced.
        if (current is null || !CarriesCurrentStamp(context.Principal, current.SecurityStamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Re-issue the cookie only when this request changed what it carries.
        var roleRefreshed = RefreshAdminRole(identity, current.IsAdmin);
        if (Stamp(context.Properties, interval, now) || roleRefreshed)
        {
            context.ShouldRenew = true;
        }
    }

    /// <summary>
    /// A Session minted before the password changed carries the stamp from before it. A cookie
    /// from before stamps existed carries none, and is refused for the same reason: there is no
    /// way to tell which password it was issued against.
    /// </summary>
    private static bool CarriesCurrentStamp(ClaimsPrincipal principal, Guid stamp) =>
        Guid.TryParse(principal.FindFirstValue(AuthEndpoints.SecurityStampClaim), out var carried)
        && carried == stamp;

    private static bool IsDue(AuthenticationProperties properties, TimeSpan interval, DateTimeOffset now)
    {
        if (interval <= TimeSpan.Zero)
        {
            return true;
        }

        return !properties.Items.TryGetValue(ValidatedAtKey, out var stamp)
            || !DateTimeOffset.TryParse(
                stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var validatedAt)
            || now - validatedAt >= interval;
    }

    /// <summary>Records this read-back, or drops a stamp left over from a wider interval.</summary>
    private static bool Stamp(AuthenticationProperties properties, TimeSpan interval, DateTimeOffset now)
    {
        if (interval <= TimeSpan.Zero)
        {
            return properties.Items.Remove(ValidatedAtKey);
        }

        properties.Items[ValidatedAtKey] = now.ToString("O", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>What the Session is answerable to, read back in the one query it already made.</summary>
    private sealed record AccountState(bool IsAdmin, Guid SecurityStamp);

    private static bool RefreshAdminRole(ClaimsIdentity identity, bool isAdmin)
    {
        var claim = identity.FindFirst(
            c => c.Type == identity.RoleClaimType && c.Value == AuthEndpoints.AdminRole);

        if (isAdmin && claim is null)
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, AuthEndpoints.AdminRole));
            return true;
        }

        if (!isAdmin && claim is not null)
        {
            identity.RemoveClaim(claim);
            return true;
        }

        return false;
    }
}
