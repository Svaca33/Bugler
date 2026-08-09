using Microsoft.Extensions.Options;

namespace Bugler.Ai;

/// <summary>
/// The Ai configuration section as a settings source. On its own it is the whole story; under
/// the Host it is the fallback a stored configuration falls back to (ADR 0027).
/// </summary>
public sealed class ConfigurationAiSettingsSource(IOptions<AiOptions> options) : IAiSettingsSource
{
    public ValueTask<AiSettings> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var ai = options.Value;
        return ValueTask.FromResult(new AiSettings(
            ai.Provider, ai.BaseUrl, ai.ApiKey, ai.Model, ai.PatienceSeconds));
    }
}
