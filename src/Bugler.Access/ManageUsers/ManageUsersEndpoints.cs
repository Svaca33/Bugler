using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Outbox;
using Bugler.Access.ResetPassword;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bugler.Access.ManageUsers;

public sealed record UserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsAdmin,
    bool IsDeactivated,
    IReadOnlyList<Guid> GrantedApplicationIds);

public sealed record CreateUserRequest(string Email, string Password, string? DisplayName, bool IsAdmin);

public sealed record GrantRequest(Guid ApplicationId);

public sealed record IssuedResetTicketDto(string Link, DateTimeOffset ExpiresAt);

internal static class ManageUsersEndpoints
{
    public static async Task<IReadOnlyList<UserDto>> List(
        AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        var grants = await dbContext.ApplicationGrants.ToListAsync(cancellationToken);
        var users = await dbContext.Users.OrderBy(u => u.Email).ToListAsync(cancellationToken);

        return users.Select(user => new UserDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsAdmin,
                user.DeactivatedAt is not null,
                grants.Where(g => g.UserId == user.Id).Select(g => g.ApplicationId.Value).ToList()))
            .ToList();
    }

    public static async Task<IResult> Create(
        CreateUserRequest request,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));

        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Results.BadRequest(messages.ValidEmailRequired);
        }

        if (!Passwords.IsAcceptable(request.Password))
        {
            return Results.BadRequest(
                messages.PasswordRequirement(Passwords.MinimumLength, Passwords.MaximumLength));
        }

        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Results.Conflict(messages.UserWithEmailExists);
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = "",
            SecurityStamp = Guid.Empty,
            DisplayName = request.DisplayName,
            IsAdmin = request.IsAdmin,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Passwords.Set(user, request.Password, hasher);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.IsAdmin, false, []));
    }

    public static async Task<IResult> Deactivate(
        Guid id,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        if (IsSelf(id, principal))
        {
            var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.Conflict(messages.CannotDeactivateOwnAccount);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        user.DeactivatedAt ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Lets a deactivated User back in, with the grants deactivation left untouched.</summary>
    public static async Task<IResult> Reactivate(
        Guid id, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        user.DeactivatedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Removes a User for good; the database cascades their grants away and the recorded event
    /// lets Alerting drop their Subscriptions. No Deactivation has to precede it — the two are
    /// separate answers, not two steps (ADR 0001).
    /// </summary>
    public static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        AccessOutbox outbox,
        IOutboxSignal signal,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        if (IsSelf(id, principal))
        {
            var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.Conflict(messages.CannotDeleteOwnAccount);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var deleted = await dbContext.Users.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return Results.NotFound();
        }

        await outbox.EnqueueAsync(new UserDeleted(id), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        signal.Nudge(); // Only now — before the commit the dispatcher would find nothing.
        return Results.NoContent();
    }

    /// <summary>
    /// The whole guard on a server keeping an Admin: since only an Admin reaches these endpoints,
    /// the last one can only be removed by themselves, and this is where that is refused (ADR 0001).
    /// </summary>
    private static bool IsSelf(Guid id, ClaimsPrincipal principal) =>
        AuthEndpoints.GetUserId(principal) == id;

    /// <summary>
    /// Hands an Admin a Reset Ticket to pass on themselves. It is the same ticket the mail would
    /// have carried — an hour, once — and it is what a server without SMTP has instead of nothing.
    /// Without it a forgotten password could only be answered by deleting the account, which would
    /// take that person's grants and subscriptions with it (ADR 0002).
    /// </summary>
    public static async Task<IResult> IssueResetTicket(
        Guid id,
        AccessDbContext dbContext,
        IOptions<AccessOptions> options,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));

        var publicBaseUrl = options.Value.PublicBaseUrl;
        if (publicBaseUrl.Length == 0)
        {
            return Results.Conflict(messages.SetPublicBaseUrlFirst);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (user.DeactivatedAt is not null)
        {
            // A ticket would be honoured by nobody: redemption refuses a User who cannot sign in.
            return Results.Conflict(messages.ReactivateBeforeResetLink);
        }

        // No throttle here: nothing is being mailed, and the Admin asking is looking at the answer.
        var secret = await ResetPasswordEndpoints.IssueAsync(
            user, dbContext, respectInterval: false, cancellationToken);

        return Results.Ok(new IssuedResetTicketDto(
            ResetMail.Link(secret!, publicBaseUrl), DateTimeOffset.UtcNow + ResetTickets.Lifetime));
    }

    public static async Task<IResult> Grant(
        Guid id, GrantRequest request, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var applicationId = new ApplicationId(request.ApplicationId);
        var exists = await dbContext.ApplicationGrants
            .AnyAsync(g => g.UserId == id && g.ApplicationId == applicationId, cancellationToken);
        if (!exists)
        {
            dbContext.ApplicationGrants.Add(new ApplicationGrant
            {
                Id = Guid.CreateVersion7(),
                UserId = id,
                ApplicationId = applicationId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    public static async Task<IResult> Revoke(
        Guid id, Guid applicationId, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        var target = new ApplicationId(applicationId);
        await dbContext.ApplicationGrants
            .Where(g => g.UserId == id && g.ApplicationId == target)
            .ExecuteDeleteAsync(cancellationToken);
        return Results.NoContent();
    }
}
