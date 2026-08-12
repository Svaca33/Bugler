using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Contracts;
using Bugler.Access.Users;
using Microsoft.AspNetCore.Http;

namespace Bugler.Access.ResolveMachineActor;

/// <summary>
/// Reads the machine off the current principal. No database round-trip: the authentication
/// handler minted these claims from the row this very request, so they are as fresh as a query
/// would be — and a caller authenticated any other way simply has no delegation claim to read.
/// </summary>
internal sealed class MachineActorResolver(IHttpContextAccessor httpContextAccessor) : IMachineActor
{
    public MachineActor? GetCurrent()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        if (!Guid.TryParse(
                principal.FindFirstValue(MachineDelegationAuthenticationHandler.DelegationClaim),
                out var delegationId)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return null;
        }

        var holdsMachineHand = Enum.TryParse<MachineDelegationGrade>(
                principal.FindFirstValue(MachineDelegationAuthenticationHandler.GradeClaim),
                out var grade)
            && grade == MachineDelegationGrade.MachineHand;

        return new MachineActor(delegationId, userId, holdsMachineHand);
    }
}
