using System.Text.Json.Serialization;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// Which rung of the ladder actually produced an Episode's Fingerprint. Stamped on the Episode
/// because what is not understood coarsens <em>visibly</em> (ADR 0033): a Runtime with no recipe,
/// a recipe that found no frames, a Match with no stack — each falls a rung, and a parser written
/// wrong must show up as grouping somebody can see rather than as a plausible hash over nonsense.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FingerprintRung>))]
public enum FingerprintRung : short
{
    /// <summary>Above the Rule: the named attribute the Match carried, whose value was the whole answer.</summary>
    NamedAttribute = 1,

    /// <summary>The code that threw — the frames of the stack trace, with the exception type.</summary>
    Stack = 2,

    /// <summary>The kind of failure — the exception type with what was said, the stack ignored or unreadable.</summary>
    Failure = 3,

    /// <summary>What was said — the template, the event name, or the body with its values blanked.</summary>
    Message = 4,
}
