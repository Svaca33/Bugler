using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;

namespace Bugler.Alerting.CloseQuietEpisodes;

/// <summary>What happens to one open Episode this run — the whole rule, free of I/O.</summary>
public static class CloseDecision
{
    public static EpisodeCloseReason? Decide(
        Sensitivity sensitivity, DateTimeOffset lastMatchAt, TimeSpan quietWindow, DateTimeOffset now)
    {
        if (sensitivity == Sensitivity.Off)
        {
            // Off means "stop talking about this service now" — not an All Clear, a silence.
            return EpisodeCloseReason.SensitivityOff;
        }

        return now - lastMatchAt >= quietWindow ? EpisodeCloseReason.QuietWindow : null;
    }
}
