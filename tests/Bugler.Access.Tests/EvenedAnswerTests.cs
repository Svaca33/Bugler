using System.Diagnostics;
using Bugler.Access.Authentication;
using Microsoft.AspNetCore.Http;

namespace Bugler.Access.Tests;

/// <summary>
/// The Evened Answer as a mechanism: an answer that cost nothing waits out the Floor, and one that
/// already cost more than the Floor is not punished further (ADR 0023). What the evening protects
/// — that a stranger's 401 arrives no sooner than a User's — is asserted where both can be asked
/// for, in the integration tests.
/// </summary>
public class EvenedAnswerTests
{
    [Fact]
    public async Task An_answer_that_cost_nothing_still_waits_out_the_floor()
    {
        var started = Stopwatch.GetTimestamp();
        var result = Results.Unauthorized();

        var answered = await EvenedAnswer.After(started, result, CancellationToken.None);

        Assert.Same(result, answered);
        Assert.True(
            Stopwatch.GetElapsedTime(started) >= EvenedAnswer.Floor,
            "an answer released before the Floor is the timing oracle again");
    }

    [Fact]
    public async Task An_answer_that_already_outgrew_the_floor_is_released_at_once()
    {
        // A timestamp two Floors in the past: the handler's work has already outspent the Floor.
        var started = Stopwatch.GetTimestamp()
            - (long)(Stopwatch.Frequency * EvenedAnswer.Floor.TotalSeconds * 2);

        var release = Stopwatch.GetTimestamp();
        await EvenedAnswer.After(started, Results.Unauthorized(), CancellationToken.None);

        Assert.True(
            Stopwatch.GetElapsedTime(release) < EvenedAnswer.Floor,
            "an answer past the Floor has nothing left to wait for");
    }
}
