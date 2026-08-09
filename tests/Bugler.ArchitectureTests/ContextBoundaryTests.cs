using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Bugler.ArchitectureTests;

/// <summary>
/// Enforces the context map (CONTEXT-MAP.md): contexts touch each other only through
/// Contracts namespaces, nothing depends on Host, and none of SharedKernel, Mail and Ai — the
/// nodes every context may lean on — depends on a context.
/// </summary>
public class ContextBoundaryTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Ingestion.IngestionModule).Assembly,
            typeof(Exploration.ExplorationModule).Assembly,
            typeof(Registry.RegistryModule).Assembly,
            typeof(Access.AccessModule).Assembly,
            typeof(Alerting.AlertingModule).Assembly,
            typeof(SharedKernel.ApplicationId).Assembly,
            typeof(Mail.MailModule).Assembly,
            typeof(Ai.AiModule).Assembly,
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
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .Check(Architecture);

    [Fact]
    public void Exploration_DependsOnRegistryAndAccessOnlyThroughContracts_AndNotOnIngestion() =>
        TypesIn("Exploration").Should()
            .NotDependOnAny(InternalsOf("Registry"))
            .AndShould().NotDependOnAny(InternalsOf("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .Check(Architecture);

    [Fact]
    public void Alerting_DependsOnRegistryAndAccessOnlyThroughContracts_AndNotOnIngestionOrExploration() =>
        TypesIn("Alerting").Should()
            .NotDependOnAny(InternalsOf("Registry"))
            .AndShould().NotDependOnAny(InternalsOf("Access"))
            // ADR 0010: Alerting reads telemetry via SQL, never through Ingestion's assembly.
            .AndShould().NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .Check(Architecture);

    [Fact]
    public void Registry_DependsOnNoOtherContext() =>
        TypesIn("Registry").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .Check(Architecture);

    [Fact]
    public void Access_DependsOnNoOtherContext() =>
        TypesIn("Access").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Registry"))
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .Check(Architecture);

    /// <summary>
    /// Mail carries messages for whoever composes them and must never learn what they mean —
    /// the moment it knows an Episode or a User, it has become a context of its own (ADR 0011).
    /// </summary>
    [Fact]
    public void Mail_DependsOnNoContextAndNotOnHost() =>
        TypesIn("Mail").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Registry"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .AndShould().NotDependOnAny(AnythingIn("Host"))
            .Check(Architecture);

    /// <summary>
    /// Ai carries prompts for whoever composes them and answers for whoever reads them — the
    /// moment it knows an Episode or an Application, it has become a context (ADR 0027).
    /// </summary>
    [Fact]
    public void Ai_DependsOnNoContextAndNotOnHost() =>
        TypesIn("Ai").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Registry"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .AndShould().NotDependOnAny(AnythingIn("Mail"))
            .AndShould().NotDependOnAny(AnythingIn("Host"))
            .Check(Architecture);

    [Fact]
    public void SharedKernel_DependsOnNoContextAndNotOnHost() =>
        TypesIn("SharedKernel").Should()
            .NotDependOnAny(AnythingIn("Ingestion"))
            .AndShould().NotDependOnAny(AnythingIn("Exploration"))
            .AndShould().NotDependOnAny(AnythingIn("Registry"))
            .AndShould().NotDependOnAny(AnythingIn("Access"))
            .AndShould().NotDependOnAny(AnythingIn("Alerting"))
            .AndShould().NotDependOnAny(AnythingIn("Mail"))
            .AndShould().NotDependOnAny(AnythingIn("Ai"))
            .AndShould().NotDependOnAny(AnythingIn("Host"))
            .Check(Architecture);

    [Fact]
    public void NoContext_DependsOnHost() =>
        Types().That().ResideInNamespaceMatching(@"^Bugler(\..*)?$")
            .And().DoNotResideInNamespaceMatching(@"^Bugler\.Host(\..*)?$")
            .Should().NotDependOnAny(AnythingIn("Host"))
            .Check(Architecture);
}
