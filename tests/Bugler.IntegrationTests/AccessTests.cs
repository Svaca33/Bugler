using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bugler.Access.Authentication;
using Bugler.Exploration.SearchLogs;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// Authentication and Visibility Scope end to end: anonymous callers are rejected, setup runs once,
/// a non-admin only ever sees telemetry of granted Applications, and an issued Session keeps
/// answering to the database — deactivation or deletion ends it and the Admin role is read back,
/// not remembered. Deactivation and deletion are the two separate ways out of a User (ADR 0001).
/// </summary>
public sealed class AccessTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Anonymous_callers_get_401_from_the_read_api()
    {
        var anonymous = _harness.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/api/logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Setup_can_only_run_once()
    {
        var anonymous = _harness.CreateAnonymousClient();

        var second = await anonymous.PostAsJsonAsync(
            "/api/auth/setup",
            new { email = "intruder@evil.test", password = "Password123!" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var anonymous = _harness.CreateAnonymousClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/auth/login",
            new { email = BuglerHarness.AdminEmail, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// "Stay signed in" is observable in exactly one place: whether the session cookie carries an
    /// expiry — and so outlives the browser — or dies with it. Omitting the flag must keep the
    /// browser-session behaviour, which is what every caller predating the flag relies on.
    /// </summary>
    [Fact]
    public async Task Staying_signed_in_gives_the_session_cookie_an_expiry()
    {
        var plain = await SignInAndReadSessionCookieAsync(staySignedIn: null);
        var remembered = await SignInAndReadSessionCookieAsync(staySignedIn: true);

        Assert.False(HasExpiry(plain), $"Expected a browser-session cookie, got: {plain}");
        Assert.True(HasExpiry(remembered), $"Expected a persistent cookie, got: {remembered}");
    }

    [Fact]
    public async Task NonAdmin_sees_only_granted_applications_and_no_admin_api()
    {
        var (crmAppId, _, crmKey) = await _harness.SeedApplicationAsync("CRM", "acme", "prod", "backend");
        await IngestLogAsync(_harness.ApiKey, "eshop log line");
        await IngestLogAsync(crmKey, "crm log line");
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 2);

        var member = await _harness.CreateUserClientAsync("member@bugler.test", "MemberPass123!", crmAppId);

        var logs = await member.GetFromJsonAsync<SearchLogsResponse>("/api/logs");
        Assert.Equal("crm log line", Assert.Single(logs!.Items).Body);

        var adminApi = await member.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, adminApi.StatusCode);

        var admin = await _harness.Client.GetFromJsonAsync<SearchLogsResponse>("/api/logs");
        Assert.Equal(2, admin!.Items.Count);
    }

    [Fact]
    public async Task Deactivating_a_user_ends_the_session_they_already_hold()
    {
        const string email = "leaver@bugler.test";
        var member = await _harness.CreateUserClientAsync(email, "LeaverPass123!", _harness.ApplicationId);
        (await member.GetAsync("/api/logs")).EnsureSuccessStatusCode();

        await _harness.DeactivateUserAsync(email);

        var afterDeactivation = await member.GetAsync("/api/logs");
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivation.StatusCode);
    }

    /// <summary>
    /// Deletion is the end, not a deeper pause: the Session goes, the grants go with the row, and
    /// the e-mail is free for a new account — which is a different User, with nothing inherited.
    /// </summary>
    [Fact]
    public async Task Deleting_a_user_ends_their_session_and_takes_their_grants()
    {
        const string email = "deleted@bugler.test";
        var member = await _harness.CreateUserClientAsync(email, "DeletedPass123!", _harness.ApplicationId);
        (await member.GetAsync("/api/logs")).EnsureSuccessStatusCode();
        var userId = await _harness.FindUserIdAsync(email);

        (await _harness.Client.DeleteAsync($"/api/users/{userId}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/logs")).StatusCode);
        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT count(*) FROM access.application_grants WHERE user_id = '{userId}'", expected: 0));

        var recreated = await _harness.Client.PostAsJsonAsync(
            "/api/users", new { email, password = "SecondPass123!", displayName = (string?)null, isAdmin = false });
        recreated.EnsureSuccessStatusCode();
        Assert.NotEqual(userId, await _harness.FindUserIdAsync(email));
    }

    /// <summary>Deactivation is a pause, and the grants it kept are still there on the way back.</summary>
    [Fact]
    public async Task Reactivating_a_user_lets_them_back_in_with_their_grants_intact()
    {
        const string email = "returning@bugler.test";
        const string password = "ReturnPass123!";
        await _harness.CreateUserClientAsync(email, password, _harness.ApplicationId);
        var userId = await _harness.FindUserIdAsync(email);
        await _harness.DeactivateUserAsync(email);

        var whileDeactivated = await _harness.CreateAnonymousClient()
            .PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, whileDeactivated.StatusCode);

        (await _harness.Client.PostAsync($"/api/users/{userId}/reactivate", content: null))
            .EnsureSuccessStatusCode();

        var returned = _harness.CreateAnonymousClient();
        (await returned.PostAsJsonAsync("/api/auth/login", new { email, password })).EnsureSuccessStatusCode();
        var me = await returned.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.Equal(_harness.ApplicationId, Assert.Single(me!.GrantedApplicationIds));
    }

    /// <summary>
    /// The whole guard on a server keeping an Admin (ADR 0001): only an Admin reaches these
    /// endpoints, so the last one could only be removed by themselves — and is not allowed to be.
    /// </summary>
    [Fact]
    public async Task An_admin_can_remove_neither_themselves_nor_their_own_access()
    {
        var me = await _harness.Client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");

        var deactivation = await _harness.Client.PostAsync($"/api/users/{me!.Id}/deactivate", content: null);
        var deletion = await _harness.Client.DeleteAsync($"/api/users/{me.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deactivation.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deletion.StatusCode);
        (await _harness.Client.GetAsync("/api/users")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_role_follows_the_database_not_the_cookie_it_was_minted_into()
    {
        const string email = "promoted@bugler.test";
        var member = await _harness.CreateUserClientAsync(email, "PromotedPass123!");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/users")).StatusCode);

        await _harness.ExecuteSqlAsync($"UPDATE access.users SET is_admin = true WHERE email = '{email}'");
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/users")).StatusCode);

        await _harness.ExecuteSqlAsync($"UPDATE access.users SET is_admin = false WHERE email = '{email}'");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task A_changed_password_is_the_only_one_that_signs_in()
    {
        const string email = "changer@bugler.test";
        const string oldPassword = "OldPass123!";
        const string newPassword = "NewPass456!";
        var client = await _harness.CreateUserClientAsync(email, oldPassword, _harness.ApplicationId);

        (await client.PostAsJsonAsync(
                "/api/auth/password/change", new { currentPassword = oldPassword, newPassword }))
            .EnsureSuccessStatusCode();

        var fresh = _harness.CreateAnonymousClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await fresh.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword })).StatusCode);
        (await fresh.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword }))
            .EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The Security Stamp in the cookie is what makes this true: every Session minted from the old
    /// password is refused on its next request, except the one that did the changing.
    /// </summary>
    [Fact]
    public async Task Changing_a_password_ends_every_other_session_but_its_own()
    {
        const string email = "two-browsers@bugler.test";
        const string password = "TwoBrowsers123!";
        var here = await _harness.CreateUserClientAsync(email, password, _harness.ApplicationId);
        var elsewhere = _harness.CreateAnonymousClient();
        (await elsewhere.PostAsJsonAsync("/api/auth/login", new { email, password })).EnsureSuccessStatusCode();
        (await elsewhere.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();

        (await here.PostAsJsonAsync(
                "/api/auth/password/change",
                new { currentPassword = password, newPassword = "TwoBrowsers456!" }))
            .EnsureSuccessStatusCode();

        (await here.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/auth/me")).StatusCode);
    }

    /// <summary>
    /// Signing out is not a fact about one browser (ADR 0003): the Stamp is rolled, so the Session
    /// left behind elsewhere dies with the one that asked — and so would a copy of this one's
    /// ticket, which is the same thing said about a thief instead of a colleague.
    /// </summary>
    [Fact]
    public async Task Signing_out_ends_the_sessions_left_behind_elsewhere()
    {
        const string email = "forgetful@bugler.test";
        const string password = "Forgetful123!";
        var here = await _harness.CreateUserClientAsync(email, password, _harness.ApplicationId);
        var elsewhere = _harness.CreateAnonymousClient();
        (await elsewhere.PostAsJsonAsync("/api/auth/login", new { email, password })).EnsureSuccessStatusCode();
        (await elsewhere.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();

        (await here.PostAsync("/api/auth/logout", content: null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await here.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/auth/me")).StatusCode);
        (await _harness.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login", new { email, password }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_wrong_current_password_changes_nothing()
    {
        const string email = "careful@bugler.test";
        const string password = "CarefulPass123!";
        var client = await _harness.CreateUserClientAsync(email, password, _harness.ApplicationId);

        var response = await client.PostAsJsonAsync(
            "/api/auth/password/change",
            new { currentPassword = "not-the-password", newPassword = "WouldBeNew123!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        (await _harness.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login", new { email, password }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_new_password_below_the_minimum_is_refused()
    {
        const string email = "too-short@bugler.test";
        const string password = "LongEnough123!";
        var client = await _harness.CreateUserClientAsync(email, password, _harness.ApplicationId);

        var response = await client.PostAsJsonAsync(
            "/api/auth/password/change", new { currentPassword = password, newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        (await _harness.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login", new { email, password }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Nobody_changes_a_password_without_being_signed_in()
    {
        var response = await _harness.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/password/change",
            new { currentPassword = BuglerHarness.AdminPassword, newPassword = "Whatever123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// This harness is a loopback http server, so it is the other half of the pair
    /// <see cref="SessionCookieTests"/> states: a Bugler that cannot offer TLS must not ask the
    /// browser for it, because a Secure cookie here — or the __Host- prefix that demands one —
    /// would be dropped and sign-in would stop working altogether (ADR 0019).
    /// </summary>
    [Fact]
    public async Task A_server_without_TLS_asks_for_none()
    {
        var setCookie = await SignInAndReadSessionCookieAsync(staySignedIn: null);

        Assert.StartsWith("bugler.session=", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Signs in on a fresh cookie jar and returns the raw Set-Cookie value of the session cookie.</summary>
    private async Task<string> SignInAndReadSessionCookieAsync(bool? staySignedIn)
    {
        object credentials = staySignedIn is null
            ? new { email = BuglerHarness.AdminEmail, password = BuglerHarness.AdminPassword }
            : new { email = BuglerHarness.AdminEmail, password = BuglerHarness.AdminPassword, staySignedIn };

        var response = await _harness.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login", credentials);
        response.EnsureSuccessStatusCode();

        return Assert.Single(response.Headers.GetValues("Set-Cookie")
            .Where(cookie => cookie.StartsWith("bugler.session=", StringComparison.Ordinal)));
    }

    private static bool HasExpiry(string setCookie) =>
        setCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase)
        || setCookie.Contains("max-age=", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The whole of what a Focus is (ADR 0004): it empties the view without closing the door.
    /// The Admin here still reads everything — the Visibility Scope has not moved — so a request
    /// that names the Application is answered in full while an open one comes back with nothing,
    /// and neither is a refusal.
    /// </summary>
    [Fact]
    public async Task A_focus_empties_the_open_view_and_still_answers_a_named_application()
    {
        var (crmAppId, _, crmKey) = await _harness.SeedApplicationAsync("CRM", "acme", "prod", "backend");
        await IngestLogAsync(_harness.ApiKey, "eshop log line");
        await IngestLogAsync(crmKey, "crm log line");
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 2);
        var adminId = await _harness.FindUserIdAsync(BuglerHarness.AdminEmail);

        await _harness.StopAttendingAsync(adminId, crmAppId);

        var focused = await _harness.Client.GetFromJsonAsync<SearchLogsResponse>("/api/logs");
        Assert.Equal("eshop log line", Assert.Single(focused!.Items).Body);

        var named = await _harness.Client.GetAsync($"/api/logs?applicationId={crmAppId}");
        named.EnsureSuccessStatusCode();
        var hidden = await named.Content.ReadFromJsonAsync<SearchLogsResponse>();
        Assert.Equal("crm log line", Assert.Single(hidden!.Items).Body);
    }

    /// <summary>
    /// Attending to nothing is a state a person can reach, and Bugler answers it plainly rather
    /// than as an error — the browser is what turns the empty answer into a sentence.
    /// </summary>
    [Fact]
    public async Task An_empty_focus_answers_nothing_rather_than_refusing()
    {
        await IngestLogAsync(_harness.ApiKey, "eshop log line");
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 1);
        var adminId = await _harness.FindUserIdAsync(BuglerHarness.AdminEmail);

        await _harness.StopAttendingAsync(adminId, _harness.ApplicationId);

        var logs = await _harness.Client.GetAsync("/api/logs");
        logs.EnsureSuccessStatusCode();
        Assert.Empty((await logs.Content.ReadFromJsonAsync<SearchLogsResponse>())!.Items);

        var me = await _harness.Client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.Empty(me!.FocusedApplicationIds);
    }

    /// <summary>
    /// A Focus subtracts and only subtracts. The member is attending to both Applications, and
    /// holds a grant on one; what they read is still the one.
    /// </summary>
    [Fact]
    public async Task A_focus_cannot_widen_what_a_member_may_read()
    {
        var (crmAppId, _, crmKey) = await _harness.SeedApplicationAsync("CRM", "acme", "prod", "backend");
        await IngestLogAsync(_harness.ApiKey, "eshop log line");
        await IngestLogAsync(crmKey, "crm log line");
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 2);

        var member = await _harness.CreateUserClientAsync("focused@bugler.test", "FocusPass123!", crmAppId);

        var logs = await member.GetFromJsonAsync<SearchLogsResponse>("/api/logs");
        Assert.Equal("crm log line", Assert.Single(logs!.Items).Body);

        // Even named outright, the Application they hold no grant on stays theirs to not read.
        var named = await member.GetAsync($"/api/logs?applicationId={_harness.ApplicationId}");
        named.EnsureSuccessStatusCode();
        Assert.Empty((await named.Content.ReadFromJsonAsync<SearchLogsResponse>())!.Items);

        var me = await member.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.Equal(crmAppId, Assert.Single(me!.FocusedApplicationIds));
    }

    /// <summary>A Focus on nothing is what an Application's Deletion leaves behind, not a dead row.</summary>
    [Fact]
    public async Task Deleting_an_application_takes_it_out_of_every_focus()
    {
        var (crmAppId, _, _) = await _harness.SeedApplicationAsync("CRM", "acme", "prod", "backend");
        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT count(*) FROM access.application_focuses WHERE application_id = '{crmAppId}'", expected: 1));

        (await _harness.Client.DeleteAsync($"/api/admin/applications/{crmAppId}")).EnsureSuccessStatusCode();

        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT count(*) FROM access.application_focuses WHERE application_id = '{crmAppId}'", expected: 0));
    }

    private async Task IngestLogAsync(string apiKey, string body)
    {
        var request = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs();
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100,
            SeverityNumber = SeverityNumber.Info,
            Body = new AnyValue { StringValue = body },
        });
        resourceLogs.ScopeLogs.Add(scopeLogs);
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await _harness.CreateAnonymousClient().SendAsync(message);
        response.EnsureSuccessStatusCode();
    }
}
