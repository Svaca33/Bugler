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

/// <summary>
/// Language is the one this User is spoken to in — their own choice where they made one, the
/// server's otherwise. Resolved here so the caller composes and never chooses (ADR 0024).
/// </summary>
public sealed record MailRecipient(Guid UserId, string Email, Language Language);

/// <summary>
/// Deliverable = the User exists, is not deactivated, is an Admin or holds a grant on the
/// Application, and is attending to it. A known-but-undeliverable User is in none of these lists —
/// they may be reactivated or re-granted later, so the caller keeps waiting. Unknown ids no longer
/// name a User at all.
///
/// <paramref name="OutsideFocus"/> is the third answer, and the reason there are three rather than
/// two: these Users could be told and have said they do not want to be (see CONTEXT.md: Focus).
/// Waiting on them would be waiting for somebody to change their mind, so the caller stops instead
/// of retrying — mail about trouble already solved is worse than no mail (ADR 0004).
/// </summary>
public sealed record MailRecipientsResult(
    IReadOnlyList<MailRecipient> Deliverable,
    IReadOnlyList<Guid> UnknownUserIds,
    IReadOnlyList<Guid> OutsideFocus);
