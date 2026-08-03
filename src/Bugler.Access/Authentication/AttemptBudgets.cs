using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Bugler.Access.Authentication;

/// <summary>
/// The Attempt Budgets of every address that has asked recently, and the only place that spends
/// them. Counting is by address rather than by caller on purpose (ADR 0021): Bugler sits behind a
/// proxy and sees one connection address for everybody, so a budget kept per connection would count
/// a whole company as one guesser and refuse them together.
/// </summary>
/// <remarks>
/// The budgets live in memory, which is the whole of their storage: a restart hands everybody a
/// full budget again, and nobody can order a restart. Addresses nobody has asked about are dropped
/// by the partitioned limiter itself, so a spray across invented addresses costs memory only while
/// it lasts — and a spray large enough to matter is an HTTP flood, which is the proxy's business
/// and not Bugler's.
/// </remarks>
internal sealed class AttemptBudgets : IDisposable
{
    /// <summary>
    /// Said to whoever has spent their budget, whatever they were asking for and whether or not the
    /// address belongs to an account. It names the endpoint's own state and nothing else — a
    /// sentence that admitted a budget belongs to a real account would turn the refusal into the
    /// answer the sign-in form refuses to give.
    /// </summary>
    private const string Spent = "Too many attempts. Wait a moment and try again.";

    /// <summary>
    /// Ten in a row for somebody unsure which password they used, then one back every half minute.
    /// The bucket is what bounds guessing: the burst is generous enough that a person never meets
    /// it, and the trickle is what an attacker is left with — and what an address whose budget was
    /// spent on purpose by somebody else waits out, seconds rather than a whole window.
    /// </summary>
    private static readonly TokenBucketRateLimiterOptions SignIn = new()
    {
        TokenLimit = 10,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromSeconds(30),
        AutoReplenishment = true,
        QueueLimit = 0,
    };

    /// <summary>
    /// Asking for a reset link is rarer and costs more, so it is tighter — and paced to
    /// <see cref="ResetPassword.ResetTickets.MailInterval"/>, because a budget that let somebody ask
    /// faster than a mail can be sent would only buy them the same answer about a mail nobody sent.
    /// </summary>
    private static readonly TokenBucketRateLimiterOptions ResetRequest = new()
    {
        TokenLimit = 3,
        TokensPerPeriod = 1,
        ReplenishmentPeriod = TimeSpan.FromMinutes(2),
        AutoReplenishment = true,
        QueueLimit = 0,
    };

    private readonly PartitionedRateLimiter<string> _signIn = Partitioned(SignIn);
    private readonly PartitionedRateLimiter<string> _resetRequest = Partitioned(ResetRequest);

    /// <summary>Spends one sign-in attempt, or reports how long the address must wait.</summary>
    public bool TrySignIn(string? email, out TimeSpan retryAfter) =>
        TrySpend(_signIn, email, SignIn, out retryAfter);

    /// <summary>Spends one ask for a reset link, or reports how long the address must wait.</summary>
    public bool TryResetRequest(string? email, out TimeSpan retryAfter) =>
        TrySpend(_resetRequest, email, ResetRequest, out retryAfter);

    /// <summary>
    /// The one way an address becomes a budget's name, and deliberately the same normalisation the
    /// handlers apply before looking a User up: without it <c>Alice@…</c> and <c>alice@…</c> would
    /// hold separate budgets, and spelling the address differently would be all it took to be
    /// counted afresh.
    /// </summary>
    internal static string KeyOf(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>
    /// The answer to a spent budget: 429 with the wait in <c>Retry-After</c>. Honest about being
    /// counted — whoever is guessing learns their rate is bounded, and whoever mistyped their
    /// password twice learns when to try again.
    /// </summary>
    public static IResult Refuse(HttpContext httpContext, TimeSpan retryAfter)
    {
        httpContext.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return Results.Json(Spent, statusCode: StatusCodes.Status429TooManyRequests);
    }

    public void Dispose()
    {
        _signIn.Dispose();
        _resetRequest.Dispose();
    }

    private static PartitionedRateLimiter<string> Partitioned(TokenBucketRateLimiterOptions options) =>
        PartitionedRateLimiter.Create<string, string>(
            email => RateLimitPartition.GetTokenBucketLimiter(email, _ => options));

    private static bool TrySpend(
        PartitionedRateLimiter<string> budgets,
        string? email,
        TokenBucketRateLimiterOptions options,
        out TimeSpan retryAfter)
    {
        using var lease = budgets.AttemptAcquire(KeyOf(email));
        if (lease.IsAcquired)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        // The limiter states the wait wherever it can work it out; the replenishment period is the
        // same answer said less precisely, and is never worse than one period out.
        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var stated)
            ? stated
            : options.ReplenishmentPeriod;
        return false;
    }
}
