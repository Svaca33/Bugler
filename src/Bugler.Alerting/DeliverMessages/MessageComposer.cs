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
        AppendLink(lines, episode, publicBaseUrl);

        return new ComposedMessage($"[Bugler] Trouble in {place}", string.Join("\n", lines));
    }

    // One link, and it points at the Episode, not the evidence: the Episode page is where the
    // trouble is acknowledged and solved, quotes the first log itself, and is one click from the
    // logs — a log-filter link would land the reader somewhere with nothing to act on.
    private static void AppendLink(List<string> lines, Episode episode, string publicBaseUrl)
    {
        if (publicBaseUrl.Length == 0)
        {
            return; // No PublicBaseUrl configured: messages carry no links rather than broken ones.
        }

        lines.Add("");
        lines.Add($"Episode: {publicBaseUrl.TrimEnd('/')}/episodes?episode={episode.Id}");
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
