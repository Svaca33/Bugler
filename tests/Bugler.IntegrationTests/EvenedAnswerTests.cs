using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Bugler.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.IntegrationTests;

/// <summary>
/// The Evened Answer as a caller meets it (ADR 0023): every refusal from the sign-in door and the
/// one sentence from the reset door leave no sooner than the Floor, whatever work — a verify, a
/// lookup that found nobody, a mail worth sending — happened behind them. Only lower bounds are
/// asserted: that the fast paths stay fast is visible in the handlers, and an upper bound measured
/// on CI would be a coin toss.
/// </summary>
public sealed class EvenedAnswerTests : IAsyncLifetime
{
    /// <summary>The observable contract of EvenedAnswer.Floor, spelt as a caller would meet it.</summary>
    private static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(300);

    private const string DeactivatedEmail = "benched@bugler.test";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync(builder =>
        {
            // A server that can mail is the one whose reset door says the evened sentence at all —
            // without SMTP it answers 404 before any account is looked for.
            builder.UseSetting("Mail:Smtp:Host", "smtp.test");
            builder.UseSetting("Mail:Smtp:From", "bugler@test.local");
            builder.ConfigureTestServices(
                services => services.AddSingleton<IMailQueue>(new RecordingMailQueue()));
        });
        await _harness.CreateUserClientAsync(DeactivatedEmail, "SoonBenched123!", _harness.ApplicationId);
        await _harness.DeactivateUserAsync(DeactivatedEmail);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task An_address_nobody_holds_is_refused_no_sooner_than_the_floor()
    {
        var (response, elapsed) = await TimedAsync(
            "/api/auth/login", new { email = "nobody@bugler.test", password = "NotAPassword123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertWaitedOutTheFloor(elapsed);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_no_sooner_than_the_floor()
    {
        var (response, elapsed) = await TimedAsync(
            "/api/auth/login", new { email = BuglerHarness.AdminEmail, password = "NotThePassword123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertWaitedOutTheFloor(elapsed);
    }

    /// <summary>
    /// The fast answer for a Deactivation was its own leak: it told a holder of the right password
    /// that the account stands but is switched off. Deactivation skips the verify entirely, so
    /// without the evening this 401 would be the early one.
    /// </summary>
    [Fact]
    public async Task A_deactivated_account_is_refused_no_sooner_than_the_floor()
    {
        var (response, elapsed) = await TimedAsync(
            "/api/auth/login", new { email = DeactivatedEmail, password = "SoonBenched123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertWaitedOutTheFloor(elapsed);
    }

    [Fact]
    public async Task The_reset_sentence_reaches_a_stranger_no_sooner_than_the_floor()
    {
        var (response, elapsed) = await TimedAsync(
            "/api/auth/password/forgot", new { email = "nobody@bugler.test" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        AssertWaitedOutTheFloor(elapsed);
    }

    private async Task<(HttpResponseMessage Response, TimeSpan Elapsed)> TimedAsync(
        string path, object body)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await _harness.CreateAnonymousClient().PostAsJsonAsync(path, body);
        return (response, Stopwatch.GetElapsedTime(started));
    }

    private static void AssertWaitedOutTheFloor(TimeSpan elapsed) =>
        Assert.True(
            elapsed >= Floor,
            $"answered after {elapsed.TotalMilliseconds:F0} ms — before the floor, "
                + "which is the timing oracle of issue #28 again");
}
