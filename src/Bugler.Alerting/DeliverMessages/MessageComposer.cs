using System.Globalization;
using System.Net;
using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.DeliverMessages;

/// <summary>
/// A message in every shape it leaves Bugler in — the Alert, or the Resignation message, which
/// wears the same shapes with the machine's reason for evidence. The named fields are the facts;
/// TextBody and HtmlBody are those facts rendered for mail, and the chat sender renders them
/// again into its own card. EpisodeUrl is null when no PublicBaseUrl is configured — messages
/// then carry no links rather than broken ones.
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
    /// <summary>The Reading in the Alert's own Language, where one was written in time; null otherwise.</summary>
    string? Reading,
    /// <summary>What the Reading is labeled as — the visible machine-made mark (see CONTEXT.md: Reading).</summary>
    string ReadingLabel,
    string? EpisodeUrl,
    string TextBody,
    string HtmlBody,
    /// <summary>The button under the quoted evidence, in the Alert's own Language.</summary>
    string OpenEpisodeLabel,
    /// <summary>The Language the Alert is spoken in — the recipient's for mail, the server's for chat.</summary>
    Language Language);

/// <summary>
/// Turns an Episode into the words that leave Bugler — the Alert, and since the machine hand,
/// the Resignation message; ADR 0003 retired the All Clear. Everything comes from the Episode
/// row and the catalog — composition never reads telemetry, so a purged first log changes
/// nothing here. The Language is the caller's to choose: whose eyes the message is for is a
/// fact about the Delivery, not the Episode.
/// </summary>
public static class MessageComposer
{
    public static ComposedAlert ComposeAlert(
        Episode episode,
        CatalogService identity,
        string publicBaseUrl,
        Language language,
        string? reading = null)
    {
        var messages = AlertingMessages.For(language);
        var place = $"{identity.ApplicationName} {identity.Namespace}/{identity.Environment}/{identity.Name}";
        var severity = episode.FirstMatchSeverity is { } band ? SeverityLabel(band) : null;
        var instant = Instant(episode.FirstMatchAt);
        var body = episode.FirstMatchDetail ?? messages.NoBody;
        var episodeUrl = publicBaseUrl.Length == 0
            ? null
            : $"{publicBaseUrl.TrimEnd('/')}/episodes?episode={episode.Id}";
        var words = messages.WordsFor(episode.Watch);

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
            Reading: reading,
            ReadingLabel: messages.ReadingLabel,
            EpisodeUrl: episodeUrl,
            TextBody: ComposeText(place, words, severity, instant, body, reading, episodeUrl, messages),
            HtmlBody: ComposeHtml(place, words, severity, instant, body, reading, episodeUrl, messages, language),
            OpenEpisodeLabel: messages.OpenEpisodeButton,
            Language: language);
    }

    /// <summary>
    /// The Resignation as a message (see CONTEXT.md: Resignation): the same shapes the Alert
    /// leaves in, with the machine's reason standing where the evidence stands in an Alert —
    /// because the reason is what the reader is being called for.
    /// </summary>
    public static ComposedAlert ComposeResignation(
        Episode episode,
        CatalogService identity,
        string publicBaseUrl,
        Language language)
    {
        var messages = AlertingMessages.For(language);
        var place = $"{identity.ApplicationName} {identity.Namespace}/{identity.Environment}/{identity.Name}";
        var words = messages.ResignationWords;
        var instant = Instant(episode.ResignedAt ?? episode.LastMatchAt);
        var reason = episode.ResignationReason ?? messages.NoBody;
        var episodeUrl = publicBaseUrl.Length == 0
            ? null
            : $"{publicBaseUrl.TrimEnd('/')}/episodes?episode={episode.Id}";

        var headline = $"{words.Subject} {place}";

        return new ComposedAlert(
            Subject: $"[Bugler] {headline}",
            Headline: headline,
            Place: place,
            Opening: words.Opening,
            EvidenceLabel: words.EvidenceLabel,
            SeverityLabel: null,
            MatchInstant: instant,
            MatchDetail: reason,
            Reading: null,
            ReadingLabel: messages.ReadingLabel,
            EpisodeUrl: episodeUrl,
            TextBody: ComposeText(place, words, null, instant, reason, null, episodeUrl, messages),
            HtmlBody: ComposeHtml(place, words, null, instant, reason, null, episodeUrl, messages, language),
            OpenEpisodeLabel: messages.OpenEpisodeButton,
            Language: language);
    }

    /// <summary>
    /// The late Alert a Service falling into a running Episode owes its own followers (ADR 0034).
    /// It names <em>that</em> Service — the Episode may have opened in a deployment its reader
    /// neither runs nor cares about — and says since when, because for this reader nothing opened
    /// just now. The quoted evidence is the Episode's Title: what the trouble is.
    /// </summary>
    public static ComposedAlert ComposeJoined(
        Episode episode, CatalogService joining, string publicBaseUrl, Language language)
    {
        var messages = AlertingMessages.For(language);
        return Compose(
            episode, joining, publicBaseUrl, language, messages,
            messages.JoinedWords(Instant(episode.OpenedAt)),
            instant: Instant(episode.LastMatchAt),
            detail: episode.Title);
    }

    /// <summary>
    /// The one message a Storm sends in place of the Alerts it folded (see CONTEXT.md: Storm):
    /// how many kinds of trouble opened in one Episode Scope, and the newest of them as an
    /// example of what they look like. The Episodes are all there; only the mails were spared.
    /// </summary>
    public static ComposedAlert ComposeStormDigest(
        Episode episode,
        CatalogService identity,
        int episodeCount,
        int windowMinutes,
        string publicBaseUrl,
        Language language)
    {
        var messages = AlertingMessages.For(language);
        return Compose(
            episode, identity, publicBaseUrl, language, messages,
            messages.StormWords(episodeCount, windowMinutes),
            instant: Instant(episode.OpenedAt),
            detail: episode.Title);
    }

    /// <summary>The one shape every message leaves in, once its own words and evidence are chosen.</summary>
    private static ComposedAlert Compose(
        Episode episode,
        CatalogService identity,
        string publicBaseUrl,
        Language language,
        AlertingMessages messages,
        AlertWords words,
        string instant,
        string detail)
    {
        var place = $"{identity.ApplicationName} {identity.Namespace}/{identity.Environment}/{identity.Name}";
        var episodeUrl = publicBaseUrl.Length == 0
            ? null
            : $"{publicBaseUrl.TrimEnd('/')}/episodes?episode={episode.Id}";
        var headline = $"{words.Subject} {place}";

        return new ComposedAlert(
            Subject: $"[Bugler] {headline}",
            Headline: headline,
            Place: place,
            Opening: words.Opening,
            EvidenceLabel: words.EvidenceLabel,
            SeverityLabel: null,
            MatchInstant: instant,
            MatchDetail: detail,
            Reading: null,
            ReadingLabel: messages.ReadingLabel,
            EpisodeUrl: episodeUrl,
            TextBody: ComposeText(place, words, null, instant, detail, null, episodeUrl, messages),
            HtmlBody: ComposeHtml(
                place, words, null, instant, detail, null, episodeUrl, messages, language),
            OpenEpisodeLabel: messages.OpenEpisodeButton,
            Language: language);
    }

    private static string ComposeText(
        string place,
        AlertWords words,
        string? severity,
        string instant,
        string body,
        string? reading,
        string? episodeUrl,
        AlertingMessages messages)
    {
        var lines = new List<string> { $"{place} {words.Opening}" };

        // The Reading stands above the quoted evidence — it is what the alert is opened for —
        // and beside it in authority: labeled machine-made, never part of the facts.
        if (reading is not null)
        {
            lines.Add("");
            lines.Add($"{messages.ReadingLabel}:");
            lines.Add(reading);
        }

        lines.Add("");
        lines.Add($"{words.EvidenceLabel} ({Stamp(severity, instant)}):");
        lines.Add(body);

        // One link, and it points at the Episode, not the evidence: the Episode page is where the
        // trouble is acknowledged and solved, quotes the first log itself, and is one click from
        // the logs — a log-filter link would land the reader somewhere with nothing to act on.
        if (episodeUrl is not null)
        {
            lines.Add("");
            lines.Add($"{messages.EpisodeLinkLabel}: {episodeUrl}");
        }

        return string.Join("\n", lines);
    }

    // The same message for eyes that render HTML: brass on warm paper, matching the UI's light
    // theme. Inline styles only — mail clients strip everything else.
    private static string ComposeHtml(
        string place,
        AlertWords words,
        string? severity,
        string instant,
        string body,
        string? reading,
        string? episodeUrl,
        AlertingMessages messages,
        Language language)
    {
        var readingBlock = reading is null
            ? ""
            : $"""

               <p style="margin:20px 0 6px;font-size:13px;color:#7A6C4E;">{WebUtility.HtmlEncode(messages.ReadingLabel)}:</p>
               <p style="margin:0;padding:12px 14px;background:#FBF3E0;border-left:3px solid #B26E0E;border-radius:6px;font-size:14px;color:#2B2416;">{WebUtility.HtmlEncode(reading)}</p>
               """;

        var button = episodeUrl is null
            ? ""
            : $"""
               <p style="margin:20px 0 0;">
               <a href="{WebUtility.HtmlEncode(episodeUrl)}" style="display:inline-block;background:#B26E0E;color:#FFF8EC;padding:10px 20px;border-radius:6px;text-decoration:none;font-size:14px;">{WebUtility.HtmlEncode(messages.OpenEpisodeButton)}</a>
               </p>
               """;

        return $"""
                <html lang="{language.Code}">
                <body style="margin:0;padding:24px;background:#F4EDDD;">
                <div style="max-width:560px;margin:0 auto;background:#FFFCF4;border:1px solid #E3D5B4;border-radius:8px;padding:24px;font-family:'Segoe UI',Arial,sans-serif;color:#2B2416;">
                <p style="margin:0;font-size:16px;"><strong>{WebUtility.HtmlEncode(place)}</strong> {WebUtility.HtmlEncode(words.Opening)}</p>{readingBlock}
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

    // Severity Band names are the domain's own vocabulary, the same in every Language — a Czech
    // developer reads ERROR, not CHYBA.
    private static string SeverityLabel(short severityNumber) => severityNumber switch
    {
        >= 21 => "FATAL",
        >= 17 => "ERROR",
        >= 13 => "WARN",
        >= 9 => "INFO",
        _ => "DEBUG",
    };

    // Invariant and UTC in every Language: a timestamp in an alert is read across time zones and
    // pasted into searches, so it stays a machine format rather than prose.
    private static string Instant(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}
