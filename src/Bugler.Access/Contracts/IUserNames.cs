namespace Bugler.Access.Contracts;

/// <summary>
/// Puts names to User ids for display — who acknowledged, who solved. Unlike
/// <see cref="IMailRecipients"/> it says nothing about deliverability or grants: a name is not
/// an authorization, so deactivated Users keep theirs. Ids that no longer name a User are simply
/// absent — the caller renders its own shrug.
/// </summary>
public interface IUserNames
{
    Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
