using Bugler.SharedKernel;

namespace Bugler.Access.Contracts;

/// <summary>
/// Answers Exploration's question: which Applications may the current caller read?
/// Null means unrestricted (an Admin); an empty set means the caller sees nothing.
/// </summary>
public interface IReadVisibility
{
    ValueTask<IReadOnlyCollection<ApplicationId>?> GetVisibleApplicationsAsync(CancellationToken cancellationToken);
}
