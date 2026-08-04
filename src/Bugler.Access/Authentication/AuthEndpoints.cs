using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using Bugler.Access.ResetPassword;
using Bugler.Access.Users;
using Bugler.Mail;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bugler.Access.Authentication;

public sealed record SetupRequest(string Email, string Password, string? DisplayName);

public sealed record LoginRequest(string Email, string Password, bool StaySignedIn = false);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Null to follow the server's language; a supported code to speak for themselves.</summary>
public sealed record SetLanguageRequest(string? Language);

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsAdmin,
    IReadOnlyList<Guid> GrantedApplicationIds,
    /// <summary>The Language they chose, or null while they follow the server's.</summary>
    string? Language);

public sealed record AuthStatusDto(
    bool NeedsSetup,
    bool PasswordResetAvailable,
    /// <summary>The server's language — what the screens before sign-in speak (ADR 0024).</summary>
    string Language);

internal static class AuthEndpoints
{
    /// <summary>The role claim carried by an Admin's Session.</summary>
    internal const string AdminRole = "Admin";

    /// <summary>The User's Security Stamp as it stood when this Session was minted.</summary>
    internal const string SecurityStampClaim = "access.security_stamp";

    /// <summary>
    /// First run: no users exist yet — whoever sets up the server becomes Admin.
    /// </summary>
    /// <remarks>
    /// Serializable, because "nobody has claimed this server" is a fact about rows that are not
    /// there: the unique index on the e-mail does not catch two requests carrying two different
    /// addresses, and both would read an empty table and both would write an Admin. Postgres takes
    /// a predicate lock over that empty scan, so one of the two is refused at the end and is told
    /// what it would have been told a moment later anyway — the server is already set up.
    /// </remarks>
    public static async Task<IResult> Setup(
        SetupRequest request,
        AccessDbContext dbContext,
        IPasswordHasher<User> hasher,
        HttpContext httpContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return Results.Conflict(messages.SetupAlreadyCompleted);
        }

        if (!IsValidEmail(request.Email))
        {
            return Results.BadRequest(messages.ValidEmailRequired);
        }

        if (!Passwords.IsAcceptable(request.Password))
        {
            return Results.BadRequest(
                messages.PasswordRequirement(Passwords.MinimumLength, Passwords.MaximumLength));
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception failure) when (IsSerializationFailure(failure))
        {
            return Results.Conflict(messages.SetupAlreadyCompleted);
        }

        await SignInAsync(httpContext, admin, staySignedIn: false);
        return Results.Ok(await ToCurrentUserAsync(admin, dbContext, cancellationToken));
    }

    /// <summary>
    /// The loser of the race, however deeply it happens to be buried. PostgreSQL may raise 40001 on
    /// the statement or on the commit, and how many layers EF wraps the statement's version in is
    /// not ours to predict: a commit arrives bare, a failed insert arrives as a DbUpdateException,
    /// and EF wraps that again in an InvalidOperationException when it judges the failure transient.
    /// Counting the layers is what made this return 500 to the loser instead of the refusal it had
    /// already been given a name for, so the chain is walked to its end rather than to a fixed depth.
    /// </summary>
    private static bool IsSerializationFailure(Exception failure)
    {
        for (Exception? error = failure; error is not null; error = error.InnerException)
        {
            if (error is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            {
                return true;
            }
        }

        return false;
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
        IServerLanguage serverLanguage,
        CancellationToken cancellationToken) =>
        new(
            NeedsSetup: !await dbContext.Users.AnyAsync(cancellationToken),
            PasswordResetAvailable: ResetPasswordEndpoints.IsAvailable(
                options.Value, await smtpSettings.GetCurrentAsync(cancellationToken)),
            Language: (await serverLanguage.GetAsync(cancellationToken)).Code);

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

        // Every 401 below leaves in evened time (ADR 0023): whether the work behind it was a
        // verify, a lookup that found nobody, or nothing at all is not readable from the clock.
        var started = Stopwatch.GetTimestamp();

        // Neither a missing password nor one longer than any that can be set can be an account's,
        // so both are refused exactly as a wrong one is. The length is what keeps the promise
        // Passwords.MaximumLength makes, on the one endpoint where an anonymous caller reaches the
        // hasher; the null is what the hasher would otherwise throw over.
        if (request.Password is null or { Length: > Passwords.MaximumLength })
        {
            return await EvenedAnswer.After(started, Results.Unauthorized(), cancellationToken);
        }

        // The same normalisation the budget was spent under, and deliberately the same function:
        // an address that named one budget must name one User.
        var email = AttemptBudgets.KeyOf(request.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // The short-circuit is the point: an address nobody holds, and an account switched off,
        // both buy the refusal without a verify. What that used to cost in readable time the
        // evening below covers (ADR 0023).
        var verification = user is null || user.DeactivatedAt is not null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || verification == PasswordVerificationResult.Failed)
        {
            return await EvenedAnswer.After(started, Results.Unauthorized(), cancellationToken);
        }

        // The password was right, and its hash was written under a cheaper setting than today's.
        // Signing in is the only moment the password is in hand to write it again — and the only
        // way accounts made before the count was raised ever reach it (Passwords.Rehash).
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            Passwords.Rehash(user, request.Password, hasher);
            await dbContext.SaveChangesAsync(cancellationToken);
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
        IRequestLanguage requestLanguage,
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

        var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            return Results.BadRequest(messages.CurrentPasswordIncorrect);
        }

        if (!Passwords.IsAcceptable(request.NewPassword))
        {
            return Results.BadRequest(
                messages.PasswordRequirement(Passwords.MinimumLength, Passwords.MaximumLength));
        }

        Passwords.Set(user, request.NewPassword, hasher);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Whatever they chose about staying signed in, they chose it — a password change is no
        // occasion to decide it for them again.
        var existing = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignInAsync(httpContext, user, existing.Properties?.IsPersistent ?? false);
        return Results.NoContent();
    }

    /// <summary>
    /// A person choosing what language Bugler speaks to them — or handing the choice back to the
    /// server with null. Takes effect on the next answer; no Session has to be re-minted, because
    /// the request's language rides in on Accept-Language, never on the cookie (ADR 0024).
    /// </summary>
    public static async Task<IResult> SetLanguage(
        SetLanguageRequest request,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        IRequestLanguage requestLanguage,
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

        if (request.Language is null)
        {
            user.Language = null;
        }
        else if (Language.TryParse(request.Language, out var language))
        {
            user.Language = language;
        }
        else
        {
            var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.BadRequest(messages.UnknownLanguage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Signing out ends every Session of this User, not only the one that asked (ADR 0003). The
    /// cookie deleted here was never the whole of a Session — a copy of the ticket stays valid on
    /// its own — so the Security Stamp is rolled instead, and every Session minted before it stops
    /// counting on its next request.
    /// </summary>
    public static async Task<IResult> Logout(
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var user = userId is null
            ? null
            : await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is not null)
        {
            // Rolled and saved before the cookie goes: a write that fails must leave the caller
            // signed in and told so, never sign them out of this browser alone while the Sessions
            // they asked to end read on elsewhere.
            user.SecurityStamp = Guid.CreateVersion7();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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

        return new CurrentUserDto(
            user.Id, user.Email, user.DisplayName, user.IsAdmin, grants, user.Language?.Code);
    }

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Length <= 320;
}
