using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

/// <summary>The three hands a Journal entry can record (see CONTEXT.md: Journal).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JournalEntryKind>))]
public enum JournalEntryKind : short
{
    /// <summary>Somebody took the kind of trouble on — a take-over reads as one of these after another.</summary>
    Acknowledged = 1,

    /// <summary>An acknowledgement ended by hand — the holder's own, anyone else's, or a Solve consuming it.</summary>
    Withdrawn = 2,

    /// <summary>The terminal verdict landed.</summary>
    Solved = 3,
}
