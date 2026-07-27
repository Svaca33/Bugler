using System.Text.Json;

namespace Bugler.Exploration.Querying;

internal static class Sql
{
    /// <summary>Npgsql rejects non-UTC DateTimes for timestamptz; query-string values arrive Unspecified.</summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value,
    };

    public static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Escapes LIKE wildcards so user input matches literally (pair with ESCAPE '\').</summary>
    public static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
