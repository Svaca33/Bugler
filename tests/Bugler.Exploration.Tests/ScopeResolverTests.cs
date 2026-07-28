using Bugler.Access.Contracts;
using Bugler.Exploration.Scoping;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Exploration.Tests;

public class ScopeResolverTests
{
    private static readonly ApplicationId Profilog = ApplicationId.New();
    private static readonly ApplicationId Crm = ApplicationId.New();
    private static readonly ServiceId DemoProdBackend = ServiceId.New();
    private static readonly ServiceId DemoProdMobile = ServiceId.New();
    private static readonly ServiceId DemoStagingBackend = ServiceId.New();
    private static readonly ServiceId VysocinaProdBackend = ServiceId.New();
    private static readonly ServiceId CrmBackend = ServiceId.New();

    private static readonly IReadOnlyList<CatalogService> Catalog =
    [
        new(DemoProdBackend, Profilog, "Profilog", "demo", "prod", "backend"),
        new(DemoProdMobile, Profilog, "Profilog", "demo", "prod", "mobile"),
        new(DemoStagingBackend, Profilog, "Profilog", "demo", "stg", "backend"),
        new(VysocinaProdBackend, Profilog, "Profilog", "vysocina", "prod", "backend"),
        new(CrmBackend, Crm, "CRM", "acme", "prod", "backend"),
    ];

    private static ScopeResolver Resolver(IReadOnlyCollection<ApplicationId>? visible) =>
        new(new FakeVisibility(visible), new FakeCatalog(Catalog));

    private static Task<Guid[]?> ResolveAsync(
        IReadOnlyCollection<ApplicationId>? visible,
        ApplicationId? application = null,
        string? serviceNamespace = null,
        string? environment = null,
        string? service = null) =>
        Resolver(visible).ResolveServiceIdsAsync(
            new SourceFilter(application, serviceNamespace, environment, service),
            CancellationToken.None);

    [Fact]
    public async Task Unrestricted_caller_with_an_open_filter_has_no_restriction()
    {
        Assert.Null(await ResolveAsync(visible: null));
    }

    [Fact]
    public async Task One_deployment_spans_every_service_registered_under_it()
    {
        var result = await ResolveAsync(visible: null, serviceNamespace: "demo", environment: "prod");

        Assert.NotNull(result);
        Assert.Equal(new[] { DemoProdBackend.Value, DemoProdMobile.Value }.Order(), result.Order());
    }

    [Fact]
    public async Task One_service_spans_every_deployment_that_runs_it()
    {
        var result = await ResolveAsync(visible: null, service: "backend");

        Assert.NotNull(result);
        Assert.DoesNotContain(DemoProdMobile.Value, result);
        Assert.Equal(
            new[] { DemoProdBackend.Value, DemoStagingBackend.Value, VysocinaProdBackend.Value, CrmBackend.Value }
                .Order(),
            result.Order());
    }

    [Fact]
    public async Task Facets_combine_down_to_a_single_service()
    {
        var result = await ResolveAsync(
            visible: null, serviceNamespace: "demo", environment: "prod", service: "mobile");

        Assert.Equal([DemoProdMobile.Value], result);
    }

    [Fact]
    public async Task Application_filter_expands_to_its_services()
    {
        var result = await ResolveAsync(visible: null, application: Profilog);

        Assert.NotNull(result);
        Assert.DoesNotContain(CrmBackend.Value, result);
        Assert.Equal(4, result.Length);
    }

    [Fact]
    public async Task Restricted_caller_sees_only_granted_applications()
    {
        Assert.Equal([CrmBackend.Value], await ResolveAsync(visible: [Crm]));
    }

    [Fact]
    public async Task Filters_cannot_widen_a_restricted_scope()
    {
        var result = await ResolveAsync(visible: [Crm], application: Profilog);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Unknown_facet_values_show_nothing_rather_than_everything()
    {
        var result = await ResolveAsync(visible: null, serviceNamespace: "nonexistent");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    private sealed class FakeVisibility(IReadOnlyCollection<ApplicationId>? visible) : IReadVisibility
    {
        public ValueTask<IReadOnlyCollection<ApplicationId>?> GetVisibleApplicationsAsync(
            CancellationToken cancellationToken) => ValueTask.FromResult(visible);
    }

    private sealed class FakeCatalog(IReadOnlyList<CatalogService> services) : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogService>> GetServicesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(services);
    }
}
