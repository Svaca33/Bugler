namespace Bugler.Access.Contracts;

/// <summary>
/// The machine at the door, as the current request presented it: which Machine Delegation, whose
/// User it acts in the name of, and whether its grade extends to the machine hand. Every mark the
/// hand lays is attributed to both — accountability stays with the person who issued the
/// credential.
/// </summary>
public sealed record MachineActor(Guid DelegationId, Guid UserId, bool HoldsMachineHand);

/// <summary>
/// Answers Alerting's question at the machine door: who is this machine, and may it lay a hand?
/// Null when the current caller is not a Machine Delegation at all — a person's Session never
/// becomes a machine by asking.
/// </summary>
public interface IMachineActor
{
    MachineActor? GetCurrent();
}
