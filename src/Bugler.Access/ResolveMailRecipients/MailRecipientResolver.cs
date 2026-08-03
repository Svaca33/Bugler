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
            return new MailRecipientsResult([], []);
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

        // Admins read everything without grants, so they are deliverable by role alone.
        var fallback = await serverLanguage.GetAsync(cancellationToken);
        var deliverable = users
            .Where(u => u.DeactivatedAt == null && (u.IsAdmin || grantedSet.Contains(u.Id)))
            .Select(u => new MailRecipient(u.Id, u.Email, u.Language ?? fallback))
            .ToList();

        var known = users.Select(u => u.Id).ToHashSet();
        var unknown = ids.Where(id => !known.Contains(id)).ToList();

        return new MailRecipientsResult(deliverable, unknown);
    }
}
