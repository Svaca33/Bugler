using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.Ai;

/// <summary>
/// Composition entry point of the AI transport. Not a bounded context: it owns no data, has no
/// lifecycle of its own and never learns what a prompt asks or an answer means — every context
/// composes its own words and reads its own meaning (ADR 0027).
/// </summary>
public static class AiModule
{
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        // The deadline is HttpAiCompletion's own linked token; the client must not cut in first.
        services.AddHttpClient(HttpAiCompletion.ClientName, client =>
            client.Timeout = Timeout.InfiniteTimeSpan);

        // The configuration section is the default source of AI settings. The Host replaces the
        // interface registration with its stored settings (which fall back to this one), so the
        // concrete type stays registered on its own.
        services.AddSingleton<ConfigurationAiSettingsSource>();
        services.AddSingleton<IAiSettingsSource>(p =>
            p.GetRequiredService<ConfigurationAiSettingsSource>());

        services.AddSingleton<IAiCompletion, HttpAiCompletion>();

        return services;
    }
}
