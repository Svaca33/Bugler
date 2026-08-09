namespace Bugler.Host;

internal sealed class EnglishHostMessages : HostMessages
{
    public override string SmtpHostRequired =>
        "The SMTP server is required — a host name or an IP address.";

    public override string SmtpPortOutOfRange => "The port must be between 1 and 65535.";

    public override string UnknownSecurityMode => "Unknown security mode.";

    public override string FromAddressInvalid => "The From address must be a valid mail address.";

    public override string UnknownLanguage => "That is not a language this server can speak.";

    public override string UnknownAiProvider => "Unknown AI provider.";

    public override string AiModelRequired => "A model name is required.";

    public override string AiBaseUrlRequired =>
        "An OpenAI-compatible endpoint needs a base address.";

    public override string AiBaseUrlInvalid =>
        "The base address must be an absolute http or https URL.";

    public override string AiPatienceOutOfRange =>
        "The patience must be between 0 and 3600 seconds — or empty, to wait as long as it takes.";

    public override string AiNotConfigured => "AI is not configured on this server.";

    public override string TestCompletionFailed => "The provider did not answer the test.";

    public override string SessionCarriesNoAddress => "This session carries no address to send to.";

    public override string TestMailNotSent => "The message could not be sent.";

    public override string TestMailSubject => "Bugler test message";

    public override string TestMailBody =>
        """
        This is a test message from Bugler.

        Somebody signed in as an administrator asked for it, to confirm this server can send mail
        at all. If it reached you, alerts and password-reset links will reach their recipients too.
        """;
}
