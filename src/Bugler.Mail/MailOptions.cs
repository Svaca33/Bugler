namespace Bugler.Mail;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary>
    /// How long one send attempt may take before it is abandoned. It has to be short: a request
    /// hands its message to the queue and a stuck send holds up every message behind it.
    /// </summary>
    public int SendTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// How many messages may wait in the hand-off queue. Bugler's queued mail is a trickle
    /// (a password reset here and there), so a full queue means the mail server has stopped
    /// answering — dropping is then the honest answer.
    /// </summary>
    public int QueueCapacity { get; set; } = 256;

    public SmtpOptions Smtp { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public SmtpSecurity Security { get; set; } = SmtpSecurity.Automatic;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
    }
}
