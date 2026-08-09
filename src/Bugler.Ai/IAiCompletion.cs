namespace Bugler.Ai;

/// <summary>
/// One question to the configured model, awaited to its answer: prompt in, text out. What the
/// prompt asks and what the answer means are the caller's business alone — the transport never
/// learns either (ADR 0027). Throws <see cref="AiException"/> when the provider does not answer,
/// answers badly, or AI is not configured at all; callers who can go on without the answer ask
/// the <see cref="IAiSettingsSource"/> first instead of catching their way out.
/// </summary>
public interface IAiCompletion
{
    Task<string> CompleteAsync(AiPrompt prompt, CancellationToken cancellationToken);
}

/// <summary>Instructions set the model's task; Input is the material the task works on.</summary>
public sealed record AiPrompt(string Instructions, string Input);

/// <summary>The provider did not answer, refused, or AI is unconfigured. The message is safe to store.</summary>
public sealed class AiException(string message, Exception? inner = null) : Exception(message, inner);
