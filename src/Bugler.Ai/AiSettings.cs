namespace Bugler.Ai;

/// <summary>
/// The AI settings in force for one completion — a snapshot taken from the
/// <see cref="IAiSettingsSource"/> at the moment of asking, never a live view. An empty
/// <see cref="BaseUrl"/> means the provider's own address (Anthropic's API); an empty
/// <see cref="ApiKey"/> means the endpoint is not authenticated against, as a LAN Ollama is not.
/// </summary>
public sealed record AiSettings(
    AiProvider Provider,
    string BaseUrl,
    string ApiKey,
    string Model,
    /// <summary>
    /// How long a caller who could go on without the answer should hold its door: 0 means do not
    /// wait, null means as long as it takes. Advice about the endpoint's speed — an API across
    /// the ocean or a slow model on the operator's own metal — not a promise the transport keeps.
    /// </summary>
    int? PatienceSeconds)
{
    /// <summary>Unconfigured AI disables it entirely; it never fails startup (ADR 0027).</summary>
    public bool IsConfigured => Model.Length > 0 && Provider switch
    {
        AiProvider.Anthropic => ApiKey.Length > 0,
        AiProvider.OpenAiCompatible => BaseUrl.Length > 0,
        _ => false,
    };
}
