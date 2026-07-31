using Bugler.Access.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access.ResolveUserNames;

internal sealed class UserNameResolver(AccessDbContext dbContext) : IUserNames
{
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.ToArray();
        return await dbContext.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Email, cancellationToken);
    }
}
