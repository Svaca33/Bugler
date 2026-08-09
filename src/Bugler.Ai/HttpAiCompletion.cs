using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Bugler.Ai;

/// <summary>
/// The one completion the transport knows how to make: a single POST — Anthropic's
/// <c>/v1/messages</c> or an OpenAI-compatible <c>/chat/completions</c> — awaited to its answer.
/// No streaming, no tools; a caller who needs structure asks for it in the prompt (ADR 0027).
/// Settings are asked for at every call, so a save on the Server screen applies to the next one.
/// </summary>
public sealed class HttpAiCompletion(
    IHttpClientFactory httpClientFactory,
    IAiSettingsSource settingsSource,
    IOptions<AiOptions> options) : IAiCompletion
{
    public const string ClientName = "Bugler.Ai";

    private const string AnthropicBaseUrl = "https://api.anthropic.com";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<string> CompleteAsync(AiPrompt prompt, CancellationToken cancellationToken)
    {
        var settings = await settingsSource.GetCurrentAsync(cancellationToken);
        if (!settings.IsConfigured)
        {
            throw new AiException("AI is not configured.");
        }

        // The deadline is ours, not HttpClient's: one linked token covers connect and body both.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.CompletionTimeoutSeconds));

        using var request = settings.Provider == AiProvider.Anthropic
            ? AnthropicRequest(settings, prompt)
            : OpenAiCompatibleRequest(settings, prompt);

        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(ClientName)
                .SendAsync(request, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiException(
                $"The provider did not answer within {options.Value.CompletionTimeoutSeconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new AiException($"The provider could not be reached: {exception.Message}", exception);
        }

        using (response)
        {
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiException(
                    $"The provider did not answer within {options.Value.CompletionTimeoutSeconds} seconds.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AiException(
                    $"The provider answered {(int)response.StatusCode}: {ErrorDetail(body)}");
            }

            return settings.Provider == AiProvider.Anthropic
                ? ParseAnthropic(body)
                : ParseOpenAiCompatible(body);
        }
    }

    private HttpRequestMessage AnthropicRequest(AiSettings settings, AiPrompt prompt)
    {
        var baseUrl = settings.BaseUrl.Length > 0 ? settings.BaseUrl.TrimEnd('/') : AnthropicBaseUrl;
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages")
        {
            Content = JsonContent(new
            {
                model = settings.Model,
                maxTokens = options.Value.MaxOutputTokens,
                system = prompt.Instructions,
                messages = new[] { new { role = "user", content = prompt.Input } },
            }),
        };
        request.Headers.Add("x-api-key", settings.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        return request;
    }

    /// <summary>The BaseUrl carries its version segment (Ollama: http://host:11434/v1), as OpenAI clients have it.</summary>
    private HttpRequestMessage OpenAiCompatibleRequest(AiSettings settings, AiPrompt prompt)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent(new
            {
                model = settings.Model,
                // The deprecated spelling, on purpose: local servers know it, and OpenAI still takes it.
                maxTokens = options.Value.MaxOutputTokens,
                messages = new[]
                {
                    new { role = "system", content = prompt.Instructions },
                    new { role = "user", content = prompt.Input },
                },
            }),
        };
        if (settings.ApiKey.Length > 0)
        {
            request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        }

        return request;
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body, SnakeCase), Encoding.UTF8, "application/json");

    private static string ParseAnthropic(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var text = new StringBuilder();
            foreach (var block in document.RootElement.GetProperty("content").EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text")
                {
                    text.Append(block.GetProperty("text").GetString());
                }
            }

            return Answer(text.ToString());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiException("The provider's answer was not in the Messages API shape.", exception);
        }
    }

    private static string ParseOpenAiCompatible(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return Answer(content ?? "");
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiException("The provider's answer was not in the chat completions shape.", exception);
        }
    }

    private static string Answer(string text) =>
        text.Trim().Length > 0 ? text.Trim() : throw new AiException("The provider answered with no text.");

    /// <summary>Both dialects say errors as { "error": { "message": … } }; anything else is quoted raw, shortened.</summary>
    private static string ErrorDetail(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && message.GetString() is { Length: > 0 } detail)
            {
                return detail;
            }
        }
        catch (JsonException)
        {
        }

        var trimmed = body.Trim();
        return trimmed.Length == 0 ? "(empty body)"
            : trimmed.Length <= 300 ? trimmed
            : trimmed[..300];
    }
}
