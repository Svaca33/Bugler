using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

[JsonConverter(typeof(JsonStringEnumConverter<EpisodeCloseReason>))]
public enum EpisodeCloseReason : short
{
    /// <summary>The Quiet Window passed without a matching Log Record — an All Clear was owed.</summary>
    QuietWindow = 1,

    /// <summary>Sensitivity turned Off while the Episode was open — closed silently, nothing was resolved.</summary>
    SensitivityOff = 2,
}
