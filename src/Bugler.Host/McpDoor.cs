namespace Bugler.Host;

/// <summary>
/// What stands in front of the machine door: the Admin's switch, asked on every request so that
/// closing it reaches the very next one (ADR 0030). The surface exists whether or not the door is
/// open — the listener is bound at startup — so this is where "this server does not do that" is
/// said, and it is said in machine-facing English like every other answer on this surface
/// (ADR 0024).
/// </summary>
public static class McpDoor
{
    public const string Closed =
        "This Bugler does not open a machine door. An administrator can open it in Bugler's " +
        "settings, under MCP.";

    /// <summary>
    /// Guards every endpoint served on <see cref="Surface.Mcp"/> — the endpoint's own declaration
    /// rather than the port it arrived on, so the switch holds wherever the endpoint is reached
    /// from and a test server without ports is not a way around it. <c>/health</c> declares no
    /// surface (it is probed on all of them) and is therefore untouched, which is right: it answers
    /// for the process, not for the door.
    /// </summary>
    public static IApplicationBuilder UseMcpDoor(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var served = context.GetEndpoint()?.Metadata.GetMetadata<ServedOnMetadata>();
            if (served?.Surface != Surface.Mcp)
            {
                await next(context);
                return;
            }

            var settings = await context.RequestServices.GetRequiredService<McpSettingsSource>()
                .GetCurrentAsync(context.RequestAborted);

            if (settings.Opened)
            {
                await next(context);
                return;
            }

            // 503 rather than 404: whoever reached this port was told to point a tool here, and
            // "not found" would send them looking for a typo in the address instead of at the
            // switch that is off.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(Closed, context.RequestAborted);
        });
}
