using Bugler.Access.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Access.ReadVisibility;

/// <summary>
/// Stand-in until the Login slice exists: every caller is treated as an Admin.
/// The per-user ApplicationGrant enforcement replaces this class, not its callers.
/// </summary>
internal sealed class UnrestrictedVisibility : IReadVisibility
{
    public ValueTask<IReadOnlyCollection<ApplicationId>?> GetVisibleApplicationsAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyCollection<ApplicationId>?>(null);
}
