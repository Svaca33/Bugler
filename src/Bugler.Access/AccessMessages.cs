using Bugler.SharedKernel;

namespace Bugler.Access;

/// <summary>
/// Every sentence Access speaks to a person, one abstract member per sentence: a language is
/// complete when it compiles. The sentences stay whole — the UI shows them verbatim (ADR 0024) —
/// and English is the language of last resort.
/// </summary>
internal abstract class AccessMessages
{
    public static readonly AccessMessages English = new EnglishAccessMessages();
    public static readonly AccessMessages Czech = new CzechAccessMessages();

    public static AccessMessages For(Language language) =>
        language == Language.Czech ? Czech : English;

    // Setup and sign-in.
    public abstract string SetupAlreadyCompleted { get; }
    public abstract string ValidEmailRequired { get; }
    public abstract string PasswordRequirement(int minimumLength, int maximumLength);
    public abstract string CurrentPasswordIncorrect { get; }
    public abstract string UnknownLanguage { get; }

    // Managing users.
    public abstract string UserWithEmailExists { get; }
    public abstract string CannotDeactivateOwnAccount { get; }
    public abstract string CannotDeleteOwnAccount { get; }
    public abstract string SetPublicBaseUrlFirst { get; }
    public abstract string ReactivateBeforeResetLink { get; }

    // Password reset.
    public abstract string ServerCannotSendMail { get; }
    public abstract string ResetLinkOnItsWay { get; }
    public abstract string ResetLinkNoLongerWorks { get; }
    public abstract string ResetMailSubject { get; }
    public abstract string ResetMailBody(string link);
}
