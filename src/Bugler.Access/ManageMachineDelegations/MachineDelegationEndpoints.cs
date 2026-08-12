using System.Security.Claims;
using Bugler.Access.Authentication;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ManageMachineDelegations;

/// <summary>One live Machine Delegation as its holder sees it. The Secret is not among these fields.</summary>
public sealed record MachineDelegationDto(
    Guid Id,
    string Name,
    Guid? ApplicationId,
    MachineDelegationGrade Grade,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt);

/// <summary>The same, plus whose it is — what an Admin needs to answer for the door they opened.</summary>
public sealed record HeldMachineDelegationDto(
    Guid Id,
    string Name,
    Guid UserId,
    string UserEmail,
    Guid? ApplicationId,
    MachineDelegationGrade Grade,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt);

/// <summary>Grade left unsaid means <see cref="MachineDelegationGrade.Reading"/> — the machine hand is asked for, never presumed.</summary>
public sealed record IssueMachineDelegationRequest(
    string Name, Guid? ApplicationId, int? LifetimeDays, MachineDelegationGrade? Grade);

/// <summary>
/// The one and only answer carrying a Secret. It is not stored in a form anything can read back,
/// so a caller that loses this response has lost the Machine Delegation and must issue another.
/// </summary>
public sealed record IssuedMachineDelegationDto(MachineDelegationDto MachineDelegation, string Secret);

internal static class MachineDelegationEndpoints
{
    private const int MinimumLifetimeDays = 1;
    private const int MaximumLifetimeDays = 365;

    public static async Task<IReadOnlyList<MachineDelegationDto>> ListOwn(
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = AuthEndpoints.GetUserId(principal);
        return await dbContext.MachineDelegations
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new MachineDelegationDto(
                d.Id, d.Name, d.ApplicationId == null ? null : d.ApplicationId.Value.Value,
                d.Grade, d.CreatedAt, d.ExpiresAt, d.LastUsedAt))
            .ToListAsync(cancellationToken);
    }

    public static async Task<IResult> Issue(
        IssueMachineDelegationRequest request,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = AccessMessages.For(await requestLanguage.GetAsync(cancellationToken));
        var userId = AuthEndpoints.GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > 100)
        {
            return Results.BadRequest(messages.MachineDelegationNameRequired);
        }

        var lifetime = request.LifetimeDays ?? MachineDelegationMaterial.DefaultLifetimeDays;
        if (lifetime is < MinimumLifetimeDays or > MaximumLifetimeDays)
        {
            return Results.BadRequest(
                messages.MachineDelegationLifetimeRange(MinimumLifetimeDays, MaximumLifetimeDays));
        }

        // A narrowing may only subtract (ADR 0029). Checked at issue for the sake of a clear
        // refusal; it is checked again on every query by GrantedVisibility, which is what makes it
        // true after the grant behind it is withdrawn.
        if (request.ApplicationId is { } application
            && !principal.IsInRole(AuthEndpoints.AdminRole)
            && !await dbContext.ApplicationGrants.AnyAsync(
                g => g.UserId == userId && g.ApplicationId == new ApplicationId(application),
                cancellationToken))
        {
            return Results.BadRequest(messages.MachineDelegationApplicationNotReadable);
        }

        var now = DateTimeOffset.UtcNow;
        var secret = MachineDelegationMaterial.GenerateSecret();
        var delegation = new MachineDelegation
        {
            Id = Guid.CreateVersion7(),
            UserId = userId.Value,
            Name = name,
            Fingerprint = MachineDelegationMaterial.Fingerprint(secret),
            ApplicationId = request.ApplicationId is { } id ? new ApplicationId(id) : null,
            Grade = request.Grade ?? MachineDelegationGrade.Reading,
            CreatedAt = now,
            ExpiresAt = now.AddDays(lifetime),
        };

        dbContext.MachineDelegations.Add(delegation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new IssuedMachineDelegationDto(
            new MachineDelegationDto(
                delegation.Id, delegation.Name, request.ApplicationId,
                delegation.Grade, delegation.CreatedAt, delegation.ExpiresAt, null),
            secret));
    }

    public static async Task<IResult> RevokeOwn(
        Guid id,
        ClaimsPrincipal principal,
        AccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = AuthEndpoints.GetUserId(principal);
        var revoked = await dbContext.MachineDelegations
            .Where(d => d.Id == id && d.UserId == userId && d.RevokedAt == null)
            .ExecuteUpdateAsync(
                d => d.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);

        return revoked == 0 ? Results.NotFound() : Results.NoContent();
    }

    /// <summary>
    /// Every live Machine Delegation on this server. Whoever holds the switch that opened the machine door
    /// has to be able to see what it opened (ADR 0029) — otherwise closing it is a decision made
    /// blind.
    /// </summary>
    public static async Task<IReadOnlyList<HeldMachineDelegationDto>> ListHeld(
        AccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Joined in the database, shaped afterwards: unwrapping a nullable ApplicationId is not
        // something the provider can translate, and this list is short enough to say so plainly.
        var held = await dbContext.MachineDelegations
            .Where(d => d.RevokedAt == null)
            .Join(dbContext.Users, d => d.UserId, u => u.Id, (d, u) => new { MachineDelegation = d, User = u })
            .OrderBy(row => row.User.Email).ThenByDescending(row => row.MachineDelegation.CreatedAt)
            .ToListAsync(cancellationToken);

        return held
            .Select(row => new HeldMachineDelegationDto(
                row.MachineDelegation.Id,
                row.MachineDelegation.Name,
                row.User.Id,
                row.User.Email,
                row.MachineDelegation.ApplicationId?.Value,
                row.MachineDelegation.Grade,
                row.MachineDelegation.CreatedAt,
                row.MachineDelegation.ExpiresAt,
                row.MachineDelegation.LastUsedAt))
            .ToList();
    }

    /// <summary>The narrower answer to a lost laptop than deactivating the person who lost it.</summary>
    public static async Task<IResult> RevokeHeld(
        Guid id,
        AccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revoked = await dbContext.MachineDelegations
            .Where(d => d.Id == id && d.RevokedAt == null)
            .ExecuteUpdateAsync(
                d => d.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);

        return revoked == 0 ? Results.NotFound() : Results.NoContent();
    }
}
