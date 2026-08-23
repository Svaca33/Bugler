namespace Bugler.Access;

internal sealed class EnglishAccessMessages : AccessMessages
{
    public override string SetupAlreadyCompleted => "Setup has already been completed.";

    public override string ValidEmailRequired => "A valid e-mail address is required.";

    public override string PasswordRequirement(int minimumLength, int maximumLength) =>
        $"A password of {minimumLength} to {maximumLength} characters is required.";

    public override string CurrentPasswordIncorrect => "The current password is not correct.";

    public override string UnknownLanguage => "That is not a language this server can speak.";

    public override string UserWithEmailExists => "A user with this e-mail already exists.";

    public override string CannotDeactivateOwnAccount =>
        "An admin cannot deactivate their own account.";

    public override string CannotDeleteOwnAccount => "An admin cannot delete their own account.";

    public override string SetPublicBaseUrlFirst =>
        "Set Server:PublicBaseUrl so Bugler knows what its own links look like.";

    public override string ReactivateBeforeResetLink =>
        "Reactivate the account before handing out a reset link.";

    public override string ServerCannotSendMail =>
        "This server cannot send mail, so passwords cannot be reset by link.";

    public override string ResetLinkOnItsWay =>
        "If an account uses that address, a link to set a new password is on its way to it.";

    public override string ResetLinkNoLongerWorks =>
        "This link no longer works. Ask for a new one and use the newest mail.";

    public override string MachineDelegationNameRequired =>
        "Give this delegation a name, so you can tell it from the others later.";

    public override string MachineDelegationLifetimeRange(int minimumDays, int maximumDays) =>
        $"A delegation lasts between {minimumDays} and {maximumDays} days.";

    public override string MachineDelegationApplicationNotReadable =>
        "You can only narrow a delegation to an application you may read yourself.";

    public override string FocusApplicationNotReadable =>
        "You can only focus on an application you may read yourself.";

    public override string ResetMailSubject => "[Bugler] Set a new password";

    public override string ResetMailBody(string link) =>
        string.Join(
            "\n",
            "Somebody asked to set a new password for this Bugler account.",
            "",
            "Set it here — the link works once and stops working in an hour:",
            link,
            "",
            "If this wasn't you, ignore this mail. Nothing has changed and your",
            "password still works.");
}
