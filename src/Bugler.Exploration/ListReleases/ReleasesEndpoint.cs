using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.ListReleases;

/// <summary>One Release: the instant a Service began reporting a version it was not already running.</summary>
/// <param name="At">
/// On the sender's clock, like every other instant a Filter or a chart is drawn on — never the
/// server's, or a marker would sit beside Buckets it does not belong to (ADR 0016).
/// </param>
public sealed record ReleaseDto(
    Guid ServiceId,
    string Version,
    string PreviousVersion,
    DateTime At);

/// <summary>What a Service was already running when the window opened.</summary>
public sealed record RunningVersionDto(Guid ServiceId, string Version, DateTime Since);

/// <summary>
/// The Releases of the Services in scope over a window, and what each was running as it opened.
/// <para>
/// Both halves are needed because a Release is a change, and a window that contains none still
/// stands on a version: a Service deploying monthly would otherwise read as running nothing at all
/// for four weeks out of five, and an Episode could not name the version it opened on.
/// </para>
/// </summary>
public sealed record ReleasesResponse(
    DateTime? From,
    DateTime? To,
    DateTime Now,
    IReadOnlyList<RunningVersionDto> Running,
    IReadOnlyList<ReleaseDto> Releases);

/// <summary>
/// Answers the Source Filter and the window, and deliberately nothing else (ADR 0016). A Release
/// happened whoever's Log Records the reader is narrowing to — so a severity threshold, a full-text
/// term or an Attribute Filter leaves it exactly where it was. This is why it does not ride along
/// in the Volume: that answers the whole Filter by definition, and these two must not be confused
/// for one another.
/// </summary>
internal static class ReleasesEndpoint
{
    public static async Task<IResult> Handle(
        Guid? applicationId,
        [FromQuery(Name = "namespace")] string? serviceNamespace,
        string? environment,
        string? service,
        RelativeRange? range,
        RangeBound? from,
        RangeBound? to,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!TimeFilter.TryCreate(range, from, to, out var time, out var error))
        {
            return TypedResults.BadRequest(error);
        }

        var filter = SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service);
        var serviceIds = await scope.ResolveServiceIdsAsync(filter, cancellationToken);

        // The server's clock, the same one the Volume resolves its own window against (ADR 0002),
        // so the two answers describe the same stretch of time rather than two readings of it.
        var now = await VolumeAggregate.ReadServerTimeAsync(dataSource, cancellationToken);

        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new ReleasesResponse(null, null, now.UtcDateTime, [], []));
        }

        // Pinned so the lower bound asked of the database and the one reported back are the same
        // instant; a Relative Range resolved twice would be two.
        var pinned = time.Pin(now);
        var lower = pinned.LowerBound(now);

        var running = lower is { } windowStart
            ? await ReadRunningAsync(serviceIds, windowStart, dataSource, cancellationToken)
            : [];
        var releases = await ReadReleasesAsync(pinned, serviceIds, dataSource, cancellationToken);

        return TypedResults.Ok(new ReleasesResponse(
            lower?.UtcDateTime,
            pinned.UpperBound?.UtcDateTime,
            now.UtcDateTime,
            running,
            releases));
    }

    /// <summary>
    /// The Releases themselves. Rows without a predecessor are passed over: they only establish the
    /// version a Service was already running when Bugler first heard from it, and drawing a marker
    /// there would put a deployment on the day Bugler was installed for every Service at once.
    /// </summary>
    private static async Task<List<ReleaseDto>> ReadReleasesAsync(
        TimeFilter time,
        Guid[]? serviceIds,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = Sql.AggregateTimeoutSeconds;

        var conditions = new List<string> { "previous_version IS NOT NULL" };
        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        time.AddConditions(conditions, command, "observed_at");

        command.CommandText = $"""
            SELECT service_id, version, previous_version, observed_at
            FROM telemetry.releases
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY observed_at, id
            """;

        var releases = new List<ReleaseDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            releases.Add(new ReleaseDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                Sql.EnsureUtc(reader.GetDateTime(3))));
        }

        return releases;
    }

    /// <summary>
    /// The newest row at or before the window's start, per Service — the version each was already
    /// running. Rows without a predecessor count here, unlike above: what a Service was running is
    /// no less true for never having changed.
    /// </summary>
    private static async Task<List<RunningVersionDto>> ReadRunningAsync(
        Guid[]? serviceIds,
        DateTimeOffset windowStart,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = Sql.AggregateTimeoutSeconds;

        var scoped = "";
        if (serviceIds is not null)
        {
            scoped = " AND service_id = ANY(@services)";
            command.Parameters.AddWithValue("services", serviceIds);
        }

        command.Parameters.AddWithValue("start", windowStart);
        command.CommandText = $"""
            SELECT DISTINCT ON (service_id) service_id, version, observed_at
            FROM telemetry.releases
            WHERE observed_at <= @start{scoped}
            ORDER BY service_id, observed_at DESC, id DESC
            """;

        var running = new List<RunningVersionDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            running.Add(new RunningVersionDto(
                reader.GetGuid(0),
                reader.GetString(1),
                Sql.EnsureUtc(reader.GetDateTime(2))));
        }

        return running;
    }
}
