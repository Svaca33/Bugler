using System.Text.Json;
using Npgsql;

namespace Bugler.Exploration.Querying;

/// <summary>One Attribute Filter criterion: an explicit path into a jsonb column matched against an exact value (ADR 0001).</summary>
public sealed record AttributeFilter(string[] Path, string Value);

internal static class AttributeFilters
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private const int MaxFilters = 32;

    /// <summary>Parses repeated query items, each a JSON object {"path":["a","b"],"value":"x"}.</summary>
    public static bool TryParse(string[]? items, out AttributeFilter[] filters)
    {
        filters = [];
        if (items is null || items.Length == 0)
        {
            return true;
        }

        if (items.Length > MaxFilters)
        {
            return false;
        }

        var parsed = new AttributeFilter[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            AttributeFilter? filter;
            try
            {
                filter = JsonSerializer.Deserialize<AttributeFilter>(items[i], Json);
            }
            catch (JsonException)
            {
                return false;
            }

            if (filter is not { Path.Length: > 0, Value: not null } || filter.Path.Any(string.IsNullOrEmpty))
            {
                return false;
            }

            parsed[i] = filter;
        }

        filters = parsed;
        return true;
    }

    /// <summary>Emits one text-equality condition per filter: `column #>> path = value` (text match, never typed containment).</summary>
    public static void AddConditions(
        List<string> conditions,
        NpgsqlCommand command,
        IReadOnlyList<AttributeFilter> filters,
        string column,
        string parameterPrefix)
    {
        for (var i = 0; i < filters.Count; i++)
        {
            var pathParam = $"{parameterPrefix}Path{i}";
            var valueParam = $"{parameterPrefix}Value{i}";
            conditions.Add($"{column} #>> @{pathParam} = @{valueParam}");
            command.Parameters.AddWithValue(pathParam, filters[i].Path);
            command.Parameters.AddWithValue(valueParam, filters[i].Value);
        }
    }
}
