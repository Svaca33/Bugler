using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Host;

/// <summary>What the admin screen shows and saves: the language this server speaks by default.</summary>
public sealed record ServerLanguageDto(string Language);

public sealed record SaveServerLanguageRequest(string Language);

/// <summary>
/// The admin screen's half of the server's language: reading and saving the one stored row.
/// Unlike the SMTP settings there is no reset — the server always speaks something, and while
/// nothing was ever saved, that something is English.
/// </summary>
internal static class ServerLanguageEndpoints
{
    public static async Task<IResult> Get(
        IServerLanguage serverLanguage, CancellationToken cancellationToken) =>
        Results.Ok(new ServerLanguageDto((await serverLanguage.GetAsync(cancellationToken)).Code));

    public static async Task<IResult> Save(
        SaveServerLanguageRequest request,
        ServerDbContext dbContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        if (!Language.TryParse(request.Language, out var language))
        {
            var messages = HostMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.Problem(
                title: messages.UnknownLanguage, statusCode: StatusCodes.Status400BadRequest);
        }

        var row = await dbContext.ServerLanguage.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new StoredServerLanguage { Id = 1 };
            dbContext.ServerLanguage.Add(row);
        }

        row.Language = language.Code;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ServerLanguageDto(language.Code));
    }
}
