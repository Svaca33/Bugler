using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;

namespace Bugler.Alerting;

/// <summary>
/// The three phrases of an Alert that differ by Watch: the headline prefix, what the Service did,
/// and what the quoted evidence is called. Everything else about an Alert is the same message
/// whichever watch found the trouble.
/// </summary>
internal sealed record AlertWords(string Subject, string Opening, string EvidenceLabel);

/// <summary>
/// Every sentence Alerting speaks to a person — refusals the UI shows verbatim (ADR 0024) and
/// the words an Alert leaves in. One abstract member per sentence: a language is complete when
/// it compiles. Query-grammar validation (a hand-crafted URL's mistake) is machine-facing and
/// stays English, off this catalog.
/// </summary>
internal abstract class AlertingMessages
{
    public static readonly AlertingMessages English = new EnglishAlertingMessages();
    public static readonly AlertingMessages Czech = new CzechAlertingMessages();

    public static AlertingMessages For(Language language) =>
        language == Language.Czech ? Czech : English;

    // Acting on Episodes.
    public abstract string ActionBelongsToNewestEpisode { get; }
    public abstract string SolvedEpisodeNeverAcknowledged { get; }
    public abstract string WithdrawingNeverRefuses { get; }
    public abstract string EpisodeAlreadySolved { get; }

    // Alerting settings.
    public abstract string QuietWindowAtLeastOneMinute { get; }
    public abstract string WebhookMustBeHttps { get; }
    public abstract string HealthCheckMustBeUrl { get; }
    public abstract string QuietWindowBetween(int maxMinutes);
    public abstract string EpisodePredatesGrouping { get; }

    // Subscriptions.
    public abstract string ApplicationOutsideVisibility { get; }
    public abstract string UnregisteredService { get; }
    public abstract string ServiceOutsideVisibility { get; }

    // The Alert.
    public abstract AlertWords WordsFor(Watch watch);
    public abstract string NoBody { get; }
    public abstract string EpisodeLinkLabel { get; }
    public abstract string OpenEpisodeButton { get; }

    /// <summary>The visible machine-made mark over the Reading (see CONTEXT.md: Reading).</summary>
    public abstract string ReadingLabel { get; }
}
