using Bugler.Alerting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.IntegrationTests;

/// <summary>
/// The one-way upgrade of ADR 0033 and 0034, over rows written before either existed. The old
/// Fingerprint was readable — that is the whole trick — so it becomes the Title, every legacy row
/// is stamped recipe version 0 so nothing ever re-fingerprints it, and the open Logs Episodes are
/// Muted as Regrouped rather than left to quiet out in a partition nothing will report again.
/// </summary>
public sealed class AlertingMigrationTests : IAsyncLifetime
{
    /// <summary>The last migration before an Episode stopped being one Service's.</summary>
    private const string BeforeTheUpgrade = "20260811094238_AddMachineHand";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Legacy_rows_keep_their_name_their_history_and_their_journals()
    {
        var quieted = Guid.CreateVersion7();
        var open = Guid.CreateVersion7();
        var claimed = Guid.CreateVersion7();
        var health = Guid.CreateVersion7();
        var delegation = Guid.NewGuid();
        var claimant = Guid.NewGuid();

        await MigrateToAsync(BeforeTheUpgrade);
        await _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, service_id, application_id, watch, fingerprint, opened_at, first_match_log_id,
                 first_match_at, first_match_severity, first_match_detail, error_count,
                 warn_count, last_match_at, closed_at, close_reason,
                 claimed_by_delegation_id, claimed_by_user_id, claimed_at, claim_lease_until)
            VALUES
                ('{quieted}', '{_harness.ServiceId}', '{_harness.ApplicationId}', 1,
                 'Payment declined', now(), 1, now(), 17, 'boom', 4, 1, now(),
                 now(), 1, NULL, NULL, NULL, NULL),
                ('{open}', '{_harness.ServiceId}', '{_harness.ApplicationId}', 1,
                 'Warehouse timed out', now(), 2, now(), 17, 'boom', 1, 0, now(),
                 NULL, NULL, NULL, NULL, NULL, NULL),
                ('{claimed}', '{_harness.ServiceId}', '{_harness.ApplicationId}', 1,
                 'MongoDb transaction commit error', now(), 3, now(), 17, 'boom', 2, 0, now(),
                 NULL, NULL, '{delegation}', '{claimant}', now(), now() + interval '1 day'),
                ('{health}', '{_harness.ServiceId}', '{_harness.ApplicationId}', 2,
                 '(health check failing)', now(), NULL, now(), NULL, 'HTTP 503', 0, 0, now(),
                 NULL, NULL, NULL, NULL, NULL, NULL);
            INSERT INTO alerting.fingerprint_quiet_windows
                (service_id, fingerprint, application_id, quiet_window_minutes, updated_at)
            VALUES ('{_harness.ServiceId}', 'Warehouse timed out', '{_harness.ApplicationId}',
                    120, now());
            INSERT INTO alerting.deliveries
                (id, episode_id, kind, channel, user_id, attempts, created_at, next_attempt_at)
            VALUES (gen_random_uuid(), '{open}', 1, 1, gen_random_uuid(), 0, now(), now());
            """);

        await MigrateToAsync(null);

        // The old Fingerprint was readable, so it is what a person keeps reading.
        Assert.Equal(4, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE title = fingerprint", 4));
        // Recipe version 0: these belong to a partition that no longer exists.
        Assert.Equal(4, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE recipe_version = 0 "
            + "AND fingerprint_rung = 4", 4));
        // Every legacy row was bound by its own Service, because it was.
        Assert.Equal(4, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.episodes "
            + $"WHERE scope_key = 'service={_harness.ServiceId}'", 4));

        // One synthetic Participation each, carrying the Episode's own tally and times.
        Assert.Equal(4, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.participations WHERE version IS NULL", 4));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.participations "
            + $"WHERE episode_id = '{quieted}' AND error_count = 4 AND warn_count = 1", 1));

        // The open Logs Episodes are Muted as Regrouped; the Health Check one is untouched,
        // because its Fingerprint is reserved and its Scope was always its Service's.
        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE close_reason = 4", 2));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.episodes "
            + $"WHERE id = '{health}' AND closed_at IS NULL", 1));
        // The Quieted one keeps how its stretch ended: it was already over.
        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.episodes WHERE id = '{quieted}' AND close_reason = 1", 1));

        // The machine claim falls with the Episode, and no claim vanishes without its line.
        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.episodes "
            + $"WHERE id = '{claimed}' AND claimed_by_delegation_id IS NULL", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.journal_entries "
            + $"WHERE episode_id = '{claimed}' AND kind = 7 AND delegation_id = '{delegation}'", 1));

        // An Alert about a partition that no longer exists is stale panic; the overrides go too.
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE lapsed_at IS NOT NULL", 1));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.fingerprint_quiet_windows", 0));
    }

    /// <summary>Null means "all the way up" — the state every other test starts from.</summary>
    private async Task MigrateToAsync(string? target)
    {
        await using var scope = _harness.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();
        await dbContext.Database.GetService<IMigrator>().MigrateAsync(target);
    }
}
