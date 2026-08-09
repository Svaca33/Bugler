namespace Bugler.Access;

internal sealed class CzechAccessMessages : AccessMessages
{
    public override string SetupAlreadyCompleted => "Prvotní nastavení už proběhlo.";

    public override string ValidEmailRequired => "Je vyžadována platná e-mailová adresa.";

    public override string PasswordRequirement(int minimumLength, int maximumLength) =>
        $"Je vyžadováno heslo o délce {minimumLength} až {maximumLength} znaků.";

    public override string CurrentPasswordIncorrect => "Současné heslo není správné.";

    public override string UnknownLanguage => "Tímto jazykem tento server mluvit neumí.";

    public override string UserWithEmailExists => "Uživatel s tímto e-mailem už existuje.";

    public override string CannotDeactivateOwnAccount =>
        "Správce nemůže deaktivovat svůj vlastní účet.";

    public override string CannotDeleteOwnAccount => "Správce nemůže smazat svůj vlastní účet.";

    public override string SetPublicBaseUrlFirst =>
        "Nastavte Server:PublicBaseUrl, aby Bugler věděl, jak vypadají jeho vlastní odkazy.";

    public override string ReactivateBeforeResetLink =>
        "Před vydáním odkazu pro obnovení hesla účet nejdřív reaktivujte.";

    public override string ServerCannotSendMail =>
        "Tento server neumí odesílat poštu, takže heslo nelze obnovit odkazem.";

    public override string ResetLinkOnItsWay =>
        "Pokud tuto adresu používá nějaký účet, odkaz pro nastavení nového hesla je na cestě.";

    public override string ResetLinkNoLongerWorks =>
        "Tento odkaz už nefunguje. Požádejte o nový a použijte nejnovější mail.";

    public override string MachineDelegationNameRequired =>
        "Pojmenujte tuto delegaci, ať ji později odlišíte od ostatních.";

    public override string MachineDelegationLifetimeRange(int minimumDays, int maximumDays) =>
        $"Delegace platí nejméně {minimumDays} a nejdéle {maximumDays} dní.";

    public override string MachineDelegationApplicationNotReadable =>
        "Delegaci lze zúžit jen na aplikaci, kterou sami smíte číst.";

    public override string ResetMailSubject => "[Bugler] Nastavte si nové heslo";

    public override string ResetMailBody(string link) =>
        string.Join(
            "\n",
            "Někdo požádal o nastavení nového hesla k tomuto účtu Bugleru.",
            "",
            "Nastavte si ho zde — odkaz funguje jednou a za hodinu přestane platit:",
            link,
            "",
            "Pokud jste to nebyli vy, tento mail ignorujte. Nic se nezměnilo a vaše",
            "heslo dál platí.");
}
