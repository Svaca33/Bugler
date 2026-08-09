namespace Bugler.Host;

/// <summary>
/// The traffic surfaces this server exposes. Each Kestrel listener serves exactly one,
/// so telemetry producers pointed at the OTLP ports can never reach the app
/// (UI + REST API) and vice versa.
/// </summary>
public enum Surface
{
    App,
    OtlpGrpc,
    OtlpHttp,

    /// <summary>
    /// The machine door (ADR 0030): a tool holding a Machine Delegation, speaking MCP over HTTP. Apart from
    /// the app because a Machine Delegation is never a cookie and this traffic is never a browser's — which
    /// is also what keeps it clear of the same-origin check the app surface mounts before routing.
    /// </summary>
    Mcp,
}

/// <summary>Endpoint metadata: the single surface an endpoint is served on.</summary>
public sealed record ServedOnMetadata(Surface Surface);

/// <summary>
/// Binds Kestrel listeners to surfaces by listener name (Kestrel:Endpoints:App|OtlpGrpc|OtlpHttp).
/// Deployment topology is a composition-root concern — modules stay port-agnostic.
/// When the surfaces later move to their own hostnames behind a proxy, replace the
/// port lookup with a host lookup here; nothing else moves.
/// </summary>
public static class ListenerSurfaces
{
    public static IReadOnlyDictionary<int, Surface> FromConfiguration(IConfiguration configuration)
    {
        var surfaceByPort = new Dictionary<int, Surface>();
        foreach (var listener in configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            if (!Enum.TryParse<Surface>(listener.Key, ignoreCase: true, out var surface))
            {
                throw new InvalidOperationException(
                    $"Kestrel endpoint '{listener.Key}' does not name a {nameof(Surface)} — " +
                    $"use one of: {string.Join(", ", Enum.GetNames<Surface>())}.");
            }

            var url = listener["Url"]?.Replace("0.0.0.0", "localhost").Replace("*", "localhost").Replace("+", "localhost");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"Kestrel endpoint '{listener.Key}' has no parsable Url.");
            }

            surfaceByPort[uri.Port] = surface;
        }

        return surfaceByPort;
    }

    public static TBuilder ServedOn<TBuilder>(this TBuilder builder, Surface surface)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new ServedOnMetadata(surface));

    /// <summary>
    /// Rejects (404) any request whose endpoint belongs to a different surface than the
    /// listener it arrived on. Connections on ports outside the map — TestServer, IDE
    /// launch profiles — are unrestricted: every production connection arrives on a
    /// configured listener. The check uses the connection's local port, not the Host
    /// header, so it cannot be spoofed by a client.
    /// </summary>
    public static IApplicationBuilder UseListenerSurfaces(
        this IApplicationBuilder app, IReadOnlyDictionary<int, Surface> surfaceByPort)
        => app.Use((context, next) =>
        {
            var served = context.GetEndpoint()?.Metadata.GetMetadata<ServedOnMetadata>();
            if (served is not null
                && surfaceByPort.TryGetValue(context.Connection.LocalPort, out var surface)
                && surface != served.Surface)
            {
                context.SetEndpoint(null);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return next(context);
        });
}
