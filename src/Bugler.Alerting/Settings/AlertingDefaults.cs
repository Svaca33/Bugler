namespace Bugler.Alerting.Settings;

/// <summary>
/// What a fresh Application alerts like before an Admin touches anything: errors are watched from
/// the start (an opt-in default would recreate the blindness alerting exists to end), warnings are
/// a deliberate opt-in. Code constants rather than options — they are part of the model's meaning.
/// </summary>
public static class AlertingDefaults
{
    public const Sensitivity Sensitivity = Bugler.Alerting.Settings.Sensitivity.Errors;
    public const int QuietWindowMinutes = 15;

    /// <summary>
    /// The finest the ladder goes (ADR 0033): by the code that threw. Coarsening is the visible
    /// remedy for a sender whose grouping went wrong, so the default is the answer that separates
    /// most — a merged Episode cannot be un-merged after the fact.
    /// </summary>
    public const FingerprintRule FingerprintRule = Settings.FingerprintRule.ThrowingCode;

    /// <summary>
    /// How long a Machine Claim's lease runs before it wilts unless renewed — long enough for an
    /// agent's overnight run, short enough that a crashed one gives the Episode back within a day.
    /// </summary>
    public const int ClaimLeaseHours = 24;
}
