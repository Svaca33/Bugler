using System.Text.Json.Serialization;

namespace Bugler.Mail;

/// <summary>
/// How the connection to the SMTP server is protected. Spelled out rather than guessed, because
/// the automatic mode cannot express two honest configurations: "this relay is plaintext on
/// purpose" (an advertised STARTTLS with a broken certificate would still be attempted, and
/// fail), and "refuse to send in the clear" (a downgrade would be accepted silently).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SmtpSecurity>))]
public enum SmtpSecurity
{
    /// <summary>STARTTLS when the server offers it, plaintext when it does not.</summary>
    Automatic,

    /// <summary>Plaintext, even if the server offers STARTTLS.</summary>
    None,

    /// <summary>STARTTLS or nothing: refuse a server that will not upgrade the connection.</summary>
    StartTls,

    /// <summary>TLS from the first byte — the dedicated-port style, usually 465.</summary>
    ImplicitTls,
}
