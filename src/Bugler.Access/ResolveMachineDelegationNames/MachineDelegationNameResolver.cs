using Bugler.Access.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ResolveMachineDelegationNames;

internal sealed class MachineDelegationNameResolver(AccessDbContext dbContext) : IMachineDelegationNames
{
    public async Task<IReadOnlyDictionary<Guid, MachineDelegationName>> ResolveAsync(
        IReadOnlyCollection<Guid> delegationIds, CancellationToken cancellationToken)
    {
        if (delegationIds.Count == 0)
        {
            return new Dictionary<Guid, MachineDelegationName>();
        }

        // Revoked and expired delegations answer too: the Journal still names whose hand it was.
        var ids = delegationIds.ToArray();
        return await dbContext.MachineDelegations
            .Where(d => ids.Contains(d.Id))
            .Join(dbContext.Users, d => d.UserId, u => u.Id,
                (d, u) => new { d.Id, d.Name, u.Email })
            .ToDictionaryAsync(
                row => row.Id,
                row => new MachineDelegationName(row.Name, row.Email),
                cancellationToken);
    }
}
