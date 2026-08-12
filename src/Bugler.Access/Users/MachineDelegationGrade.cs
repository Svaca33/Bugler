using System.Text.Json.Serialization;

namespace Bugler.Access.Users;

/// <summary>
/// What a Machine Delegation was issued to do, stamped in at issue and never editable — exactly
/// like its Application narrowing (ADR 0029): wanting a different grade means revoking this one
/// and issuing another.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MachineDelegationGrade>))]
public enum MachineDelegationGrade : short
{
    /// <summary>Reads telemetry and Episodes in its User's name and writes nothing — the default.</summary>
    Reading = 1,

    /// <summary>
    /// Reading, and the machine hand: the narrow Alerting verbs by which an agent claims an
    /// Episode, annotates it, proposes Solved, or resigns. Solved itself stays a human verdict.
    /// </summary>
    MachineHand = 2,
}
