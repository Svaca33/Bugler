using Bugler.Alerting.Episodes;

namespace Bugler.Alerting.CloseQuietEpisodes;

/// <summary>What happens to one open Episode this run — the whole rule, free of I/O.</summary>
public static class CloseDecision
{
    /// <summary>
    /// <paramref name="watchOff"/> is the Episode's own Watch turned off — Sensitivity Off for
    /// the Logs Watch, a cleared address for the Health Check Watch. Which question it took to
    /// answer belongs to the caller; the rule is the same either way.
    /// </summary>
    public static EpisodeCloseReason? Decide(
        bool watchOff,
        bool acknowledged,
        DateTimeOffset lastMatchAt,
        TimeSpan quietWindow,
        DateTimeOffset now)
    {
        if (watchOff)
        {
            // Off means "stop talking about this service now" — not an All Clear, a silence.
            // It outranks an acknowledgement: with the watch off there is nothing to hold open.
            return EpisodeCloseReason.WatchOff;
        }

        if (acknowledged)
        {
            // An Acknowledged Episode never Quiets (ADR 0005): somebody is on it, so it stays
            // open absorbing matches — no new Episode, no new Alert — until Solved or withdrawn.
            return null;
        }

        return now - lastMatchAt >= quietWindow ? EpisodeCloseReason.QuietWindow : null;
    }
}
