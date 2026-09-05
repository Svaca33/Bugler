using Bugler.Alerting.Episodes;

namespace Bugler.Alerting;

internal sealed class EnglishAlertingMessages : AlertingMessages
{
    public override string ActionBelongsToNewestEpisode =>
        "The action belongs to the newest Episode of its kind.";

    public override string SolvedEpisodeNeverAcknowledged =>
        "A Solved Episode is never Acknowledged.";

    public override string WithdrawingNeverRefuses => "Withdrawing never refuses.";

    public override string EpisodeAlreadySolved =>
        "The Episode is already Solved; the verdict is rendered once.";

    public override string OnlyAClosedEpisodeIsArchived =>
        "An open Episode is still taking matches and cannot be archived.";

    public override string KindStillHasAnOpenEpisode =>
        "The kind of trouble still has an open Episode; it is deleted whole or not at all.";

    public override string KindNotYetArchivedWhole =>
        "Every Episode of the kind must be archived before the kind is deleted.";

    public override string QuietWindowAtLeastOneMinute =>
        "The quiet window must be at least 1 minute.";

    public override string ClaimLeaseAtLeastOneHour =>
        "The machine claim lease must be at least 1 hour.";

    public override string WebhookMustBeHttps => "The webhook must be an absolute https URL.";

    public override string HealthCheckMustBeUrl =>
        "The health check must be an absolute http or https URL.";

    public override string QuietWindowBetween(int maxMinutes) =>
        $"The quiet window must be between 1 and {maxMinutes} minutes.";

    public override string AttributeKeyTooLong(int maxLength) =>
        $"The attribute name can hold at most {maxLength} characters.";

    public override string EpisodePredatesGrouping =>
        "This episode predates grouping by kind of trouble and cannot carry a quiet window.";

    public override string ApplicationOutsideVisibility =>
        "An application outside your visibility cannot be subscribed to.";

    public override string UnregisteredService =>
        "An unregistered service cannot be subscribed to.";

    public override string ServiceOutsideVisibility =>
        "A service outside your visibility cannot be subscribed to.";

    public override AlertWords WordsFor(Watch watch) => watch switch
    {
        Watch.HealthCheck => new AlertWords(
            "No answer from", "stopped answering its health check.", "Health check"),
        _ => new AlertWords("Trouble in", "started logging trouble.", "First log"),
    };

    public override AlertWords ResignationWords => new(
        "Machine resigned in",
        "carries trouble a machine resigned: it looked and cannot fix this from the code. "
        + "A human hand is needed.",
        "The machine's reason");

    public override AlertWords JoinedWords(string runningSince) => new(
        "Trouble reached",
        $"has fallen into trouble that has been running since {runningSince}.",
        "The kind of trouble");

    public override AlertWords StormWords(int episodeCount, int windowMinutes) => new(
        "Storm in",
        $"opened {episodeCount} kinds of trouble in the last {windowMinutes} minutes. "
        + "Their alerts were folded into this one message; every episode is there to be seen. "
        + "A storm is a sender's grouping gone wrong as often as it is a real outage.",
        "The newest of them");

    public override string NoBody => "(no body)";

    public override string EpisodeLinkLabel => "Episode";

    public override string OpenEpisodeButton => "Open episode";

    public override string ReadingLabel => "AI reading";
}
