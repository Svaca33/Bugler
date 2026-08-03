using Bugler.SharedKernel;

namespace Bugler.Host;

/// <summary>
/// Every sentence the Host speaks to a person, one abstract member per sentence: a language is
/// complete when it compiles. English is the language of last resort; adding a language is one
/// new sealed class and one arm in <see cref="For"/>.
/// </summary>
internal abstract class HostMessages
{
    public static readonly HostMessages English = new EnglishHostMessages();
    public static readonly HostMessages Czech = new CzechHostMessages();

    public static HostMessages For(Language language) =>
        language == Language.Czech ? Czech : English;

    // Admin → Server: SMTP settings.
    public abstract string SmtpHostRequired { get; }
    public abstract string SmtpPortOutOfRange { get; }
    public abstract string UnknownSecurityMode { get; }
    public abstract string FromAddressInvalid { get; }

    // Admin → Server: the server's language.
    public abstract string UnknownLanguage { get; }

    // The test mail.
    public abstract string SessionCarriesNoAddress { get; }
    public abstract string TestMailNotSent { get; }
    public abstract string TestMailSubject { get; }
    public abstract string TestMailBody { get; }
}
