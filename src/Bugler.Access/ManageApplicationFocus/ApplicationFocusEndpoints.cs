using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ManageApplicationFocus;

/// <summary>
/// A person choosing which Applications they attend to (see CONTEXT.md: Focus). One row per click,
/// like the grant checkboxes on the People tab — there is no draft of a Focus worth keeping, so
/// there is no Save.
///
/// Both verbs are idempotent, because a checkbox clicked twice in a flaky network is not an error.
/// </summary>
internal static class ApplicationFocusEndpoints
{
    public static async Task<IResult> Attend(
        Guid applicationId,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        if (AuthEndpoints.GetUserId(principal) is not { } userId)
        {
            return Results.Unauthorized();
        }

        // A Focus may only subtract, so it may not name what its holder could not read anyway.
        // Refused here for the sake of a clear answer; it is intersected again on every query by
        // FocusedApplications, which is what keeps it true after a grant is withdrawn.
        var application = new ApplicationId(applicationId);
        if (!principal.IsInRole(AuthEndpoints.AdminRole)
            && !await dbContext.ApplicationGrants.AnyAsync(
                g => g.UserId == userId && g.ApplicationId == application, cancellationToken))
        {
            var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.BadRequest(messages.FocusApplicationNotReadable);
        }

        if (!await dbContext.ApplicationFocuses.AnyAsync(
                f => f.UserId == userId && f.ApplicationId == application, cancellationToken))
        {
            dbContext.ApplicationFocuses.Add(new ApplicationFocus
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                ApplicationId = application,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    public static async Task<IResult> Ignore(
        Guid applicationId,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (AuthEndpoints.GetUserId(principal) is not { } userId)
        {
            return Results.Unauthorized();
        }

        // No readability check on the way out: letting go of an Application is never a widening,
        // and somebody whose grant has just been withdrawn must still be able to tidy their Focus.
        var application = new ApplicationId(applicationId);
        await dbContext.ApplicationFocuses
            .Where(f => f.UserId == userId && f.ApplicationId == application)
            .ExecuteDeleteAsync(cancellationToken);

        return Results.NoContent();
    }
}
