namespace Bugler.Registry;

public sealed class RegistryOptions
{
    public const string SectionName = "Registry";

    /// <summary>Log Retention applied to Services without an override.</summary>
    public int DefaultRetentionDays { get; set; } = 7;

    /// <summary>
    /// Trace Retention applied to Services without an override. Shorter than the log default on
    /// purpose: a trace is consulted while the trouble is still warm, and a span carries its
    /// events and links, so a day of traces weighs more than a day of logs does.
    /// </summary>
    public int DefaultTraceRetentionDays { get; set; } = 3;
}
