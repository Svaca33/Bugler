using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bugler.Exploration.BrowseCatalog;
using Bugler.Registry.ManageApiKeys;
using Bugler.Registry.ManageCatalog;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// The admin lifecycle end to end: register an application and service, issue a key,
/// export with it, revoke it, and watch exports get rejected.
/// </summary>
public sealed class RegistryAdminTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Admin_can_register_sources_and_manage_keys()
    {
        var admin = _harness.Client;

        var appResponse = await admin.PostAsJsonAsync("/api/admin/applications", new { name = "CRM" });
        appResponse.EnsureSuccessStatusCode();
        var application = (await appResponse.Content.ReadFromJsonAsync<ApplicationDto>())!;

        var duplicate = await admin.PostAsJsonAsync("/api/admin/applications", new { name = "CRM" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var serviceRequest = new
        {
            applicationId = application.Id,
            @namespace = "globex",
            environment = "prod",
            name = "backend",
            retentionDays = 14,
        };
        var serviceResponse = await admin.PostAsJsonAsync("/api/admin/services", serviceRequest);
        serviceResponse.EnsureSuccessStatusCode();
        var service = (await serviceResponse.Content.ReadFromJsonAsync<ServiceDto>())!;
        Assert.Equal(14, service.RetentionDays);
        Assert.Equal("globex", service.Namespace);

        var duplicateService = await admin.PostAsJsonAsync("/api/admin/services", serviceRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateService.StatusCode);

        // Same deployment, different role: a separate registration with its own key.
        var mobile = await admin.PostAsJsonAsync(
            "/api/admin/services",
            new { applicationId = application.Id, @namespace = "globex", environment = "prod", name = "mobile" });
        mobile.EnsureSuccessStatusCode();

        var keyResponse = await admin.PostAsJsonAsync($"/api/admin/services/{service.Id}/keys", new { });
        keyResponse.EnsureSuccessStatusCode();
        var issued = (await keyResponse.Content.ReadFromJsonAsync<IssuedApiKeyDto>())!;
        Assert.StartsWith("blgr_", issued.Plaintext);

        Assert.Equal(HttpStatusCode.OK, (await IngestAsync(issued.Plaintext)).StatusCode);
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 1);

        var revoke = await admin.DeleteAsync($"/api/admin/keys/{issued.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await IngestAsync(issued.Plaintext)).StatusCode);

        var retention = await admin.PutAsJsonAsync(
            $"/api/admin/services/{service.Id}/retention", new { retentionDays = 7 });
        Assert.Equal(HttpStatusCode.NoContent, retention.StatusCode);
    }

    [Fact]
    public async Task Catalog_is_visibility_filtered()
    {
        var (crmAppId, _, _) = await _harness.SeedApplicationAsync("CRM", "acme", "prod", "backend");

        var adminCatalog = await _harness.Client.GetFromJsonAsync<CatalogResponse>("/api/catalog");
        Assert.Equal(2, adminCatalog!.Applications.Count);

        var member = await _harness.CreateUserClientAsync("viewer@bugler.test", "ViewerPass123!", crmAppId);
        var memberCatalog = await member.GetFromJsonAsync<CatalogResponse>("/api/catalog");
        var visible = Assert.Single(memberCatalog!.Applications);
        Assert.Equal("CRM", visible.Name);

        var adminApi = await member.GetAsync("/api/admin/applications");
        Assert.Equal(HttpStatusCode.Forbidden, adminApi.StatusCode);
    }

    private async Task<HttpResponseMessage> IngestAsync(string apiKey)
    {
        var request = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs();
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100,
            SeverityNumber = SeverityNumber.Info,
            Body = new AnyValue { StringValue = "from CRM" },
        });
        resourceLogs.ScopeLogs.Add(scopeLogs);
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await _harness.CreateAnonymousClient().SendAsync(message);
    }
}
