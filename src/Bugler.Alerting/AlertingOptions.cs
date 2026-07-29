namespace Bugler.Alerting;

public sealed class AlertingOptions
{
    public const string SectionName = "Alerting";

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

    /// <summary>Public origin of this Bugler (e.g. https://bugler.example.com), used for links in messages. Empty means messages carry no links.</summary>
    public string PublicBaseUrl { get; set; } = "";

    public SmtpOptions Smtp { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";

        /// <summary>Unconfigured SMTP disables the mail channel entirely; it never fails startup.</summary>
        public bool IsConfigured => Host.Length > 0 && From.Length > 0;
    }
}
