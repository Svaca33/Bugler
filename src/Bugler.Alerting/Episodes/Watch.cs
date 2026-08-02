using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// Which of Bugler's watches found an Episode's trouble and keeps feeding it (see CONTEXT.md:
/// Watch). A Fingerprint means something different under each, so what tells one kind of trouble
/// from another is the pair, not the Fingerprint alone.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Watch>))]
public enum Watch : short
{
    /// <summary>The Log Records a Service sends; a match is one at or above its Sensitivity.</summary>
    Logs = 1,
}
