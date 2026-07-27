using System.Security.Claims;
using Bugler.Access.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.Authentication;

public sealed record SetupRequest(string Email, string Password, string? DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserDto(
    Guid Id, string Email, string? DisplayName, bool IsAdmin, IReadOnlyList<Guid> GrantedApplicationIds);

public sealed record AuthStatusDto(bool NeedsSetup);

internal static class AuthEndpoints
{
    /// <summary>First run: no users exist yet — whoever sets up the server becomes Admin.</summary>
    public static async Task<IResult> Setup(
        SetupRequest request,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return Results.Conflict("Setup has already been completed.");
        }

        if (!IsValidEmail(request.Email) || request.Password.Length < 8)
        {
            return Results.BadRequest("A valid e-mail and a password of at least 8 characters are required.");
        }

        var admin = new User
        {
            Id = Guid.CreateVersion7(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = "",
            DisplayName = request.DisplayName,
            IsAdmin = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        admin.PasswordHash = hasher.HashPassword(admin, request.Password);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SignInAsync(httpContext, admin);
        return Results.Ok(await ToCurrentUserAsync(admin, dbContext, cancellationToken));
    }

    public static async Task<AuthStatusDto> Status(AccessDbContext dbContext, CancellationToken cancellationToken) =>
        new(NeedsSetup: !await dbContext.Users.AnyAsync(cancellationToken));

    public static async Task<IResult> Login(
        LoginRequest request,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.DeactivatedAt is not null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        await SignInAsync(httpContext, user);
        return Results.Ok(await ToCurrentUserAsync(user, dbContext, cancellationToken));
    }

    public static async Task<IResult> Logout(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    public static async Task<IResult> Me(
        ClaimsPrincipal principal, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var user = userId is null
            ? null
            : await dbContext.Users.FirstOrDefaultAsync(
                u => u.Id == userId && u.DeactivatedAt == null, cancellationToken);

        return user is null
            ? Results.Unauthorized()
            : Results.Ok(await ToCurrentUserAsync(user, dbContext, cancellationToken));
    }

    internal static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static async Task SignInAsync(HttpContext httpContext, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };
        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private static async Task<CurrentUserDto> ToCurrentUserAsync(
        User user, AccessDbContext dbContext, CancellationToken cancellationToken)
    {
        var grants = user.IsAdmin
            ? []
            : await dbContext.ApplicationGrants
                .Where(g => g.UserId == user.Id)
                .Select(g => g.ApplicationId.Value)
                .ToListAsync(cancellationToken);

        return new CurrentUserDto(user.Id, user.Email, user.DisplayName, user.IsAdmin, grants);
    }

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Length <= 320;
}
