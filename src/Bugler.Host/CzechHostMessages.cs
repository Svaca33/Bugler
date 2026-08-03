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
