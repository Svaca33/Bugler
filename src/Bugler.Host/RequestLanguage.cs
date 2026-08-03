using Bugler.SharedKernel;

namespace Bugler.Host;

/// <summary>
/// Resolves the language of the current request's answer: the first supported language named by
/// Accept-Language, the server's own otherwise. The SPA sends its active language as the header's
/// single token, so the fallback is for everyone else — curl, scripts, no context at all.
/// </summary>
internal sealed class RequestLanguage(
    IHttpContextAccessor httpContextAccessor,
    IServerLanguage serverLanguage) : IRequestLanguage
{
    private Language? resolved;

    public async ValueTask<Language> GetAsync(CancellationToken cancellationToken) =>
        resolved ??= FromHeader() ?? await serverLanguage.GetAsync(cancellationToken);

    private Language? FromHeader()
    {
        var header = httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage;
        if (header is not { Count: > 0 } values)
        {
            return null;
        }

        // In written order rather than by q-value: the SPA sends one token, and browsers list
        // by preference anyway. A quality parameter is stripped, never weighed.
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            foreach (var item in value.Split(','))
            {
                var token = item;
                var parameters = token.IndexOf(';');
                if (parameters >= 0)
                {
                    token = token[..parameters];
                }

                if (Language.TryParse(token, out var language))
                {
                    return language;
                }
            }
        }

        return null;
    }
}
