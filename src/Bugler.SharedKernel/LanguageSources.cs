namespace Bugler.SharedKernel;

/// <summary>
/// The language this server speaks when nobody has chosen one: for users who never picked their
/// own, for anonymous requests, and for messages with no person behind them (a chat card).
/// Stored by the Host — a deployment fact, owned by no bounded context — and read here by
/// whoever composes words.
/// </summary>
public interface IServerLanguage
{
    ValueTask<Language> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The language the current request's answer should be spoken in: the requester's own where the
/// Accept-Language header names a supported one, the server's otherwise. The UI shows a server
/// sentence verbatim, so the sentence must arrive in the language the UI is speaking.
/// </summary>
public interface IRequestLanguage
{
    ValueTask<Language> GetAsync(CancellationToken cancellationToken);
}
