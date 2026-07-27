using Bugler.Access.Contracts;
using Bugler.Exploration.Scoping;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Exploration.Tests;

public class ScopeResolverTests
{
    private static readonly ApplicationId Eshop = ApplicationId.New();
    private static readonly ApplicationId Crm = ApplicationId.New();
    private static readonly InstanceId EshopAcme = InstanceId.New();
    private static readonly InstanceId EshopGlobex = InstanceId.New();
    private static readonly InstanceId CrmAcme = InstanceId.New();

    private static readonly IReadOnlyList<CatalogInstance> Catalog =
    [
        new(EshopAcme, Eshop, "Acme", "Eshop"),
        new(EshopGlobex, Eshop, "Globex", "Eshop"),
        new(CrmAcme, Crm, "Acme", "CRM"),
    ];

    private static ScopeResolver Resolver(IReadOnlyCollection<ApplicationId>? visible) =>
        new(new FakeVisibility(visible), new FakeCatalog(Catalog));

    [Fact]
    public async Task Unrestricted_caller_without_filters_has_no_instance_restriction()
    {
        var result = await Resolver(visible: null).ResolveInstanceIdsAsync(null, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Unrestricted_caller_keeps_an_explicit_instance_filter()
    {
        var result = await Resolver(visible: null)
            .ResolveInstanceIdsAsync(null, EshopAcme, CancellationToken.None);

        Assert.Equal([EshopAcme.Value], result);
    }

    [Fact]
    public async Task Application_filter_expands_to_its_instances()
    {
        var result = await Resolver(visible: null)
            .ResolveInstanceIdsAsync(Eshop, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new[] { EshopAcme.Value, EshopGlobex.Value }.Order(), result.Order());
        Assert.DoesNotContain(CrmAcme.Value, result);
    }

    [Fact]
    public async Task Restricted_caller_sees_only_granted_applications()
    {
        var result = await Resolver(visible: [Crm]).ResolveInstanceIdsAsync(null, null, CancellationToken.None);

        Assert.Equal([CrmAcme.Value], result);
    }

    [Fact]
    public async Task Filters_cannot_widen_a_restricted_scope()
    {
        var result = await Resolver(visible: [Crm])
            .ResolveInstanceIdsAsync(Eshop, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    private sealed class FakeVisibility(IReadOnlyCollection<ApplicationId>? visible) : IReadVisibility
    {
        public ValueTask<IReadOnlyCollection<ApplicationId>?> GetVisibleApplicationsAsync(
            CancellationToken cancellationToken) => ValueTask.FromResult(visible);
    }

    private sealed class FakeCatalog(IReadOnlyList<CatalogInstance> instances) : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogInstance>> GetInstancesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(instances);
    }
}
