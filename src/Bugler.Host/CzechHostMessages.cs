namespace Bugler.Host;

internal sealed class CzechHostMessages : HostMessages
{
    public override string SmtpHostRequired =>
        "Server SMTP je povinný — název hostitele, nebo IP adresa.";

    public override string SmtpPortOutOfRange => "Port musí být mezi 1 a 65535.";

    public override string UnknownSecurityMode => "Neznámý režim zabezpečení.";

    public override string FromAddressInvalid =>
        "Adresa odesílatele musí být platná e-mailová adresa.";

    public override string UnknownLanguage => "Tímto jazykem tento server mluvit neumí.";

    public override string UnknownAiProvider => "Neznámý AI provider.";

    public override string AiModelRequired => "Název modelu je povinný.";

    public override string AiBaseUrlRequired =>
        "OpenAI-kompatibilní endpoint potřebuje základní adresu.";

    public override string AiBaseUrlInvalid =>
        "Základní adresa musí být absolutní http nebo https URL.";

    public override string McpPublicUrlInvalid =>
        "Adresa, na které odpovídají strojové dveře, musí být absolutní http nebo https URL.";

    public override string AiPatienceOutOfRange =>
        "Trpělivost musí být mezi 0 a 3600 sekundami — nebo prázdná, aby se čekalo, jak dlouho bude třeba.";

    public override string AiNotConfigured => "AI na tomto serveru není nastavena.";

    public override string TestCompletionFailed => "Provider na zkoušku neodpověděl.";

    public override string SessionCarriesNoAddress =>
        "Tato relace nenese žádnou adresu, na kterou by šlo psát.";

    public override string TestMailNotSent => "Zprávu se nepodařilo odeslat.";

    public override string TestMailSubject => "Zkušební zpráva Bugleru";

    public override string TestMailBody =>
        """
        Toto je zkušební zpráva z Bugleru.

        Požádal o ni někdo přihlášený jako správce, aby ověřil, že tento server vůbec umí odesílat
        poštu. Pokud dorazila, dorazí příjemcům i výstrahy a odkazy pro obnovení hesla.
        """;
}
