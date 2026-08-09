namespace Bugler.Exploration.Querying;

/// <summary>
/// The four groups Bugler collapses the OTel severity scale into (CONTEXT.md: Severity Band).
/// Coarser than the OTel levels on purpose — a band is what a colour means — and the one spelling
/// of severity a caller outside the browser is ever offered: FATAL reads as Error, and everything
/// below 9, declared or not, reads as Debug.
/// </summary>
public static class SeverityBand
{
    public const string Error = "Error";
    public const string Warn = "Warn";
    public const string Info = "Info";
    public const string Debug = "Debug";

    public static readonly string[] Names = [Error, Warn, Info, Debug];

    /// <summary>
    /// The lowest OTel severity number a band admits, so "at least Warn" is one comparison rather
    /// than a set. Null for an unknown name, which the caller reports rather than guesses at.
    /// </summary>
    public static short? Floor(string? band) => band?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "error" => 17,
        "warn" or "warning" => 13,
        "info" => 9,
        "debug" => 0,
        _ => null,
    };

    /// <summary>Which band a stored severity number falls in.</summary>
    public static string Of(short severityNumber) => severityNumber switch
    {
        >= 17 => Error,
        >= 13 => Warn,
        >= 9 => Info,
        _ => Debug,
    };
}
