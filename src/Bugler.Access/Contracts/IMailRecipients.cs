using Bugler.SharedKernel;

namespace Bugler.Access.Contracts;

/// <summary>
/// Answers Alerting's question at the moment a mail is about to leave: which of these Users may
/// be told about this Application right now, and at what address? Unlike
/// <see cref="IReadVisibility"/> it has no notion of a current caller, so a background loop can
/// ask about anyone.
/// </summary>
public interface IMailRecipients
{
    Task<MailRecipientsResult> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, ApplicationId applicationId, CancellationToken cancellationToken);
}

public sealed record MailRecipient(Guid UserId, string Email);

/// <summary>
/// Deliverable = the User exists, is not deactivated, and is an Admin or holds a grant on the
/// Application. A known-but-undeliverable User is neither listed — they may be reactivated or
/// re-granted later, so the caller keeps waiting. Unknown ids no longer name a User at all.
/// </summary>
public sealed record MailRecipientsResult(
    IReadOnlyList<MailRecipient> Deliverable,
    IReadOnlyList<Guid> UnknownUserIds);
