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
