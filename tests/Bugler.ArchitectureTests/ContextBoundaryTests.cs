using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Bugler.ArchitectureTests;

/// <summary>
/// Enforces the context map (CONTEXT-MAP.md): contexts touch each other only through
/// Contracts namespaces, nothing depends on Host, and SharedKernel depends on no context.
/// </summary>
public class ContextBoundaryTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Ingestion.IngestionModule).Assembly,
            typeof(Exploration.ExplorationModule).Assembly,
            typeof(Registry.RegistryModule).Assembly,
            typeof(Access.AccessModule).Assembly,
            typeof(SharedKernel.ApplicationId).Assembly,
            typeof(Host.HostMarker).Assembly)
        .Build();

    private static string Ns(string context) => $@"^Bugler\.{context}(\..*)?$";

    private static GivenTypesConjunction TypesIn(string context) =>
        Types().That().ResideInNamespaceMatching(Ns(context));

    private static IObjectProvider<IType> InternalsOf(string context) =>
        Types().That().ResideInNamespaceMatching(Ns(context))
            .And().DoNotResideInNamespaceMatching($@"^Bugler\.{context}\.Contracts(\..*)?$")
            .As($"internals of {context}");

    private static IObjectProvider<IType> AnythingIn(string context) =>
        Types().That().ResideInNamespaceMatching(Ns(context)).As($"types of {context}");

    [Fact]
    public void Ingestion_DependsOnRegistryOnlyThroughContracts_AndOnNoOtherContext() =>
        TypesIn("Ingestion").Should()
            .NotDependOnAny(InternalsOf("Registry"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .Check(Architecture);

    [Fact]
    public void Exploration_DependsOnRegistryAndAccessOnlyThroughContracts_AndNotOnIngestion() =>
        TypesIn("Exploration").Should()
            .NotDependOnAny(InternalsOf("Registry"))
            .AndShould().NotDependOnAny(InternalsOf("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Ingestion"))
            .Check(Architecture);

    [Fact]
    public void Registry_DependsOnNoOtherContext() =>
        TypesIn("Registry").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .Check(Architecture);

    [Fact]
    public void Access_DependsOnNoOtherContext() =>
        TypesIn("Access").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Registry"))
            .Check(Architecture);

    [Fact]
    public void SharedKernel_DependsOnNoContextAndNotOnHost() =>
        TypesIn("SharedKernel").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Registry"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Host"))
            .Check(Architecture);

    [Fact]
    public void NoContext_DependsOnHost() =>
        Types().That().ResideInNamespaceMatching(@"^Bugler(\..*)?$")
            .And().DoNotResideInNamespaceMatching(@"^Bugler\.Host(\..*)?$")
            .Should().NotDependOnAny(AnythingIn("Host"))
            .Check(Architecture);
}
