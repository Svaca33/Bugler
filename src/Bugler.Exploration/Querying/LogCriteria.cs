using Bugler.Exploration.Scoping;
using Npgsql;

namespace Bugler.Exploration.Querying;

/// <summary>
/// Every criterion a Log Record query narrows by, parsed once from the query string. The list, its
/// total and its Volume all build their WHERE clause here on purpose: a Volume whose predicate had
/// drifted from the list's would keep looking right while counting different rows, and nothing in
/// the UI could reveal it.
/// </summary>
internal sealed record LogCriteria(
    SourceFilter Source,
    TimeFilter Time,
    short? SeverityMin,
    string? Search,
    string? TraceId,
    AttributeFilter[] Attributes,
    AttributeFilter[] ResourceAttributes)
{
    /// <summary>Validates the raw query values. On failure <paramref name="error"/> is the 400 body.</summary>
    public static bool TryCreate(
        Guid? applicationId,
        string? serviceNamespace,
        string? environment,
        string? service,
        short? severityMin,
        RelativeRange? range,
        RangeBound? from,
        RangeBound? to,
        string? q,
        string? traceId,
        string[]? attr,
        string[]? res,
        out LogCriteria criteria,
        out string? error)
    {
        criteria = null!;

        if (!AttributeFilters.TryParse(attr, out var attributeFilters) ||
            !AttributeFilters.TryParse(res, out var resourceFilters))
        {
            error = "Invalid attribute filter.";
            return false;
        }

        if (!TimeFilter.TryCreate(range, from, to, out var timeFilter, out error))
        {
            return false;
        }

        criteria = new LogCriteria(
            SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service),
            timeFilter,
            severityMin,
            q,
            traceId,
            attributeFilters,
            resourceFilters);
        return true;
    }

    /// <summary>
    /// Appends every criterion to <paramref name="conditions"/> and binds its values.
    /// <paramref name="serviceIds"/> is the resolved Visibility Scope: null means unrestricted.
    /// </summary>
    public void AddConditions(List<string> conditions, NpgsqlCommand command, Guid[]? serviceIds)
    {
        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        Time.AddConditions(conditions, command, "timestamp");

        if (SeverityMin is > 0)
        {
            conditions.Add("severity_number >= @severityMin");
            command.Parameters.AddWithValue("severityMin", SeverityMin.Value);
        }

        AttributeFilters.AddConditions(conditions, command, Attributes, "attributes", "attr");
        AttributeFilters.AddConditions(conditions, command, ResourceAttributes, "resource_attributes", "res");

        if (!string.IsNullOrEmpty(Search))
        {
            conditions.Add(@"body ILIKE @q ESCAPE '\'");
            command.Parameters.AddWithValue("q", $"%{Sql.EscapeLike(Search)}%");
        }

        if (!string.IsNullOrEmpty(TraceId))
        {
            conditions.Add("trace_id = @traceId");
            command.Parameters.AddWithValue("traceId", TraceId.ToLowerInvariant());
        }
    }

    /// <summary>The WHERE clause for these criteria, empty string when nothing narrows.</summary>
    public string Where(NpgsqlCommand command, Guid[]? serviceIds)
    {
        var conditions = new List<string>();
        AddConditions(conditions, command, serviceIds);
        return conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
    }
}
