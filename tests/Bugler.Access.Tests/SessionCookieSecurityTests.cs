using Bugler.Access.Authentication;
using Microsoft.AspNetCore.Http;

namespace Bugler.Access.Tests;

/// <summary>
/// What a stated public address buys the Session cookie. Bugler reads TLS off this string and
/// nowhere else, so every shape an operator can type into it is worth stating once.
/// </summary>
public class SessionCookieSecurityTests
{
    [Theory]
    [InlineData("https://bugler.example.com")]
    [InlineData("https://bugler.example.com/")]
    [InlineData("https://bugler.example.com:8443")]
    [InlineData("HTTPS://BUGLER.EXAMPLE.COM")]
    [InlineData("  https://bugler.example.com  ")]
    [InlineData("https://localhost:8080")]
    public void An_https_address_is_a_server_with_TLS_in_front(string publicBaseUrl) =>
        Assert.Equal(PublicAddress.Tls, SessionCookieSecurity.Read(publicBaseUrl).Address);

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:3000")]
    public void Plain_http_on_a_loopback_address_is_a_developers_machine(string publicBaseUrl) =>
        Assert.Equal(PublicAddress.Loopback, SessionCookieSecurity.Read(publicBaseUrl).Address);

    [Theory]
    [InlineData("http://bugler.example.com")]
    [InlineData("http://192.168.1.10:8080")]
    public void Plain_http_on_a_name_others_resolve_is_the_one_worth_warning_about(string publicBaseUrl) =>
        Assert.Equal(PublicAddress.PlainHttp, SessionCookieSecurity.Read(publicBaseUrl).Address);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bugler.example.com")]
    [InlineData("not a url")]
    [InlineData("ftp://bugler.example.com")]
    [InlineData("C:\\bugler")]
    public void Anything_that_is_not_an_absolute_http_url_states_nothing(string? publicBaseUrl) =>
        Assert.Equal(PublicAddress.Unstated, SessionCookieSecurity.Read(publicBaseUrl).Address);

    /// <summary>
    /// Silence is not read as danger. A Secure cookie invented from an unstated address would be
    /// dropped by the browser of anyone running Bugler over plain HTTP, and their sign-in would
    /// fail somewhere no explanation can reach them.
    /// </summary>
    [Fact]
    public void An_unstated_address_does_not_ask_for_Secure() =>
        Assert.False(SessionCookieSecurity.Read("").RequiresSecure);

    [Fact]
    public void TLS_locks_the_cookie_to_the_host_and_asks_for_Secure_on_every_request()
    {
        var security = SessionCookieSecurity.Read("https://bugler.example.com");

        Assert.Equal("__Host-bugler.session", security.CookieName);
        Assert.Equal(CookieSecurePolicy.Always, security.SecurePolicy);
    }

    /// <summary>
    /// Without TLS the prefix has to go too: a browser refuses a __Host- cookie that carries no
    /// Secure, so keeping the name would not weaken the Session — it would end sign-in outright.
    /// </summary>
    [Fact]
    public void Without_TLS_the_cookie_keeps_its_plain_name()
    {
        var security = SessionCookieSecurity.Read("http://localhost:8080");

        Assert.Equal("bugler.session", security.CookieName);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, security.SecurePolicy);
    }
}
