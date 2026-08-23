namespace Bugler.Access.Contracts;

/// <summary>
/// Answers the question a *listing* asks: which Applications is the current caller attending to?
/// Their Focus (see CONTEXT.md), resolved against their Visibility Scope, so it only ever subtracts
/// from <see cref="IReadVisibility"/> and never widens it. Null means unrestricted — a caller who
/// holds no Focus at all, which is everyone who is not a signed-in person. An empty set means
/// nothing is shown, and unlike an empty Visibility Scope that is a choice rather than a refusal.
///
/// Deliberately a contract of its own rather than a second mood of <see cref="IReadVisibility"/>
/// (ADR 0004): a lens belongs on a listing, while anything that authorizes one named thing — a
/// detail, a write, a Subscription's target — must keep asking for the right, or hiding would
/// become forbidding. The machine door asks for the right too; an architecture test holds it to it.
/// </summary>
public interface IReadApplicationFocus
{
    ValueTask<IReadOnlyCollection<ApplicationId>?> GetFocusedApplicationsAsync(
        CancellationToken cancellationToken);
}
