using System.Globalization;
using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;

namespace Bugler.Alerting.DeliverMessages;

public sealed record ComposedMessage(string Subject, string Text);

/// <summary>
/// Turns an Episode into the words that leave Bugler — the Alert only, since ADR 0003 retired
/// the All Clear. Everything comes from the Episode row and the catalog — composition never
/// reads telemetry, so a purged first log changes nothing here.
/// </summary>
public static class MessageComposer
{
    public static ComposedMessage ComposeAlert(
        Episode episode, CatalogService identity, string publicBaseUrl)
    {
        var place = $"{identity.ApplicationName} {identity.Namespace}/{identity.Environment}/{identity.Name}";
        var lines = new List<string>
        {
            $"{place} started logging trouble.",
            "",
            $"First log ({SeverityLabel(episode.FirstLogSeverity)}, {Instant(episode.FirstLogTimestamp)}):",
            episode.FirstLogBody ?? "(no body)",
        };
        AppendLinks(lines, episode, identity, publicBaseUrl, includeLogLink: true);

        return new ComposedMessage($"[Bugler] Trouble in {place}", string.Join("\n", lines));
    }

    private static void AppendLinks(
        List<string> lines, Episode episode, CatalogService identity, string publicBaseUrl,
        bool includeLogLink)
    {
        if (publicBaseUrl.Length == 0)
        {
            return; // No PublicBaseUrl configured: messages carry no links rather than broken ones.
        }

        var origin = publicBaseUrl.TrimEnd('/');
        // The window starts a little before the opening: the first log's own timestamp predates
        // the detection that opened the Episode, and a link that hides its own subject is worse
        // than a slightly wider one.
        var windowStart = episode.OpenedAt - TimeSpan.FromMinutes(5);
        var filter =
            $"{origin}/?applicationId={identity.ApplicationId.Value}"
            + $"&namespace={Uri.EscapeDataString(identity.Namespace)}"
            + $"&environment={Uri.EscapeDataString(identity.Environment)}"
            + $"&service={Uri.EscapeDataString(identity.Name)}"
            + "&severityMin=13"
            + $"&from={Uri.EscapeDataString(windowStart.UtcDateTime.ToString("o"))}";

        lines.Add("");
        lines.Add($"Logs: {filter}");
        if (includeLogLink)
        {
            lines.Add($"Log record: {filter}&log={episode.FirstLogId}");
        }
    }

    private static string SeverityLabel(short severityNumber) => severityNumber switch
    {
        >= 21 => "FATAL",
        >= 17 => "ERROR",
        >= 13 => "WARN",
        >= 9 => "INFO",
        _ => "DEBUG",
    };

    private static string Instant(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}
