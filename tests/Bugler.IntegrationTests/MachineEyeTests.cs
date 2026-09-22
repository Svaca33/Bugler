using System.Net.Http.Json;
using System.Text.Json;
using Bugler.Access.ManageMachineDelegations;
using Bugler.Exploration.Mcp;

namespace Bugler.IntegrationTests;

/// <summary>
/// What the machine door can be asked about telemetry, in the words its own descriptions offer
/// (ADR 0031). The Attribute Filter is the whole point here: the door tells a model to write one
/// `key=value`, and until this file existed nothing checked that such a Filter was ever accepted —
/// the query string's JSON was the only form the parser knew, and every pair was refused.
/// </summary>
public sealed class MachineEyeTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;
    private string _secret = null!;

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync();
        (await _harness.Client.PutAsJsonAsync(
            "/api/admin/mcp/settings",
            new { opened = true, publicUrl = "https://bugler.test/mcp" })).EnsureSuccessStatusCode();

        var issued = await _harness.Client.PostAsJsonAsync(
            "/api/machine-delegations",
            new { name = "eye", applicationId = (Guid?)null, lifetimeDays = (int?)null });
        issued.EnsureSuccessStatusCode();
        _secret = (await issued.Content.ReadFromJsonAsync<IssuedMachineDelegationDto>())!.Secret;

        await SeedLogsAsync();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_filter_written_key_equals_value_narrows_and_says_why_the_record_matched()
    {
        var answer = await SearchAsync(new { attributes = new[] { "XCareDataTypes=Employees" } });

        Assert.Equal(1, answer.Matched);
        var only = Assert.Single(answer.Items);
        Assert.Equal("Employee sync failed", only.Body);
        // The key the caller named comes back on the row, because it is why the row is there.
        var named = Assert.Single(only.Attributes!);
        Assert.Equal("XCareDataTypes", named.Key);
        Assert.Equal("Employees", named.Value);
    }

    /// <summary>
    /// A dot belongs to the key (ADR 0001). Were the pair split on it, this Filter would look for
    /// a `method` inside an `http` that is not there and answer "nothing matched" — which is the
    /// one answer that must never be wrong.
    /// </summary>
    [Fact]
    public async Task A_dotted_key_is_one_key_and_not_a_path()
    {
        var answer = await SearchAsync(new { attributes = new[] { "http.method=GET" } });

        Assert.Equal("Employee sync failed", Assert.Single(answer.Items).Body);
    }

    [Fact]
    public async Task A_nested_key_is_named_the_way_list_observed_keys_reports_it()
    {
        var answer = await SearchAsync(new
        {
            attributes = new[] { """{"path":["order","total"],"value":"42"}""" },
        });

        var only = Assert.Single(answer.Items);
        Assert.Equal("Checkout started", only.Body);
        Assert.Equal("42", only.Attributes!["order.total"]);
    }

    [Fact]
    public async Task Resource_attributes_are_filtered_the_same_way()
    {
        var narrowed = await SearchAsync(new { resourceAttributes = new[] { "service.name=web" } });
        Assert.Equal(2, narrowed.Matched);

        var none = await SearchAsync(new { resourceAttributes = new[] { "service.name=mobile" } });
        Assert.Empty(none.Items);
    }

    /// <summary>
    /// The refusal has to teach the form, because a model that wrote the wrong one has nothing else
    /// to go on — and an McpException is what carries its words through to the caller.
    /// </summary>
    [Fact]
    public async Task A_filter_that_is_neither_form_is_refused_in_words_that_say_how_to_write_one()
    {
        var body = await CallToolAsync(new { attributes = new[] { "XCareDataTypes" } });

        Assert.Contains("is not an Attribute Filter", body, StringComparison.Ordinal);
        Assert.Contains("key=value", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"items\"", body, StringComparison.Ordinal);
    }

    private async Task<LogSearchAnswer> SearchAsync(object arguments) =>
        AnswerOf<LogSearchAnswer>(await CallToolAsync(arguments));

    private static T AnswerOf<T>(string body)
    {
        var data = body.Split('\n').Single(line => line.StartsWith("data:", StringComparison.Ordinal))["data:".Length..];
        var text = JsonDocument.Parse(data).RootElement
            .GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonSerializer.Deserialize<T>(text, JsonSerializerOptions.Web)!;
    }

    /// <summary>One tools/call through the real door; the raw body comes back so a refusal is readable too.</summary>
    private async Task<string> CallToolAsync(object arguments)
    {
        var client = _harness.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_secret}");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        var response = await client.PostAsync("/mcp", JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new { name = "search_log_records", arguments },
        }));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private Task SeedLogsAsync() =>
        _harness.ExecuteSqlAsync(
            $$$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES
                ('{{{_harness.ServiceId}}}', now(), 17, 'Employee sync failed',
                 '{"service.name": "web"}',
                 '{"XCareDataTypes": "Employees", "http.method": "GET"}'),
                ('{{{_harness.ServiceId}}}', now(), 9, 'Checkout started',
                 '{"service.name": "web"}',
                 '{"order": {"total": 42}}'),
                ('{{{_harness.ServiceId}}}', now(), 9, 'Nothing to do here',
                 '{"service.name": "worker"}', '{}');
            """);
}
