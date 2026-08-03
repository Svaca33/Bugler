namespace Bugler.SharedKernel;

/// <summary>
/// A language Bugler speaks to people — the UI, a mail, a chat card. Text and formats travel
/// together: each language implies its formatting conventions, so there is exactly one axis to
/// choose on. Machine-facing text (logs, /health, OTLP answers) is not spoken in a Language at
/// all and stays English.
/// </summary>
public readonly record struct Language(string Code)
{
    public static readonly Language English = new("en");
    public static readonly Language Czech = new("cs");

    /// <summary>Every language Bugler can speak. English first: it is the language of last resort.</summary>
    public static readonly IReadOnlyList<Language> Supported = [English, Czech];

    /// <summary>
    /// Recognises a supported language code, whole ("cs") or as the primary subtag of a full
    /// BCP 47 tag ("cs-CZ"). Anything else is refused rather than guessed at.
    /// </summary>
    public static bool TryParse(string? code, out Language language)
    {
        var primary = code?.Trim();
        if (primary is not null)
        {
            var dash = primary.IndexOf('-');
            if (dash > 0)
            {
                primary = primary[..dash];
            }

            foreach (var supported in Supported)
            {
                if (string.Equals(primary, supported.Code, StringComparison.OrdinalIgnoreCase))
                {
                    language = supported;
                    return true;
                }
            }
        }

        language = default;
        return false;
    }

    public override string ToString() => Code;
}
