using System.Text.Json.Serialization;
using Bugler.Ai;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Host;

/// <summary>Which configuration is answering: the saved row, or the Ai section.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AiSettingsOrigin>))]
public enum AiSettingsOrigin
{
    Configuration,
    Stored,
}

/// <summary>
/// What the admin screen shows: everything but the API key itself. IsConfigured is the same
/// verdict the transport reaches, said here so the screen need not re-derive it.
/// </summary>
public sealed record AiSettingsDto(
    AiSettingsOrigin Source,
    AiProvider Provider,
    string BaseUrl,
    bool HasApiKey,
    string Model,
    int? PatienceSeconds,
    bool IsConfigured);

/// <summary>
/// The API key is the one field that cannot round-trip through the screen: null keeps whatever
/// is stored, an empty string clears it, anything else replaces it. PatienceSeconds carries the
/// whole three-way choice: 0 = don't wait, a number = that many seconds, null = as long as it takes.
/// </summary>
public sealed record SaveAiSettingsRequest(
    AiProvider Provider,
    string? BaseUrl,
    string? ApiKey,
    string Model,
    int? PatienceSeconds);

/// <summary>
/// The admin screen's half of ADR 0027: reading, saving and resetting the stored AI settings.
/// Saving makes the row the whole truth for every next completion; resetting deletes it and the
/// Ai configuration section applies again.
/// </summary>
internal static class AiSettingsEndpoints
{
    /// <summary>Longer than any model needs, short enough that "forever" stays an explicit choice (null).</summary>
    private const int MaxPatienceSeconds = 3600;

    public static async Task<IResult> Get(
        ServerDbContext dbContext,
        ConfigurationAiSettingsSource fallback,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.AiSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return Results.Ok(await ToDtoAsync(stored, fallback, cancellationToken));
    }

    public static async Task<IResult> Save(
        SaveAiSettingsRequest request,
        ServerDbContext dbContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = HostMessages.For(await requestLanguage.GetAsync(cancellationToken));

        if (!Enum.IsDefined(request.Provider))
        {
            return Invalid(messages.UnknownAiProvider);
        }

        var model = request.Model?.Trim() ?? "";
        if (model.Length == 0)
        {
            return Invalid(messages.AiModelRequired);
        }

        var baseUrl = request.BaseUrl?.Trim() ?? "";
        if (baseUrl.Length == 0 && request.Provider == AiProvider.OpenAiCompatible)
        {
            return Invalid(messages.AiBaseUrlRequired);
        }

        if (baseUrl.Length > 0
            && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return Invalid(messages.AiBaseUrlInvalid);
        }

        if (request.PatienceSeconds is < 0 or > MaxPatienceSeconds)
        {
            return Invalid(messages.AiPatienceOutOfRange);
        }

        var row = await dbContext.AiSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new StoredAiSettings { Id = 1 };
            dbContext.AiSettings.Add(row);
        }

        row.Provider = request.Provider;
        row.BaseUrl = baseUrl;
        if (request.ApiKey is not null)
        {
            row.ApiKey = request.ApiKey;
        }

        row.Model = model;
        row.PatienceSeconds = request.PatienceSeconds;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(row));
    }

    public static async Task<IResult> Reset(
        ServerDbContext dbContext,
        ConfigurationAiSettingsSource fallback,
        CancellationToken cancellationToken)
    {
        await dbContext.AiSettings.ExecuteDeleteAsync(cancellationToken);
        return Results.Ok(await ToDtoAsync(stored: null, fallback, cancellationToken));
    }

    private static async Task<AiSettingsDto> ToDtoAsync(
        StoredAiSettings? stored,
        ConfigurationAiSettingsSource fallback,
        CancellationToken cancellationToken)
    {
        if (stored is not null)
        {
            return ToDto(stored);
        }

        var ai = await fallback.GetCurrentAsync(cancellationToken);
        return new AiSettingsDto(
            AiSettingsOrigin.Configuration,
            ai.Provider, ai.BaseUrl, ai.ApiKey.Length > 0, ai.Model, ai.PatienceSeconds,
            ai.IsConfigured);
    }

    private static AiSettingsDto ToDto(StoredAiSettings row) =>
        new(AiSettingsOrigin.Stored,
            row.Provider, row.BaseUrl, row.ApiKey.Length > 0, row.Model, row.PatienceSeconds,
            new AiSettings(row.Provider, row.BaseUrl, row.ApiKey, row.Model, row.PatienceSeconds)
                .IsConfigured);

    private static IResult Invalid(string message) =>
        Results.Problem(title: message, statusCode: StatusCodes.Status400BadRequest);
}
