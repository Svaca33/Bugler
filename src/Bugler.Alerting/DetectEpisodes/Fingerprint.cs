using System.Text.RegularExpressions;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// Distills the kind of trouble a Log Record announces (see CONTEXT.md: Fingerprint). The
/// sender's message template is the truth when it travels along (`{OriginalFormat}`, as .NET
/// loggers send it, or `event.name`); otherwise the body with its variable parts blanked has
/// to do. A heuristic either way — the ADR records the accepted failure modes.
/// </summary>
public static partial class Fingerprint
{
    public const int MaxLength = 300;

    /// <summary>Where every kind of trouble beyond the per-Service cap of open Episodes is folded.</summary>
    public const string Overflow = "(more kinds of trouble)";

    /// <summary>
    /// The one kind of trouble the Health Check Watch knows. Reserved rather than derived: a
    /// Service that answers 503 and one that refuses the connection are the same trouble — it
    /// is not answering — so both feed one Episode instead of flapping between two.
    /// </summary>
    public const string HealthCheckFailing = "(health check failing)";

    public static string Of(string? template, string? eventName, string? body)
    {
        var source = template ?? eventName ?? (body is null ? "(no body)" : Normalize(body));
        source = source.Trim();
        if (source.Length == 0)
        {
            source = "(no body)";
        }

        return source.Length <= MaxLength ? source : source[..MaxLength];
    }

    private static string Normalize(string body)
    {
        var result = UuidPattern().Replace(body, "<id>");
        result = LongHexPattern().Replace(result, "<hex>");
        return DigitsPattern().Replace(result, "<n>");
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex UuidPattern();

    [GeneratedRegex("[0-9a-fA-F]{8,}")]
    private static partial Regex LongHexPattern();

    [GeneratedRegex("[0-9]+")]
    private static partial Regex DigitsPattern();
}
