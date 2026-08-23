using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ReadApplicationFocus;

/// <summary>
/// The Visibility Scope seen through the caller's Focus. The Scope is resolved first and the Focus
/// intersected into it, never the other way round, which is what makes a Focus subtract and only
/// subtract: a row naming an Application the User may not read changes nothing.
///
/// Two callers hold no Focus and get the Scope back untouched — an anonymous one, who is already
/// shown nothing, and a Machine Delegation, which lends its issuer's reading rather than the lens
/// they happen to hold this week (ADR 0029, ADR 0004). The machine door is kept away from this
/// class by an architecture test as well; this is the belt to that pair of braces.
/// </summary>
internal sealed class FocusedApplications(
    IHttpContextAccessor httpContextAccessor,
    IReadVisibility visibility,
    AccessDbContext dbContext) : IReadApplicationFocus
{
    public async ValueTask<IReadOnlyCollection<ApplicationId>?> GetFocusedApplicationsAsync(
        CancellationToken cancellationToken)
    {
        var visible = await visibility.GetVisibleApplicationsAsync(cancellationToken);

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated is not true
            || principal.FindFirstValue(MachineDelegationAuthenticationHandler.DelegationClaim) is not null
            || AuthEndpoints.GetUserId(principal) is not { } userId)
        {
            return visible;
        }

        var focused = await ReadFocusAsync(dbContext, userId, cancellationToken);
        return visible is null ? focused : visible.Where(focused.Contains).ToList();
    }

    /// <summary>
    /// The stored rows, as they stand. An empty answer is a Focus of nothing rather than a missing
    /// one: a person who has chosen no Application is shown none (ADR 0004).
    /// </summary>
    internal static async Task<HashSet<ApplicationId>> ReadFocusAsync(
        AccessDbContext dbContext, Guid userId, CancellationToken cancellationToken) =>
        (await dbContext.ApplicationFocuses
            .Where(f => f.UserId == userId)
            .Select(f => f.ApplicationId)
            .ToListAsync(cancellationToken))
        .ToHashSet();
}
