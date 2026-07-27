using Bugler.Access.Authentication;
using Bugler.Access.Contracts;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ReadVisibility;

/// <summary>
/// The real Visibility Scope: Admins see everything (null), a signed-in user sees
/// their granted Applications, and an anonymous caller sees nothing (empty set).
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

        if (principal.IsInRole("Admin"))
        {
            return null;
        }

        var userId = AuthEndpoints.GetUserId(principal);
        if (userId is null)
        {
            return Array.Empty<ApplicationId>();
        }

        return await dbContext.ApplicationGrants
            .Where(g => g.UserId == userId)
            .Select(g => g.ApplicationId)
            .ToListAsync(cancellationToken);
    }
}
