using System.Net;
using System.Net.Http.Json;
using Bugler.Access.ManageMachineDelegations;
using Bugler.Host;

namespace Bugler.IntegrationTests;

/// <summary>
/// The machine door and the Machine Delegation that opens it (ADR 0029, ADR 0030).
///
/// One thing the harness cannot reproduce: a <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>
/// has no ports, so every request looks to <c>ListenerSurfaces</c> like it arrived somewhere
/// unconfigured — which means the app surface's same-origin check applies to <c>/mcp</c> here and
/// never does in production, where 8081 is bound to <see cref="Surface.Mcp"/>. The clients below
/// therefore name their requests. What that costs is only that this file cannot prove the surfaces
/// are apart; what it does prove is everything the Machine Delegation itself decides.
/// </summary>
public sealed class MachineDelegationTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    /// <summary>A minimal MCP handshake: enough to see who the door let in, not a client.</summary>
    private static HttpContent Handshake() => JsonContent.Create(new
    {
        jsonrpc = "2.0",
        id = 1,
        method = "initialize",
        @params = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "delegation-tests", version = "1" },
        },
    });

    private HttpClient DoorClient(string secret)
    {
        var client = _harness.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secret}");
        // Streamable HTTP asks for both, and the server answers with whichever it needs.
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        return client;
    }

    private async Task<IssuedMachineDelegationDto> IssueAsync(
        HttpClient client, string name, Guid? applicationId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/machine-delegations", new { name, applicationId, lifetimeDays = (int?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssuedMachineDelegationDto>())!;
    }

    private async Task OpenTheDoorAsync() =>
        (await _harness.Client.PutAsJsonAsync(
            "/api/admin/mcp/settings",
            new { opened = true, publicUrl = "https://bugler.test/mcp" })).EnsureSuccessStatusCode();

    [Fact]
    public async Task The_door_is_closed_until_an_admin_opens_it()
    {
        var issued = await IssueAsync(_harness.Client, "laptop");

        var refused = await DoorClient(issued.Secret).PostAsync("/mcp", Handshake());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(McpDoor.Closed, await refused.Content.ReadAsStringAsync());

        await OpenTheDoorAsync();
        var admitted = await DoorClient(issued.Secret).PostAsync("/mcp", Handshake());
        admitted.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The Secret is the only thing that opens it. A Session cookie is not a Machine Delegation, however
    /// good it is elsewhere — the policy names one scheme and it is not the cookie's.
    /// </summary>
    [Fact]
    public async Task A_session_does_not_open_the_machine_door()
    {
        await OpenTheDoorAsync();

        var withCookie = _harness.Client;
        withCookie.DefaultRequestHeaders.Remove("Accept");
        withCookie.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        var response = await withCookie.PostAsync("/mcp", Handshake());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>And the other way round: what opens the door opens nothing else.</summary>
    [Fact]
    public async Task A_delegation_does_not_open_the_rest_api()
    {
        await OpenTheDoorAsync();
        var issued = await IssueAsync(_harness.Client, "laptop");

        var response = await DoorClient(issued.Secret).GetAsync("/api/logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_revoked_delegation_stops_reading_at_once()
    {
        await OpenTheDoorAsync();
        var issued = await IssueAsync(_harness.Client, "laptop");
        (await DoorClient(issued.Secret).PostAsync("/mcp", Handshake())).EnsureSuccessStatusCode();

        (await _harness.Client.DeleteAsync($"/api/machine-delegations/{issued.MachineDelegation.Id}"))
            .EnsureSuccessStatusCode();

        var response = await DoorClient(issued.Secret).PostAsync("/mcp", Handshake());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Deactivating the person behind it ends the Machine Delegation on the next request — the account is
    /// read back every time rather than trusted from the day the Secret was issued.
    /// </summary>
    [Fact]
    public async Task Deactivating_the_holder_ends_their_delegation()
    {
        await OpenTheDoorAsync();
        var holder = await _harness.CreateUserClientAsync(
            "holder@bugler.test", "HolderPass123!", _harness.ApplicationId);
        var issued = await IssueAsync(holder, "laptop");
        (await DoorClient(issued.Secret).PostAsync("/mcp", Handshake())).EnsureSuccessStatusCode();

        await _harness.DeactivateUserAsync("holder@bugler.test");

        var response = await DoorClient(issued.Secret).PostAsync("/mcp", Handshake());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A narrowing may only subtract. Asking for an Application the issuer cannot read is refused
    /// outright rather than quietly granted — and what the check is really for is the other half,
    /// tested below.
    /// </summary>
    [Fact]
    public async Task A_delegation_cannot_be_narrowed_to_an_application_its_issuer_cannot_read()
    {
        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "StrangerPass1!");

        var response = await stranger.PostAsJsonAsync(
            "/api/machine-delegations",
            new { name = "laptop", applicationId = _harness.ApplicationId, lifetimeDays = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// An Admin sees every Machine Delegation issued and may revoke any — the narrower answer to a lost
    /// laptop than deactivating the person who lost it (ADR 0029).
    /// </summary>
    [Fact]
    public async Task An_admin_sees_and_may_revoke_a_delegation_that_is_not_theirs()
    {
        await OpenTheDoorAsync();
        var holder = await _harness.CreateUserClientAsync(
            "holder@bugler.test", "HolderPass123!", _harness.ApplicationId);
        var issued = await IssueAsync(holder, "lost laptop");

        var held = await _harness.Client
            .GetFromJsonAsync<List<HeldMachineDelegationDto>>("/api/admin/machine-delegations");
        Assert.Contains(held!, d => d.Id == issued.MachineDelegation.Id && d.UserEmail == "holder@bugler.test");

        (await _harness.Client.DeleteAsync($"/api/admin/machine-delegations/{issued.MachineDelegation.Id}"))
            .EnsureSuccessStatusCode();

        var response = await DoorClient(issued.Secret).PostAsync("/mcp", Handshake());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A person sees only their own, and cannot revoke somebody else's through that door.</summary>
    [Fact]
    public async Task A_holder_sees_only_their_own_delegations()
    {
        var mine = await IssueAsync(_harness.Client, "admin laptop");
        var holder = await _harness.CreateUserClientAsync(
            "holder@bugler.test", "HolderPass123!", _harness.ApplicationId);
        await IssueAsync(holder, "their laptop");

        var theirs = await holder.GetFromJsonAsync<List<MachineDelegationDto>>("/api/machine-delegations");

        Assert.DoesNotContain(theirs!, d => d.Id == mine.MachineDelegation.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await holder.DeleteAsync($"/api/machine-delegations/{mine.MachineDelegation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await holder.GetAsync("/api/admin/machine-delegations")).StatusCode);
    }
}
