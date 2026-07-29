using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.ManageSubscriptions;

namespace Bugler.IntegrationTests;

public sealed class AlertingSubscriptionTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_user_subscribes_inside_their_visibility_and_reads_it_back()
    {
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);

        var put = await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = new[] { _harness.ServiceId },
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var dto = await member.GetFromJsonAsync<SubscriptionsDto>("/api/alerting/subscriptions");
        Assert.Equal([_harness.ApplicationId], dto!.ApplicationIds);
        Assert.Equal([_harness.ServiceId], dto.ServiceIds);

        // The set semantics: sending a smaller picture removes what fell out of it.
        var shrink = await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = Array.Empty<Guid>(),
            serviceIds = new[] { _harness.ServiceId },
        });
        Assert.Equal(HttpStatusCode.NoContent, shrink.StatusCode);
        var after = await member.GetFromJsonAsync<SubscriptionsDto>("/api/alerting/subscriptions");
        Assert.Empty(after!.ApplicationIds);
        Assert.Equal([_harness.ServiceId], after.ServiceIds);
    }

    [Fact]
    public async Task Visibility_bounds_what_can_be_subscribed_to()
    {
        var (foreignApp, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);

        var foreignApplication = await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { foreignApp },
            serviceIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, foreignApplication.StatusCode);

        var foreignServicePut = await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = Array.Empty<Guid>(),
            serviceIds = new[] { foreignService },
        });
        Assert.Equal(HttpStatusCode.BadRequest, foreignServicePut.StatusCode);

        var unregistered = await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = Array.Empty<Guid>(),
            serviceIds = new[] { Guid.NewGuid() },
        });
        Assert.Equal(HttpStatusCode.BadRequest, unregistered.StatusCode);

        // An Admin is never scoped — the foreign application is theirs to watch too.
        var admin = await _harness.Client.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { foreignApp },
            serviceIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.NoContent, admin.StatusCode);
    }
}
