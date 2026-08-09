using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Contracts;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ReadVisibility;

/// <summary>
/// The real Visibility Scope: Admins see everything (null), a signed-in user sees
/// their granted Applications, and an anonymous caller sees nothing (empty set).
///
/// A caller holding a Machine Delegation narrowed to one Application sees that one alone — and only where
/// its User would have seen it anyway. The narrowing subtracts and never adds (ADR 0029), so it is
/// resolved here, against the grants as they stand now, rather than trusted from the day the
/// Machine Delegation was issued: revoking the grant closes the machine's view on its next query.
/// </summary>
internal sealed class GrantedVisibility(
    IHttpContextAccessor httpContextAccessor,
    AccessDbContext dbContext) : IReadVisibility
{
    public async ValueTask<IReadOnlyCollection<ApplicationId>?> GetVisibleApplicationsAsync(
        CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated is not true)
        {
            return Array.Empty<ApplicationId>();
        }

        var narrowedTo = ReadNarrowing(principal);
        var isAdmin = principal.IsInRole(AuthEndpoints.AdminRole);

        if (narrowedTo is null)
        {
            if (isAdmin)
            {
                return null;
            }
        }
        else if (isAdmin)
        {
            // An Admin reads everything, so the narrowing is the whole of the answer.
            return [narrowedTo.Value];
        }

        var userId = AuthEndpoints.GetUserId(principal);
        if (userId is null)
        {
            return Array.Empty<ApplicationId>();
        }

        var granted = await dbContext.ApplicationGrants
            .Where(g => g.UserId == userId)
            .Select(g => g.ApplicationId)
            .ToListAsync(cancellationToken);

        return narrowedTo is { } application
            ? granted.Where(g => g == application).ToList()
            : granted;
    }

    private static ApplicationId? ReadNarrowing(ClaimsPrincipal principal) =>
        Guid.TryParse(
            principal.FindFirstValue(MachineDelegationAuthenticationHandler.ApplicationClaim), out var id)
            ? new ApplicationId(id)
            : null;
}
