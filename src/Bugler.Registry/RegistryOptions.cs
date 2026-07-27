namespace Bugler.Registry;

public sealed class RegistryOptions
{
    public const string SectionName = "Registry";

    /// <summary>Retention applied to Instances without an override.</summary>
    public int DefaultRetentionDays { get; set; } = 30;
}
