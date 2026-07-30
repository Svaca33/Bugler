namespace Bugler.Alerting;

public sealed class AlertingOptions
{
    public const string SectionName = "Alerting";

    /// <summary>
    /// The section holding facts about the server rather than about Alerting. Bound onto these
    /// same options after <see cref="SectionName"/>, because <see cref="PublicBaseUrl"/> is one
    /// of them and Access needs the very same value (ADR 0011).
    /// </summary>
    public const string ServerSectionName = "Server";

    /// <summary>How often the loop polls telemetry, closes quiet Episodes, and sends Deliveries.</summary>
    public int PollIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// How many ids behind the high-water mark each poll re-reads. Identity ids are assigned at
    /// insert but become visible at commit, so a slow transaction can surface below ids the poll
    /// has already passed; the overlap catches them (ADR 0010). 2× the ingest COPY batch, with slack.
    /// </summary>
    public int DetectionOverlapIds { get; set; } = 10_000;

    /// <summary>How long a Delivery keeps retrying before it lapses undelivered — a stale Alert wakes more panic than none.</summary>
    public int DeliveryTimeToLiveHours { get; set; } = 6;

    /// <summary>
    /// Public origin of this Bugler (e.g. https://bugler.example.com), used for links in messages.
    /// Empty means messages carry no links. Comes from <see cref="ServerSectionName"/>, not from
    /// Alerting's own section.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";
}
