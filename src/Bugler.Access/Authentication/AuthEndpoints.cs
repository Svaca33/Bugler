using System.Security.Claims;
using Bugler.Access.ResetPassword;
using Bugler.Access.Users;
using Bugler.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bugler.Access.Authentication;

public sealed record SetupRequest(string Email, string Password, string? DisplayName);

public sealed record LoginRequest(string Email, string Password, bool StaySignedIn = false);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CurrentUserDto(
    Guid Id, string Email, string? DisplayName, bool IsAdmin, IReadOnlyList<Guid> GrantedApplicationIds);

public sealed record AuthStatusDto(bool NeedsSetup, bool PasswordResetAvailable);

internal static class AuthEndpoints
{
    /// <summary>The role claim carried by an Admin's Session.</summary>
    internal const string AdminRole = "Admin";

    /// <summary>The User's Security Stamp as it stood when this Session was minted.</summary>
    internal const string SecurityStampClaim = "access.security_stamp";

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

        if (!IsValidEmail(request.Email))
        {
            return Results.BadRequest("A valid e-mail address is required.");
        }

        if (!Passwords.IsAcceptable(request.Password))
        {
            return Results.BadRequest(Passwords.Requirement);
        }

        var admin = new User
        {
            Id = Guid.CreateVersion7(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = "",
            SecurityStamp = Guid.Empty,
            DisplayName = request.DisplayName,
            IsAdmin = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Passwords.Set(admin, request.Password, hasher);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SignInAsync(httpContext, admin, staySignedIn: false);
        return Results.Ok(await ToCurrentUserAsync(admin, dbContext, cancellationToken));
    }

    /// <summary>
    /// What the sign-in page needs before it can render: whether this server is still unclaimed,
    /// and whether it can offer a reset at all — a server without SMTP hides the link rather than
    /// promising a mail that never comes.
    /// </summary>
    public static async Task<AuthStatusDto> Status(
        AccessDbContext dbContext,
        IOptions<AccessOptions> options,
        ISmtpSettingsSource smtpSettings,
        CancellationToken cancellationToken) =>
        new(
            NeedsSetup: !await dbContext.Users.AnyAsync(cancellationToken),
            PasswordResetAvailable: ResetPasswordEndpoints.IsAvailable(
                options.Value, await smtpSettings.GetCurrentAsync(cancellationToken)));

    public static async Task<IResult> Login(
        LoginRequest request,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        AttemptBudgets budgets,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Before the lookup and long before the hasher: an address that has spent its Attempt
        // Budget buys no work at all, which is the point of having one (ADR 0021).
        if (!budgets.TrySignIn(request.Email, out var retryAfter))
        {
            return AttemptBudgets.Refuse(httpContext, retryAfter);
        }

        // Neither a missing password nor one longer than any that can be set can be an account's,
        // so both are refused exactly as a wrong one is. The length is what keeps the promise
        // Passwords.MaximumLength makes, on the one endpoint where an anonymous caller reaches the
        // hasher; the null is what the hasher would otherwise throw over.
        if (request.Password is null or { Length: > Passwords.MaximumLength })
        {
            return Results.Unauthorized();
        }

        // The same normalisation the budget was spent under, and deliberately the same function:
        // an address that named one budget must name one User.
        var email = AttemptBudgets.KeyOf(request.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.DeactivatedAt is not null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        await SignInAsync(httpContext, user, request.StaySignedIn);
        return Results.Ok(await ToCurrentUserAsync(user, dbContext, cancellationToken));
    }

    /// <summary>
    /// A Password Change: the User proves themselves with the password they are replacing. Every
    /// other Session dies with the Security Stamp it was minted from; this one is re-issued, so
    /// nobody is thrown out of the browser they just used to change it.
    /// </summary>
    public static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var user = userId is null
            ? null
            : await dbContext.Users.FirstOrDefaultAsync(
                u => u.Id == userId && u.DeactivatedAt == null, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            return Results.BadRequest("The current password is not correct.");
        }

        if (!Passwords.IsAcceptable(request.NewPassword))
        {
            return Results.BadRequest(Passwords.Requirement);
        }

        Passwords.Set(user, request.NewPassword, hasher);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Whatever they chose about staying signed in, they chose it — a password change is no
        // occasion to decide it for them again.
        var existing = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignInAsync(httpContext, user, existing.Properties?.IsPersistent ?? false);
        return Results.NoContent();
    }

    public static async Task<IResult> Logout(HttpContext httpContext, CancellationToken cancellationToken)
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

    private static async Task SignInAsync(HttpContext httpContext, User user, bool staySignedIn)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(SecurityStampClaim, user.SecurityStamp.ToString()),
        };
        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AdminRole));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Persisting the cookie is the whole of "stay signed in" — the ticket keeps its sliding
        // lifetime either way; only surviving a browser restart is at stake.
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = staySignedIn });
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
