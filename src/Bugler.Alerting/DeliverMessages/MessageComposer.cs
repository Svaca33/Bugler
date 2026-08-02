using System.Globalization;
using System.Net;
using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;

namespace Bugler.Alerting.DeliverMessages;

/// <summary>
/// The Alert in every shape it leaves Bugler in. The named fields are the facts; TextBody and
/// HtmlBody are those facts rendered for mail, and the chat sender renders them again into its
/// own card. EpisodeUrl is null when no PublicBaseUrl is configured — messages then carry no
/// links rather than broken ones.
/// </summary>
public sealed record ComposedAlert(
    string Subject,
    /// <summary>The subject without the tag — what a channel that brands itself already shows as a title.</summary>
    string Headline,
    string Place,
    /// <summary>What the Service did, in the words of the Watch that noticed.</summary>
    string Opening,
    /// <summary>What the quoted evidence is called under that Watch — a log, a health check.</summary>
    string EvidenceLabel,
    /// <summary>The opening match's Severity Band, where its Watch has one; null where it does not.</summary>
    string? SeverityLabel,
    string MatchInstant,
    string MatchDetail,
    string? EpisodeUrl,
    string TextBody,
    string HtmlBody);

/// <summary>
/// Turns an Episode into the words that leave Bugler — the Alert only, since ADR 0003 retired
/// the All Clear. Everything comes from the Episode row and the catalog — composition never
/// reads telemetry, so a purged first log changes nothing here.
/// </summary>
public static class MessageComposer
{
    public static ComposedAlert ComposeAlert(
        Episode episode, CatalogService identity, string publicBaseUrl)
    {
        var place = $"{identity.ApplicationName} {identity.Namespace}/{identity.Environment}/{identity.Name}";
        var severity = episode.FirstMatchSeverity is { } band ? SeverityLabel(band) : null;
        var instant = Instant(episode.FirstMatchAt);
        var body = episode.FirstMatchDetail ?? "(no body)";
        var episodeUrl = publicBaseUrl.Length == 0
            ? null
            : $"{publicBaseUrl.TrimEnd('/')}/episodes?episode={episode.Id}";
        var words = Words.For(episode.Watch);

        var headline = $"{words.Subject} {place}";

        return new ComposedAlert(
            Subject: $"[Bugler] {headline}",
            Headline: headline,
            Place: place,
            Opening: words.Opening,
            EvidenceLabel: words.EvidenceLabel,
            SeverityLabel: severity,
            MatchInstant: instant,
            MatchDetail: body,
            EpisodeUrl: episodeUrl,
            TextBody: ComposeText(place, words, severity, instant, body, episodeUrl),
            HtmlBody: ComposeHtml(place, words, severity, instant, body, episodeUrl));
    }

    /// <summary>
    /// The three phrases that differ by Watch. Everything else about an Alert — the place, the
    /// stamp, the evidence, the link — is the same message whichever watch found the trouble.
    /// </summary>
    internal sealed record Words(string Subject, string Opening, string EvidenceLabel)
    {
        public static Words For(Watch watch) => watch switch
        {
            Watch.HealthCheck => new Words(
                "No answer from", "stopped answering its health check.", "Health check"),
            _ => new Words("Trouble in", "started logging trouble.", "First log"),
        };
    }

    private static string ComposeText(
        string place, Words words, string? severity, string instant, string body, string? episodeUrl)
    {
        var lines = new List<string>
        {
            $"{place} {words.Opening}",
            "",
            $"{words.EvidenceLabel} ({Stamp(severity, instant)}):",
            body,
        };

        // One link, and it points at the Episode, not the evidence: the Episode page is where the
        // trouble is acknowledged and solved, quotes the first log itself, and is one click from
        // the logs — a log-filter link would land the reader somewhere with nothing to act on.
        if (episodeUrl is not null)
        {
            lines.Add("");
            lines.Add($"Episode: {episodeUrl}");
        }

        return string.Join("\n", lines);
    }

    // The same message for eyes that render HTML: brass on warm paper, matching the UI's light
    // theme. Inline styles only — mail clients strip everything else.
    private static string ComposeHtml(
        string place, Words words, string? severity, string instant, string body, string? episodeUrl)
    {
        var button = episodeUrl is null
            ? ""
            : $"""
               <p style="margin:20px 0 0;">
               <a href="{WebUtility.HtmlEncode(episodeUrl)}" style="display:inline-block;background:#B26E0E;color:#FFF8EC;padding:10px 20px;border-radius:6px;text-decoration:none;font-size:14px;">Open episode</a>
               </p>
               """;

        return $"""
                <html>
                <body style="margin:0;padding:24px;background:#F4EDDD;">
                <div style="max-width:560px;margin:0 auto;background:#FFFCF4;border:1px solid #E3D5B4;border-radius:8px;padding:24px;font-family:'Segoe UI',Arial,sans-serif;color:#2B2416;">
                <p style="margin:0;font-size:16px;"><strong>{WebUtility.HtmlEncode(place)}</strong> {WebUtility.HtmlEncode(words.Opening)}</p>
                <p style="margin:20px 0 6px;font-size:13px;color:#7A6C4E;">{WebUtility.HtmlEncode(words.EvidenceLabel)} ({WebUtility.HtmlEncode(Stamp(severity, instant))}):</p>
                <pre style="margin:0;padding:12px 14px;background:#F4EDDD;border-radius:6px;font-family:Consolas,Menlo,monospace;font-size:13px;white-space:pre-wrap;word-break:break-word;color:#2B2416;">{WebUtility.HtmlEncode(body)}</pre>{button}
                </div>
                </body>
                </html>
                """;
    }

    /// <summary>How the opening match is stamped: its band and moment, or just the moment where its Watch has no bands.</summary>
    private static string Stamp(string? severity, string instant) =>
        severity is null ? instant : $"{severity}, {instant}";

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
