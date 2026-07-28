using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || request.Password.Length < 8)
        {
            return Results.BadRequest("A valid e-mail and a password of at least 8 characters are required.");
        }

        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Results.Conflict("A user with this e-mail already exists.");
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = "",
            DisplayName = request.DisplayName,
            IsAdmin = request.IsAdmin,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.IsAdmin, false, []));
    }

    public static async Task<IResult> Deactivate(
        Guid id, ClaimsPrincipal principal, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        if (IsSelf(id, principal))
        {
            return Results.Conflict("An admin cannot deactivate their own account.");
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
    /// Removes a User for good; the database cascades their grants away. No Deactivation has to
    /// precede it — the two are separate answers, not two steps (ADR 0001).
    /// </summary>
    public static async Task<IResult> Delete(
        Guid id, ClaimsPrincipal principal, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        if (IsSelf(id, principal))
        {
            return Results.Conflict("An admin cannot delete their own account.");
        }

        var deleted = await dbContext.Users.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? Results.NotFound() : Results.NoContent();
    }

    /// <summary>
    /// The whole guard on a server keeping an Admin: since only an Admin reaches these endpoints,
    /// the last one can only be removed by themselves, and this is where that is refused (ADR 0001).
    /// </summary>
    private static bool IsSelf(Guid id, ClaimsPrincipal principal) =>
        AuthEndpoints.GetUserId(principal) == id;

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
