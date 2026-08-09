using Bugler.Ai;

namespace Bugler.Host;

/// <summary>
/// The one row of AI settings the admin screen edits, on ADR 0014's terms exactly: while the
/// table is empty, the Ai configuration section applies; from the first save the row wins whole —
/// never field by field — until the reset action deletes it again (ADR 0027). The API key is
/// stored as it will be sent and the API never returns it (write-only).
/// </summary>
public sealed class StoredAiSettings
{
    /// <summary>Always 1 — a single-row table, like the SMTP settings.</summary>
    public int Id { get; set; }

    public AiProvider Provider { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";

    /// <summary>See <see cref="AiSettings.PatienceSeconds"/>: 0 = don't wait, null = as long as it takes.</summary>
    public int? PatienceSeconds { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
