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
}
