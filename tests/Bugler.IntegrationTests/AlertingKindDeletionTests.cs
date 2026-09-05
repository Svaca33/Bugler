using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.ListEpisodes;
using Npgsql;

namespace Bugler.IntegrationTests;

/// <summary>
/// The Deletion of one kind of trouble (Alerting CONTEXT.md: Deletion): every Episode sharing the
/// Episode Scope, Watch and Fingerprint goes in one transaction, with everything that hangs off
/// them — and nothing else. Only an Admin, and only once every Episode of the kind is closed and
/// Archived.
/// </summary>
public sealed class AlertingKindDeletionTests : IAsyncLifetime
{
    private const string Kind = "payment declined";
    private const string OtherKind = "warehouse timed out";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task An_admin_deletes_a_kind_whole_and_its_neighbours_keep_their_answers()
    {
        var (worker, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        var (foreignApp, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");

        // The kind, three times over across two Services, closed and filed — with a Reading, an
        // owed Delivery, its Journal (the Archived entries) and a Quiet Window of its own.
        var first = await SeedClosedEpisodeAsync(Kind, [_harness.ServiceId], withReading: true);
        var second = await SeedClosedEpisodeAsync(Kind, [_harness.ServiceId, worker], withDelivery: true);
        var newest = await SeedClosedEpisodeAsync(Kind, [worker]);
        foreach (var id in new[] { first, second, newest })
        {
            await ArchiveAsync(id);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await _harness.Client.PutAsJsonAsync(
            $"/api/admin/episodes/{newest}/quiet-window", new { quietWindowMinutes = 120 })).StatusCode);

        // The neighbours: another kind in the same Scope with a history of its own, and the same
        // Fingerprint in another Application's Scope.
        await SeedClosedEpisodeAsync(OtherKind, [_harness.ServiceId]);
        var otherFace = await SeedClosedEpisodeAsync(OtherKind, [_harness.ServiceId]);
        var elsewhere = await SeedClosedEpisodeAsync(
            Kind, [foreignService], applicationId: foreignApp);

        Assert.Equal(3, await CountAsync("alerting.journal_entries"));
        Assert.Equal(1, await CountAsync("alerting.readings"));
        Assert.Equal(1, await CountAsync("alerting.deliveries"));
        Assert.Equal(1, await CountAsync("alerting.fingerprint_quiet_windows"));

        // Addressed through any Episode of the kind — history will do as well as the face.
        var deleted = await _harness.Client.DeleteAsync($"/api/admin/episodes/{first}/kind");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // The kind is gone whole, and nothing that hung off it was left behind.
        Assert.Equal(0, await CountAsync(
            "alerting.episodes", $"fingerprint = '{Kind}' AND application_id = '{_harness.ApplicationId}'"));
        Assert.Equal(0, await CountAsync("alerting.journal_entries"));
        Assert.Equal(0, await CountAsync("alerting.readings"));
        Assert.Equal(0, await CountAsync("alerting.deliveries"));
        Assert.Equal(0, await CountAsync("alerting.fingerprint_quiet_windows"));
        Assert.Equal(0, await CountAsync(
            "alerting.participations",
            $"episode_id IN ('{first}', '{second}', '{newest}')"));

        // The neighbours stand, and every answer they give about their own kind is unchanged.
        var everything = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?includeArchived=true");
        Assert.Equal(3, everything!.Items.Count);
        Assert.Contains(everything.Items, e => e.Id == elsewhere);
        Assert.Equal(1, everything.Items.Single(e => e.Id == otherFace).PriorCount);
        var faces = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?latestPerFingerprint=true&includeArchived=true");
        Assert.Equal(
            new[] { otherFace, elsewhere }.OrderBy(id => id),
            faces!.Items.Select(e => e.Id).OrderBy(id => id));

        // Deleting what is already gone is not found, not silently nothing.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _harness.Client.DeleteAsync($"/api/admin/episodes/{first}/kind")).StatusCode);
    }

    [Fact]
    public async Task Deletion_is_an_admins_act_alone()
    {
        var id = await SeedClosedEpisodeAsync(Kind, [_harness.ServiceId]);
        await ArchiveAsync(id);

        // A member who may see the Application, and file it, may still not destroy its record.
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.DeleteAsync($"/api/admin/episodes/{id}/kind")).StatusCode);
        Assert.Equal(1, await CountAsync("alerting.episodes"));
    }

    [Fact]
    public async Task A_kind_with_an_open_episode_is_not_deleted()
    {
        var earlier = await SeedClosedEpisodeAsync(Kind, [_harness.ServiceId]);
        await ArchiveAsync(earlier);
        await SeedOpenEpisodeAsync(Kind);

        // Detection would only reopen the kind on its next Match — and the filed sibling is not
        // taken on its own either: the kind goes whole or not at all.
        var refused = await _harness.Client.DeleteAsync($"/api/admin/episodes/{earlier}/kind");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(
            "\"The kind of trouble still has an open Episode; it is deleted whole or not at all.\"",
            await refused.Content.ReadAsStringAsync());
        Assert.Equal(2, await CountAsync("alerting.episodes"));
        Assert.Equal(1, await CountAsync("alerting.journal_entries"));
    }

    [Fact]
    public async Task A_kind_with_an_episode_not_yet_archived_is_not_deleted()
    {
        var filed = await SeedClosedEpisodeAsync(Kind, [_harness.ServiceId]);
        await ArchiveAsync(filed);
        var unfiled = await SeedClosedEpisodeAsync(Kind, [_harness.ServiceId]);

        // Archiving is the reversible step that must precede the irreversible one, on every
        // Episode of the kind — asked through the filed one or the unfiled one alike.
        foreach (var through in new[] { filed, unfiled })
        {
            var refused = await _harness.Client.DeleteAsync($"/api/admin/episodes/{through}/kind");
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(
                "\"Every Episode of the kind must be archived before the kind is deleted.\"",
                await refused.Content.ReadAsStringAsync());
        }
        Assert.Equal(2, await CountAsync("alerting.episodes"));

        // Filing the last one is what unlocks the door.
        await ArchiveAsync(unfiled);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync($"/api/admin/episodes/{unfiled}/kind")).StatusCode);
        Assert.Equal(0, await CountAsync("alerting.episodes"));
    }

    private async Task ArchiveAsync(Guid id) =>
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/archive", null)).StatusCode);

    /// <summary>The deletion is synchronous, so the count is read once rather than awaited.</summary>
    private async Task<long> CountAsync(string table, string where = "TRUE")
    {
        await using var command = _harness.GetRequiredService<NpgsqlDataSource>()
            .CreateCommand($"SELECT COUNT(*) FROM {table} WHERE {where}");
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private Task<Guid> SeedOpenEpisodeAsync(string kind) =>
        SeedAsync(kind, [_harness.ServiceId], _harness.ApplicationId, closed: false, false, false);

    private Task<Guid> SeedClosedEpisodeAsync(
        string kind, IReadOnlyList<Guid> serviceIds, Guid? applicationId = null,
        bool withReading = false, bool withDelivery = false) =>
        SeedAsync(kind, serviceIds, applicationId ?? _harness.ApplicationId, closed: true,
            withReading, withDelivery);

    /// <summary>
    /// An Episode bound by an Application-wide Scope (ADR 0034) and fed by every named Service.
    /// Ids are version-7 and a second apart, so the newest is never a coin toss.
    /// </summary>
    private async Task<Guid> SeedAsync(
        string kind, IReadOnlyList<Guid> serviceIds, Guid applicationId, bool closed,
        bool withReading, bool withDelivery)
    {
        var id = Guid.CreateVersion7(_idBase.AddSeconds(_seeded++));
        var participations = string.Join(",\n", serviceIds.Select(serviceId =>
            $"(gen_random_uuid(), '{id}', '{serviceId}', NULL, now(), now(), 1, 0)"));
        var reading = withReading
            ? $"""
              INSERT INTO alerting.readings (episode_id, requested_at, attempts, next_attempt_at)
              VALUES ('{id}', now(), 0, now());
              """
            : "";
        var delivery = withDelivery
            ? $"""
              INSERT INTO alerting.deliveries
                  (id, episode_id, kind, channel, user_id, attempts, created_at, next_attempt_at)
              VALUES (gen_random_uuid(), '{id}', 1, 1, '{Guid.NewGuid()}', 0, now(), now());
              """
            : "";
        await _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, opened_by_service_id, application_id, scope_key, watch, fingerprint, title,
                 recipe_version, fingerprint_rung, stack_truncated, alert_folded_into_storm,
                 opened_at, first_match_log_id, first_match_at, first_match_severity,
                 first_match_detail, error_count, warn_count, last_match_at, closed_at, close_reason)
            VALUES
                ('{id}', '{serviceIds[0]}', '{applicationId}',
                 'app={applicationId}|env=prod', 1, '{kind}', '{kind}',
                 1, 2, false, false, now(), 1, now(), 17, '{kind}', 1, 0, now(),
                 {(closed ? "now()" : "NULL")}, {(closed ? "1" : "NULL")});
            INSERT INTO alerting.participations
                (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
            VALUES
                {participations};
            {reading}
            {delivery}
            """);
        return id;
    }

    private readonly DateTimeOffset _idBase = DateTimeOffset.UtcNow;
    private int _seeded;
}
