namespace Bugler.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Anthropic;

    /// <summary>Empty means the provider's own address; required for an OpenAI-compatible endpoint.</summary>
    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "";

    /// <summary>See <see cref="AiSettings.PatienceSeconds"/>: 0 = don't wait, null = as long as it takes.</summary>
    public int? PatienceSeconds { get; set; } = 60;

    /// <summary>
    /// How long one completion may take before it is abandoned. Generous, because a local model
    /// writing two paragraphs is slow by nature — the callers that cannot wait this long govern
    /// themselves by the patience, not by the deadline.
    /// </summary>
    public int CompletionTimeoutSeconds { get; set; } = 120;

    /// <summary>The ceiling on the answer's length, in the provider's output tokens.</summary>
    public int MaxOutputTokens { get; set; } = 1024;
}
