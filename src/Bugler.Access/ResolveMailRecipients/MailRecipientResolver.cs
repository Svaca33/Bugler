using Bugler.Access.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ResolveMailRecipients;

internal sealed class MailRecipientResolver(
    AccessDbContext dbContext,
    IServerLanguage serverLanguage) : IMailRecipients
{
    public async Task<MailRecipientsResult> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, ApplicationId applicationId, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new MailRecipientsResult([], [], []);
        }

        var ids = userIds.ToArray();
        var users = await dbContext.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.IsAdmin, u.DeactivatedAt, u.Language })
            .ToListAsync(cancellationToken);

        var granted = await dbContext.ApplicationGrants
            .Where(g => ids.Contains(g.UserId) && g.ApplicationId == applicationId)
            .Select(g => g.UserId)
            .ToListAsync(cancellationToken);
        var grantedSet = granted.ToHashSet();

        // Attending to the Application is asked separately from being allowed to read it, because
        // the two undeliverables are answered differently: a missing grant may arrive, a Focus that
        // leaves this Application out is a decision (ADR 0004).
        var attending = await dbContext.ApplicationFocuses
            .Where(f => ids.Contains(f.UserId) && f.ApplicationId == applicationId)
            .Select(f => f.UserId)
            .ToListAsync(cancellationToken);
        var attendingSet = attending.ToHashSet();

        // Admins read everything without grants, so they are readable by role alone.
        var fallback = await serverLanguage.GetAsync(cancellationToken);
        var readable = users
            .Where(u => u.DeactivatedAt == null && (u.IsAdmin || grantedSet.Contains(u.Id)))
            .ToList();

        var deliverable = readable
            .Where(u => attendingSet.Contains(u.Id))
            .Select(u => new MailRecipient(u.Id, u.Email, u.Language ?? fallback))
            .ToList();

        var outsideFocus = readable
            .Where(u => !attendingSet.Contains(u.Id))
            .Select(u => u.Id)
            .ToList();

        var known = users.Select(u => u.Id).ToHashSet();
        var unknown = ids.Where(id => !known.Contains(id)).ToList();

        return new MailRecipientsResult(deliverable, unknown, outsideFocus);
    }
}
