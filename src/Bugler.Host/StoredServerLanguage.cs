namespace Bugler.Host;

/// <summary>
/// The one row naming the language this server speaks by default: to users who never chose their
/// own, to anonymous requests, and to messages with no person behind them. While the table is
/// empty, the server speaks English. A deployment fact like the SMTP relay, so the Host owns it
/// (ADR 0014's pattern).
/// </summary>
public sealed class StoredServerLanguage
{
    /// <summary>Always 1 — a single-row table, like the stored SMTP settings.</summary>
    public int Id { get; set; }

    public string Language { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
