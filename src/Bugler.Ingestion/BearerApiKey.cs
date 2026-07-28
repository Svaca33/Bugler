namespace Bugler.Ingestion;

/// <summary>
/// Extracts the Service API key from a standard `Authorization: Bearer` header
/// (set via OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_...").
/// </summary>
internal static class BearerApiKey
{
    private const string Scheme = "Bearer ";

    public static string? Extract(string? authorizationHeader) =>
        authorizationHeader is not null
        && authorizationHeader.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[Scheme.Length..].Trim()
            : null;
}
