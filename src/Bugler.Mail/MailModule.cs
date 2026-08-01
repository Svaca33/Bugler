using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.Mail;

/// <summary>
/// Composition entry point of the mail transport. Not a bounded context: it owns no data, has no
/// lifecycle of its own and knows nothing about what the messages say — every context composes
/// its own words and hands them over (ADR 0011).
/// </summary>
public static class MailModule
{
    public static IServiceCollection AddMail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));

        // The configuration section is the default source of SMTP settings. The Host replaces the
        // interface registration with its stored settings (which fall back to this one), so the
        // concrete type stays registered on its own.
        services.AddSingleton<ConfigurationSmtpSettingsSource>();
        services.AddSingleton<ISmtpSettingsSource>(p =>
            p.GetRequiredService<ConfigurationSmtpSettingsSource>());

        services.AddSingleton<IMailSender, MailKitMailSender>();
        services.AddSingleton<MailQueue>();
        services.AddSingleton<IMailQueue>(p => p.GetRequiredService<MailQueue>());
        services.AddHostedService<QueuedMailSender>();

        return services;
    }
}
