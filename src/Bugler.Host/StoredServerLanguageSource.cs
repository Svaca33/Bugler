using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Host;

/// <summary>
/// The server's language as the admin screen saved it, read fresh at every ask so a save applies
/// to the very next answer and the very next mail. While nothing was ever saved, the server
/// speaks English.
/// </summary>
internal sealed class StoredServerLanguageSource(IServiceScopeFactory scopeFactory) : IServerLanguage
{
    public async ValueTask<Language> GetAsync(CancellationToken cancellationToken)
    {
        // A scope per ask: the callers include singletons and background loops, and the DbContext
        // must not be one.
        await using var scope = scopeFactory.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServerDbContext>()
            .ServerLanguage.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return stored is not null && Language.TryParse(stored.Language, out var language)
            ? language
            : Language.English;
    }
}
