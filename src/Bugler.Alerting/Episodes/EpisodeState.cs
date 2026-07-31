using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// Where an Episode stands (see CONTEXT.md: Episode) — derived from how it ended, never stored.
/// Open is the only state that takes matches; Solved is the only one a human hand reaches.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EpisodeState>))]
public enum EpisodeState : short
{
    /// <summary>Matching Log Records may still arrive; the stretch is running.</summary>
    Open = 1,

    /// <summary>The Quiet Window passed without a match (see CONTEXT.md: Quieted).</summary>
    Quieted = 2,

    /// <summary>A human ruled the cause fixed (see CONTEXT.md: Solved). Terminal.</summary>
    Solved = 3,

    /// <summary>Sensitivity turned Off while it was open (see CONTEXT.md: Muted).</summary>
    Muted = 4,
}
