using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bugler.Exploration.SearchLogs;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// Authentication and Visibility Scope end to end: anonymous callers are rejected,
/// setup runs once, and a non-admin only ever sees telemetry of granted Applications.
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

    [Fact]
    public async Task NonAdmin_sees_only_granted_applications_and_no_admin_api()
    {
        var (crmAppId, _, crmKey) = await _harness.SeedApplicationAsync("CRM", "Acme");
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
