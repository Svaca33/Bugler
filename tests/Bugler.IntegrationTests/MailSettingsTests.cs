using System.Net;
using System.Net.Http.Json;
using Bugler.Mail;
using Microsoft.AspNetCore.Hosting;

namespace Bugler.IntegrationTests;

/// <summary>
/// The stored SMTP settings of ADR 0014: the Mail:Smtp configuration section answers until the
/// first save, the saved row answers from then on — for the admin screen and for the transport
/// alike — and the reset hands the answer back. The password goes in and never comes out.
/// </summary>
public sealed class MailSettingsTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await BuglerHarness.StartAsync(builder =>
        {
            builder.UseSetting("Mail:Smtp:Host", "smtp.config.test");
            builder.UseSetting("Mail:Smtp:Port", "587");
            builder.UseSetting("Mail:Smtp:From", "config@test.local");
        });

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Saved_settings_take_over_from_configuration_and_a_reset_hands_back()
    {
        // Nothing saved yet: the screen and the transport both read the configuration section.
        var initial = await GetSettingsAsync();
        Assert.Equal("Configuration", initial.Source);
        Assert.Equal("smtp.config.test", initial.Host);
        Assert.False(initial.HasPassword);
        Assert.Equal("smtp.config.test", (await CurrentTransportSettingsAsync()).Host);

        // The admin's plain relay: an IP, no encryption, nobody to sign in as.
        var saved = await PutSettingsAsync(new
        {
            host = "172.19.1.236",
            port = 25,
            security = "None",
            username = "",
            password = (string?)null,
            from = "bugler@lan.local",
        });
        Assert.Equal("Stored", saved.Source);
        Assert.False(saved.HasPassword);

        var effective = await CurrentTransportSettingsAsync();
        Assert.Equal("172.19.1.236", effective.Host);
        Assert.Equal(25, effective.Port);
        Assert.Equal(SmtpSecurity.None, effective.Security);
        Assert.True(effective.IsConfigured);

        // A password goes in, is acknowledged only as a flag, and never travels back.
        var response = await _harness.Client.PutAsJsonAsync("/api/admin/mail/settings", new
        {
            host = "172.19.1.236",
            port = 25,
            security = "None",
            username = "relay-user",
            password = "relay-secret",
            from = "bugler@lan.local",
        });
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("relay-secret", raw);
        Assert.True((await GetSettingsAsync()).HasPassword);
        Assert.Equal("relay-secret", (await CurrentTransportSettingsAsync()).Password);

        // Null means "leave the stored password alone" — the form cannot echo what it never had.
        await PutSettingsAsync(new
        {
            host = "172.19.1.236",
            port = 25,
            security = "None",
            username = "relay-user",
            password = (string?)null,
            from = "bugler@lan.local",
        });
        Assert.Equal("relay-secret", (await CurrentTransportSettingsAsync()).Password);

        // An empty string is the explicit removal.
        var cleared = await PutSettingsAsync(new
        {
            host = "172.19.1.236",
            port = 25,
            security = "None",
            username = "",
            password = "",
            from = "bugler@lan.local",
        });
        Assert.False(cleared.HasPassword);

        // The reset deletes the row; the configuration section applies again, whole.
        var reset = await _harness.Client.DeleteAsync("/api/admin/mail/settings");
        reset.EnsureSuccessStatusCode();
        var after = await reset.Content.ReadFromJsonAsync<MailSettingsView>();
        Assert.Equal("Configuration", after!.Source);
        Assert.Equal("smtp.config.test", after.Host);
        Assert.Equal("smtp.config.test", (await CurrentTransportSettingsAsync()).Host);
    }

    [Fact]
    public async Task Nonsense_is_refused_and_nothing_of_it_is_stored()
    {
        Assert.Equal(HttpStatusCode.BadRequest, await SaveAsync(host: "", from: "bugler@lan.local"));
        Assert.Equal(HttpStatusCode.BadRequest, await SaveAsync(host: "relay", from: ""));
        Assert.Equal(HttpStatusCode.BadRequest, await SaveAsync(host: "relay", from: "not-an-address"));
        Assert.Equal(HttpStatusCode.BadRequest, await SaveAsync(host: "relay", from: "bugler@lan.local", port: 0));
        Assert.Equal(HttpStatusCode.BadRequest, await SaveAsync(host: "relay", from: "bugler@lan.local", port: 70000));

        // None of the refusals left a row behind: the configuration still answers.
        Assert.Equal("Configuration", (await GetSettingsAsync()).Source);
    }

    [Fact]
    public async Task Only_an_admin_may_even_look()
    {
        var anonymous = _harness.CreateAnonymousClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/admin/mail/settings")).StatusCode);

        var user = await _harness.CreateUserClientAsync("reader@bugler.test", "ReaderPass123!");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await user.GetAsync("/api/admin/mail/settings")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await user.DeleteAsync("/api/admin/mail/settings")).StatusCode);
    }

    private async Task<MailSettingsView> GetSettingsAsync()
    {
        var settings = await _harness.Client.GetFromJsonAsync<MailSettingsView>("/api/admin/mail/settings");
        return settings!;
    }

    private async Task<MailSettingsView> PutSettingsAsync(object body)
    {
        var response = await _harness.Client.PutAsJsonAsync("/api/admin/mail/settings", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MailSettingsView>())!;
    }

    private async Task<HttpStatusCode> SaveAsync(string host, string from, int port = 25)
    {
        var response = await _harness.Client.PutAsJsonAsync("/api/admin/mail/settings", new
        {
            host,
            port,
            security = "None",
            username = "",
            password = (string?)null,
            from,
        });
        return response.StatusCode;
    }

    /// <summary>What the transport itself would use for the very next send.</summary>
    private async Task<SmtpSettings> CurrentTransportSettingsAsync() =>
        await _harness.GetRequiredService<ISmtpSettingsSource>()
            .GetCurrentAsync(CancellationToken.None);

    private sealed record MailSettingsView(
        string Source,
        string Host,
        int Port,
        string Security,
        string Username,
        bool HasPassword,
        string From);
}
