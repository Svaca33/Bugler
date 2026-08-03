using Bugler.SharedKernel;

namespace Bugler.Registry;

/// <summary>
/// Every sentence Registry speaks to a person, one abstract member per sentence: a language is
/// complete when it compiles. The UI shows these verbatim (ADR 0024).
/// </summary>
internal abstract class RegistryMessages
{
    public static readonly RegistryMessages English = new EnglishRegistryMessages();
    public static readonly RegistryMessages Czech = new CzechRegistryMessages();

    public static RegistryMessages For(Language language) =>
        language == Language.Czech ? Czech : English;

    public abstract string ApplicationNameLength { get; }
    public abstract string ApplicationNameExists { get; }
    public abstract string ServiceFacetsLength { get; }
    public abstract string RetentionAtLeastOneDay { get; }
    public abstract string UnknownApplication { get; }
    public abstract string ServiceAlreadyRegistered { get; }
}

internal sealed class EnglishRegistryMessages : RegistryMessages
{
    public override string ApplicationNameLength => "Application name must be 1-200 characters.";

    public override string ApplicationNameExists => "An application with this name already exists.";

    public override string ServiceFacetsLength =>
        "Namespace, environment and name must each be 1-200 characters.";

    public override string RetentionAtLeastOneDay => "Retention must be at least 1 day.";

    public override string UnknownApplication => "Unknown application.";

    public override string ServiceAlreadyRegistered =>
        "This service is already registered for the application.";
}

internal sealed class CzechRegistryMessages : RegistryMessages
{
    public override string ApplicationNameLength => "Název aplikace musí mít 1–200 znaků.";

    public override string ApplicationNameExists => "Aplikace s tímto názvem už existuje.";

    public override string ServiceFacetsLength =>
        "Namespace, prostředí i název musí mít každý 1–200 znaků.";

    public override string RetentionAtLeastOneDay => "Retence musí být alespoň 1 den.";

    public override string UnknownApplication => "Neznámá aplikace.";

    public override string ServiceAlreadyRegistered =>
        "Tato služba už je u aplikace registrovaná.";
}
