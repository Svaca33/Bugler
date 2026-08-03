using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Bugler.Access.Authentication;

/// <summary>
/// The evening of the clock on the doors that answer before anyone is signed in (ADR 0023). Their
/// words are already the same for everybody; how long Bugler thought about them was not — a sign-in
/// for an address nobody holds skipped the password verify and answered measurably sooner, which
/// answered the question the words refuse to. The handler notes when it began, does whatever the
/// request turns out to cost, and the answer leaves once the Floor has passed — however little of
/// it was spent working.
/// </summary>
/// <remarks>
/// Holding the answer is the whole of the cost: an async timer and a connection, never a thread and
/// never the PBKDF2 a dummy verify would have spent on every invented address (ADR 0023 rejects
/// that trade). What remains is a tail — under load a real verify can outgrow the Floor — and the
/// Attempt Budget is what keeps that tail unreadable, by pricing the samples (ADR 0021).
/// </remarks>
internal static class EvenedAnswer
{
    /// <summary>
    /// Above a password verify — PBKDF2-HMAC-SHA512 at <see cref="Users.Passwords.IterationCount"/>
    /// measures 112 ms on a 2026 developer laptop — and below what a person retyping a password
    /// would notice. Fixed rather than measured: a floor that moved with the hardware would be one
    /// more thing to observe. Raising the iteration count spends this margin, so it is a decision
    /// about this constant too (ADR 0023).
    /// </summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(300);

    /// <summary>Releases the answer once the Floor has passed since <paramref name="started"/>.</summary>
    public static async Task<IResult> After(
        long started, IResult result, CancellationToken cancellationToken)
    {
        var remaining = Floor - Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken);
        }

        return result;
    }
}
