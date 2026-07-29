using System.Globalization;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;

namespace Bugler.Alerting.DeliverMessages;

public sealed record ComposedMessage(string Subject, string Text);

/// <summary>
/// Turns an Episode into the words that leave Bugler. Everything comes from the Episode row and
/// the catalog — composition never reads telemetry, so a purged first log changes nothing here.
/// </summary>
public static class MessageComposer
{
    public static ComposedMessage Compose(
        DeliveryKind kind, Episode episode, CatalogService identity, string publicBaseUrl)
    {
        var place = $"{identity.ApplicationName} {identity.Namespace}/{identity.Environment}/{identity.Name}";
        return kind == DeliveryKind.Alert
            ? ComposeAlert(episode, identity, place, publicBaseUrl)
            : ComposeAllClear(episode, identity, place, publicBaseUrl);
    }

    private static ComposedMessage ComposeAlert(
        Episode episode, CatalogService identity, string place, string publicBaseUrl)
    {
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

    private static ComposedMessage ComposeAllClear(
        Episode episode, CatalogService identity, string place, string publicBaseUrl)
    {
        var closedAt = episode.ClosedAt ?? episode.LastMatchAt;
        var lines = new List<string>
        {
            $"{place} fell quiet.",
            "",
            $"Duration: {Duration(closedAt - episode.OpenedAt)} "
                + $"(opened {Instant(episode.OpenedAt)}, closed {Instant(closedAt)})",
            $"Counted: {Count(episode.ErrorCount, "error")}, {Count(episode.WarnCount, "warning")}.",
        };
        AppendLinks(lines, episode, identity, publicBaseUrl, includeLogLink: false);

        return new ComposedMessage($"[Bugler] All clear in {place}", string.Join("\n", lines));
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

    private static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
        {
            return "under a minute";
        }

        var parts = new List<string>();
        if (duration.Days > 0)
        {
            parts.Add($"{duration.Days} d");
        }

        if (duration.Hours > 0)
        {
            parts.Add($"{duration.Hours} h");
        }

        if (duration.Minutes > 0)
        {
            parts.Add($"{duration.Minutes} min");
        }

        return string.Join(" ", parts);
    }

    private static string Count(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
