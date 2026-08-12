namespace Bugler.Access.Contracts;

/// <summary>One Machine Delegation as a mark on an Episode names it: what its holder called it, and whose it is.</summary>
public sealed record MachineDelegationName(string Name, string HolderEmail);

/// <summary>
/// Puts names to Machine Delegation ids for display — whose hand claimed, proposed, resigned.
/// Like <see cref="IUserNames"/> it says nothing about validity: a revoked or expired delegation
/// keeps its name, because the Journal still has to say whose hand it was. Ids that no longer
/// name a delegation are simply absent — the caller renders its own shrug.
/// </summary>
public interface IMachineDelegationNames
{
    Task<IReadOnlyDictionary<Guid, MachineDelegationName>> ResolveAsync(
        IReadOnlyCollection<Guid> delegationIds, CancellationToken cancellationToken);
}
