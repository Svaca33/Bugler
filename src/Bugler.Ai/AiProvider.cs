using System.Text.Json.Serialization;

namespace Bugler.Ai;

/// <summary>
/// Which wire protocol the configured endpoint speaks. OpenAiCompatible is not about OpenAI: it
/// is the dialect of Ollama, vLLM, LM Studio and most gateways — the door through which a
/// self-hosted operator points Bugler at their own machine (ADR 0027).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AiProvider>))]
public enum AiProvider : short
{
    Anthropic = 1,
    OpenAiCompatible = 2,
}
