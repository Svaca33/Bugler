using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

[JsonConverter(typeof(JsonStringEnumConverter<EpisodeCloseReason>))]
public enum EpisodeCloseReason : short
{
    /// <summary>The Quiet Window passed without a matching Log Record (see CONTEXT.md: Quieted).</summary>
    QuietWindow = 1,

    /// <summary>Sensitivity turned Off while the Episode was open (see CONTEXT.md: Muted).</summary>
    SensitivityOff = 2,

    /// <summary>A human Solved it while it was still open — the verdict ended the stretch by hand.</summary>
    Solved = 3,
}
