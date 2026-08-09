using System.Text.Json.Serialization;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Host;

/// <summary>Which configuration is answering: the saved row, or the configuration sections.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpSettingsOrigin>))]
public enum McpSettingsOrigin
{
    Configuration,
    Stored,
}

public sealed record McpSettingsDto(McpSettingsOrigin Source, bool Opened, string PublicUrl);

public sealed record SaveMcpSettingsRequest(bool Opened, string? PublicUrl);

/// <summary>
/// What anyone signed in may know about the machine door: whether it is open, and where it answers.
/// Not admin-only, because a person issuing a Machine Delegation needs the address to point their tool at —
/// and it is the same address the Admin typed, not a secret.
/// </summary>
public sealed record McpConnectionDto(bool Opened, string PublicUrl);

/// <summary>
/// The admin screen's half of ADR 0030: reading, saving and resetting the stored MCP settings, on
/// the same terms as the SMTP and AI ones.
/// </summary>
internal static class McpSettingsEndpoints
{
    public static async Task<IResult> Get(
        ServerDbContext dbContext,
        McpSettingsSource source,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.McpSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return Results.Ok(ToDto(stored, source));
    }

    public static async Task<IResult> Save(
        SaveMcpSettingsRequest request,
        ServerDbContext dbContext,
        McpSettingsSource source,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        var messages = HostMessages.For(await requestLanguage.GetAsync(cancellationToken));
        var publicUrl = request.PublicUrl?.Trim() ?? "";

        if (publicUrl.Length > 0
            && (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return Results.Problem(
                title: messages.McpPublicUrlInvalid, statusCode: StatusCodes.Status400BadRequest);
        }

        var row = await dbContext.McpSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new StoredMcpSettings { Id = 1 };
            dbContext.McpSettings.Add(row);
        }

        row.Opened = request.Opened;
        row.PublicUrl = publicUrl.TrimEnd('/');
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(row, source));
    }

    public static async Task<IResult> Reset(
        ServerDbContext dbContext,
        McpSettingsSource source,
        CancellationToken cancellationToken)
    {
        await dbContext.McpSettings.ExecuteDeleteAsync(cancellationToken);
        return Results.Ok(ToDto(stored: null, source));
    }

    public static async Task<IResult> Connection(
        McpSettingsSource source,
        CancellationToken cancellationToken)
    {
        var current = await source.GetCurrentAsync(cancellationToken);
        return Results.Ok(new McpConnectionDto(current.Opened, current.PublicUrl));
    }

    private static McpSettingsDto ToDto(StoredMcpSettings? stored, McpSettingsSource source)
    {
        if (stored is not null)
        {
            return new McpSettingsDto(McpSettingsOrigin.Stored, stored.Opened, stored.PublicUrl);
        }

        var configured = source.FromConfiguration();
        return new McpSettingsDto(
            McpSettingsOrigin.Configuration, configured.Opened, configured.PublicUrl);
    }
}
