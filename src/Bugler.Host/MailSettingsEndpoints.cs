using System.Text.Json.Serialization;
using Bugler.Mail;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Bugler.Host;

/// <summary>Which configuration is answering: the saved row, or the Mail:Smtp section.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MailSettingsSource>))]
public enum MailSettingsSource
{
    Configuration,
    Stored,
}

/// <summary>What the admin screen shows: everything but the password itself.</summary>
public sealed record MailSettingsDto(
    MailSettingsSource Source,
    string Host,
    int Port,
    SmtpSecurity Security,
    string Username,
    bool HasPassword,
    string From);

/// <summary>
/// The password is the one field that cannot round-trip through the screen: null keeps whatever
/// is stored, an empty string clears it, anything else replaces it.
/// </summary>
public sealed record SaveMailSettingsRequest(
    string Host,
    int Port,
    SmtpSecurity Security,
    string? Username,
    string? Password,
    string From);

/// <summary>
/// The admin screen's half of ADR 0014: reading, saving and resetting the stored SMTP settings.
/// Saving makes the row the whole truth for every next send; resetting deletes it and the
/// Mail:Smtp configuration section applies again.
/// </summary>
internal static class MailSettingsEndpoints
{
    public static async Task<IResult> Get(
        ServerDbContext dbContext,
        ConfigurationSmtpSettingsSource fallback,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.SmtpSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return Results.Ok(await ToDtoAsync(stored, fallback, cancellationToken));
    }

    public static async Task<IResult> Save(
        SaveMailSettingsRequest request,
        ServerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var host = request.Host?.Trim() ?? "";
        if (host.Length == 0)
        {
            return Invalid("The SMTP server is required — a host name or an IP address.");
        }

        if (request.Port is < 1 or > 65535)
        {
            return Invalid("The port must be between 1 and 65535.");
        }

        if (!Enum.IsDefined(request.Security))
        {
            return Invalid("Unknown security mode.");
        }

        var from = request.From?.Trim() ?? "";
        if (from.Length == 0 || !from.Contains('@') || !MailboxAddress.TryParse(from, out _))
        {
            // The one field SMTP itself insists on: a message has a sender or it does not exist.
            return Invalid("The From address must be a valid mail address.");
        }

        var row = await dbContext.SmtpSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new StoredSmtpSettings { Id = 1 };
            dbContext.SmtpSettings.Add(row);
        }

        row.Host = host;
        row.Port = request.Port;
        row.Security = request.Security;
        row.Username = request.Username?.Trim() ?? "";
        if (request.Password is not null)
        {
            row.Password = request.Password;
        }

        row.From = from;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(row));
    }

    public static async Task<IResult> Reset(
        ServerDbContext dbContext,
        ConfigurationSmtpSettingsSource fallback,
        CancellationToken cancellationToken)
    {
        await dbContext.SmtpSettings.ExecuteDeleteAsync(cancellationToken);
        return Results.Ok(await ToDtoAsync(stored: null, fallback, cancellationToken));
    }

    private static async Task<MailSettingsDto> ToDtoAsync(
        StoredSmtpSettings? stored,
        ConfigurationSmtpSettingsSource fallback,
        CancellationToken cancellationToken)
    {
        if (stored is not null)
        {
            return ToDto(stored);
        }

        var smtp = await fallback.GetCurrentAsync(cancellationToken);
        return new MailSettingsDto(
            MailSettingsSource.Configuration,
            smtp.Host, smtp.Port, smtp.Security, smtp.Username, smtp.Password.Length > 0, smtp.From);
    }

    private static MailSettingsDto ToDto(StoredSmtpSettings row) =>
        new(MailSettingsSource.Stored,
            row.Host, row.Port, row.Security, row.Username, row.Password.Length > 0, row.From);

    private static IResult Invalid(string message) =>
        Results.Problem(title: message, statusCode: StatusCodes.Status400BadRequest);
}
