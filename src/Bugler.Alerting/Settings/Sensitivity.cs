using System.Text.Json.Serialization;

namespace Bugler.Alerting.Settings;

/// <summary>
/// Which Severity Bands of a Service's Log Records may open or sustain an Episode
/// (see CONTEXT.md: Sensitivity). Detection always reads its current value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Sensitivity>))]
public enum Sensitivity : short
{
    Off = 0,
    Errors = 1,
    ErrorsAndWarnings = 2,
}

public static class SensitivityExtensions
{
    /// <summary>
    /// The lowest OTel severity number this Sensitivity matches — the same band boundaries
    /// Exploration renders (Error ≥ 17, Warn ≥ 13). Null means nothing matches.
    /// </summary>
    public static short? SeverityFloor(this Sensitivity sensitivity) => sensitivity switch
    {
        Sensitivity.Off => null,
        Sensitivity.Errors => 17,
        Sensitivity.ErrorsAndWarnings => 13,
        _ => throw new ArgumentOutOfRangeException(nameof(sensitivity), sensitivity, null),
    };
}
