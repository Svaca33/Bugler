using System.Net;
using System.Net.Http.Json;
using Bugler.Access.Authentication;
using Bugler.Access.ManageUsers;
using Bugler.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.IntegrationTests;

/// <summary>
/// The Password Reset end to end: what leaves in the mail, what the link buys, and everything it
/// stops buying — spent, expired, superseded, or belonging to somebody who may not sign in.
/// </summary>
public sealed class PasswordResetTests : IAsyncLifetime
{
    private const string Email = "forgetful@bugler.test";
    private const string OldPassword = "ForgottenPass123!";
    private const string NewPassword = "RememberedPass456!";

    private readonly RecordingMailQueue _mail = new();
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync(
            builder =>
            {
                builder.UseSetting("Mail:Smtp:Host", "smtp.test");
                builder.UseSetting("Mail:Smtp:From", "bugler@test.local");
                builder.ConfigureTestServices(services => services.AddSingleton<IMailQueue>(_mail));
            },
            publicBaseUrl: "https://bugler.test");
        await _harness.CreateUserClientAsync(Email, OldPassword, _harness.ApplicationId);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_answer_is_the_same_whether_or_not_the_address_belongs_to_an_account()
    {
        var known = await AskAsync(Email);
        var stranger = await AskAsync("nobody@bugler.test");

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, stranger.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await stranger.Content.ReadAsStringAsync());

        // Only one of the two was worth a mail, and the caller cannot tell which.
        Assert.Equal(Email, Assert.Single(_mail.Queued).ToEmail);
    }

    [Fact]
    public async Task The_link_in_the_mail_sets_a_new_password()
    {
        await AskAsync(Email);
        var token = RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued));

        (await ResetAsync(token, NewPassword)).EnsureSuccessStatusCode();

        var fresh = _harness.CreateAnonymousClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await fresh.PostAsJsonAsync(
                "/api/auth/login", new { email = Email, password = OldPassword })).StatusCode);
        (await fresh.PostAsJsonAsync("/api/auth/login", new { email = Email, password = NewPassword }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_ticket_buys_exactly_one_password()
    {
        await AskAsync(Email);
        var token = RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued));
        (await ResetAsync(token, NewPassword)).EnsureSuccessStatusCode();

        var second = await ResetAsync(token, "ThirdPassword789!");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        (await _harness.CreateAnonymousClient()
                .PostAsJsonAsync("/api/auth/login", new { email = Email, password = NewPassword }))
            .EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The throttle: the mail somebody is holding still works, so asking again changes nothing
    /// rather than filling their inbox.
    /// </summary>
    [Fact]
    public async Task A_second_ask_within_the_window_sends_nothing()
    {
        await AskAsync(Email);
        var first = Assert.Single(_mail.Queued);

        await AskAsync(Email);

        Assert.Equal(first, Assert.Single(_mail.Queued));
    }

    /// <summary>The newest mail is the one that works — the previous ticket does not survive it.</summary>
    [Fact]
    public async Task A_newer_ticket_voids_the_one_before_it()
    {
        await AskAsync(Email);
        var stale = RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued));

        // Step over the throttle window rather than waiting it out.
        await _harness.ExecuteSqlAsync(
            "UPDATE access.reset_tickets SET created_at = created_at - interval '5 minutes'");
        await AskAsync(Email);
        var newest = RecordingMailQueue.TokenIn(_mail.Queued[^1]);

        Assert.Equal(2, _mail.Queued.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(stale, NewPassword)).StatusCode);
        (await ResetAsync(newest, NewPassword)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_expired_ticket_buys_nothing()
    {
        await AskAsync(Email);
        var token = RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued));

        await _harness.ExecuteSqlAsync(
            "UPDATE access.reset_tickets SET expires_at = now() - interval '1 minute'");

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, NewPassword)).StatusCode);
        (await _harness.CreateAnonymousClient()
                .PostAsJsonAsync("/api/auth/login", new { email = Email, password = OldPassword }))
            .EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Unlike a Password Change, a reset spares no Session at all: whoever needed the link was
    /// holding none worth keeping.
    /// </summary>
    [Fact]
    public async Task A_reset_ends_every_session_of_the_account()
    {
        var elsewhere = _harness.CreateAnonymousClient();
        (await elsewhere.PostAsJsonAsync("/api/auth/login", new { email = Email, password = OldPassword }))
            .EnsureSuccessStatusCode();
        (await elsewhere.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();

        await AskAsync(Email);
        (await ResetAsync(RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued)), NewPassword))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_user_is_mailed_nothing()
    {
        await _harness.DeactivateUserAsync(Email);

        Assert.Equal(HttpStatusCode.Accepted, (await AskAsync(Email)).StatusCode);

        Assert.Empty(_mail.Queued);
    }

    /// <summary>A password Bugler refuses must not spend the one link standing between them and their account.</summary>
    [Fact]
    public async Task A_password_below_the_minimum_leaves_the_ticket_unspent()
    {
        await AskAsync(Email);
        var token = RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued));

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "short")).StatusCode);

        (await ResetAsync(token, NewPassword)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_invented_token_buys_nothing()
    {
        var response = await ResetAsync("not-a-real-ticket", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_server_that_can_mail_offers_the_reset()
    {
        var status = await _harness.CreateAnonymousClient()
            .GetFromJsonAsync<AuthStatusDto>("/api/auth/status");

        Assert.True(status!.PasswordResetAvailable);
    }

    /// <summary>
    /// The way out on a server that cannot mail at all — and the reason a forgotten password no
    /// longer costs the account, its grants and its subscriptions.
    /// </summary>
    [Fact]
    public async Task An_admin_hands_out_a_link_that_works()
    {
        var userId = await _harness.FindUserIdAsync(Email);

        var issued = await _harness.Client.PostAsync($"/api/users/{userId}/reset-ticket", content: null);
        issued.EnsureSuccessStatusCode();
        var ticket = await issued.Content.ReadFromJsonAsync<IssuedResetTicketDto>();

        // Nothing was mailed: the Admin is looking at the answer and passes it on themselves.
        Assert.Empty(_mail.Queued);
        (await ResetAsync(TokenIn(ticket!.Link), NewPassword)).EnsureSuccessStatusCode();
        (await _harness.CreateAnonymousClient()
                .PostAsJsonAsync("/api/auth/login", new { email = Email, password = NewPassword }))
            .EnsureSuccessStatusCode();
    }

    /// <summary>The newest ticket wins whichever way it was handed over.</summary>
    [Fact]
    public async Task A_link_from_an_admin_voids_the_one_that_was_mailed()
    {
        await AskAsync(Email);
        var mailed = RecordingMailQueue.TokenIn(Assert.Single(_mail.Queued));

        var issued = await _harness.Client.PostAsync(
            $"/api/users/{await _harness.FindUserIdAsync(Email)}/reset-ticket", content: null);
        var ticket = await issued.Content.ReadFromJsonAsync<IssuedResetTicketDto>();

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(mailed, NewPassword)).StatusCode);
        (await ResetAsync(TokenIn(ticket!.Link), NewPassword)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task No_link_is_handed_out_for_an_account_that_cannot_sign_in()
    {
        var userId = await _harness.FindUserIdAsync(Email);
        await _harness.DeactivateUserAsync(Email);

        var issued = await _harness.Client.PostAsync($"/api/users/{userId}/reset-ticket", content: null);

        Assert.Equal(HttpStatusCode.Conflict, issued.StatusCode);
    }

    [Fact]
    public async Task Only_an_admin_hands_out_links()
    {
        var member = await _harness.CreateUserClientAsync("member@bugler.test", "MemberPass123!");

        var issued = await member.PostAsync(
            $"/api/users/{await _harness.FindUserIdAsync(Email)}/reset-ticket", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, issued.StatusCode);
    }

    private static string TokenIn(string link) =>
        Uri.UnescapeDataString(link[(link.IndexOf("?token=", StringComparison.Ordinal) + 7)..]);

    private Task<HttpResponseMessage> AskAsync(string email) =>
        _harness.CreateAnonymousClient().PostAsJsonAsync("/api/auth/password/forgot", new { email });

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword) =>
        _harness.CreateAnonymousClient()
            .PostAsJsonAsync("/api/auth/password/reset", new { token, newPassword });
}

/// <summary>
/// A server without SMTP is the shipping default: it must say so plainly rather than promise mail
/// nobody will ever receive.
/// </summary>
public sealed class PasswordResetWithoutMailTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() =>
        // Said out loud rather than left to the defaults: the harness runs in Development, where
        // appsettings.Development.json points mail at the local mailpit.
        _harness = await BuglerHarness.StartAsync(builder =>
        {
            builder.UseSetting("Mail:Smtp:Host", "");
            builder.UseSetting("Mail:Smtp:From", "");
        });

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_reset_is_neither_offered_nor_accepted()
    {
        var anonymous = _harness.CreateAnonymousClient();

        var status = await anonymous.GetFromJsonAsync<AuthStatusDto>("/api/auth/status");
        var asked = await anonymous.PostAsJsonAsync(
            "/api/auth/password/forgot", new { email = BuglerHarness.AdminEmail });

        Assert.False(status!.PasswordResetAvailable);
        Assert.Equal(HttpStatusCode.NotFound, asked.StatusCode);
    }
}
