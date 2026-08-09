using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bugler.Host;

/// <summary>
/// The one row the MCP admin screen edits, on ADR 0014's terms exactly: while the table is empty
/// the configuration applies; from the first save the row wins whole until the reset action deletes
/// it again (ADR 0030).
/// </summary>
public sealed class StoredMcpSettings
{
    /// <summary>Always 1 — a single-row table, like the SMTP and AI settings.</summary>
    public int Id { get; set; }

    /// <summary>Whether this server opens a machine door at all. Off until an Admin says otherwise.</summary>
    public bool Opened { get; set; }

    /// <summary>
    /// Where the door answers from outside, as only the operator can know (ADR 0019's reasoning,
    /// applied to a second address). Unlike Server:PublicBaseUrl it decides nothing — it is printed
    /// into the connect command shown beside a new Machine Delegation's Secret and read nowhere else.
    /// </summary>
    public string PublicUrl { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>What the configuration says while nothing has been saved.</summary>
public sealed record McpSettings(bool Opened, string PublicUrl);

/// <summary>
/// The MCP settings as they stand right now, read fresh at every ask so closing the door reaches
/// the very next request rather than the next restart.
/// </summary>
public sealed class McpSettingsSource(IServiceScopeFactory scopeFactory, IConfiguration configuration)
{
    public async ValueTask<McpSettings> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServerDbContext>()
            .McpSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return stored is null
            ? FromConfiguration()
            : new McpSettings(stored.Opened, stored.PublicUrl);
    }

    public McpSettings FromConfiguration() => new(
        configuration.GetValue("Mcp:Opened", defaultValue: false),
        configuration["Server:PublicMcpUrl"]?.Trim() ?? "");
}
