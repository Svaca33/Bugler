using Bugler.Ingestion.ObserveReleases;
using Bugler.SharedKernel;

namespace Bugler.Ingestion.Tests;

/// <summary>
/// The rule from ADR 0016, scenario by scenario. Every clock here is explicit — the ledger is
/// handed "now" rather than reading one — so a rolling deploy and a rollback can be told apart in
/// a test the same way they are told apart in production: by nothing but elapsed time.
/// </summary>
public class ReleaseLedgerTests
{
    private static readonly ServiceId Backend = ServiceId.New();
    private static readonly ServiceId Frontend = ServiceId.New();
    private static readonly TimeSpan Straggler = TimeSpan.FromMinutes(30);
    private static readonly DateTime Noon = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

    private static ReleaseLedger Ledger() => new(Straggler);

    private static ServiceVersionSighting Sighting(string version, DateTime at) =>
        new(Backend, version, at);

    /// <summary>Admits a sighting and commits it if it turned out to be worth recording.</summary>
    private static RecordableRelease? Record(ReleaseLedger ledger, string version, DateTime now)
    {
        var release = ledger.Admit(Sighting(version, now), now);
        if (release is not null)
        {
            ledger.Commit(release, now);
        }

        return release;
    }

    [Fact]
    public void First_version_of_a_service_is_recorded_but_is_not_a_release()
    {
        var release = Record(Ledger(), "1.2.3", Noon);

        Assert.NotNull(release);
        Assert.Equal("1.2.3", release.Version);
        // No predecessor: it says what the Service was already running, not that it deployed.
        Assert.Null(release.PreviousVersion);
    }

    [Fact]
    public void Same_version_again_is_nothing()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);

        Assert.Null(Record(ledger, "1.2.3", Noon.AddMinutes(1)));
    }

    [Fact]
    public void A_changed_version_is_a_release_naming_what_it_replaced()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);

        var release = Record(ledger, "1.2.4", Noon.AddHours(2));

        Assert.NotNull(release);
        Assert.Equal("1.2.4", release.Version);
        Assert.Equal("1.2.3", release.PreviousVersion);
    }

    [Fact]
    public void A_release_is_stamped_with_the_senders_clock_not_the_ledgers()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);

        // The Batch that first carried 1.2.4 was stamped a minute before it was weighed.
        var stamped = Noon.AddHours(2);
        var release = ledger.Admit(Sighting("1.2.4", stamped), stamped.AddMinutes(1));

        Assert.Equal(stamped, release?.ObservedAt);
    }

    [Fact]
    public void A_rolling_deploy_yields_one_release_however_long_the_old_version_keeps_arriving()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);
        var deploy = Noon.AddHours(1);

        Assert.NotNull(Record(ledger, "1.2.4", deploy));

        // Replicas restart one by one; both versions arrive interleaved for seven minutes.
        foreach (var minute in new[] { 1, 2, 3, 5, 7 })
        {
            Assert.Null(Record(ledger, "1.2.3", deploy.AddMinutes(minute)));
            Assert.Null(Record(ledger, "1.2.4", deploy.AddMinutes(minute)));
        }
    }

    [Fact]
    public void A_canary_running_beside_the_new_version_for_weeks_stays_one_release()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);
        Assert.NotNull(Record(ledger, "1.2.4", Noon.AddHours(1)));

        // The nine tenths still on 1.2.3 keep arriving, so it never falls quiet and never re-enters.
        for (var minute = 10; minute <= 60 * 24 * 14; minute += 10)
        {
            Assert.Null(Record(ledger, "1.2.3", Noon.AddMinutes(minute)));
        }
    }

    [Fact]
    public void A_service_that_logs_once_a_week_never_announces_itself_again()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);

        // The version regarded as running does not age out, however long the silence.
        Assert.Null(Record(ledger, "1.2.3", Noon.AddDays(7)));
        Assert.Null(Record(ledger, "1.2.3", Noon.AddDays(30)));
    }

    [Fact]
    public void A_rollback_after_the_old_version_fell_quiet_is_a_release()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);
        var deploy = Noon.AddHours(1);
        Record(ledger, "1.2.4", deploy);

        var release = Record(ledger, "1.2.3", deploy + Straggler + TimeSpan.FromMinutes(1));

        Assert.NotNull(release);
        Assert.Equal("1.2.3", release.Version);
        Assert.Equal("1.2.4", release.PreviousVersion);
    }

    [Fact]
    public void A_rollback_while_the_old_version_still_arrives_is_passed_over()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);
        var deploy = Noon.AddHours(1);
        Record(ledger, "1.2.4", deploy);

        // Indistinguishable from a straggler, and silence is preferred to a false marker (ADR 0016).
        Assert.Null(Record(ledger, "1.2.3", deploy.AddMinutes(12)));
    }

    [Fact]
    public void Versions_are_compared_for_equality_alone_so_going_backwards_is_an_ordinary_release()
    {
        var ledger = Ledger();
        Record(ledger, "2.0.0", Noon);

        var release = Record(ledger, "a01dbef8a", Noon.AddDays(1));

        Assert.NotNull(release);
        Assert.Equal("a01dbef8a", release.Version);
        Assert.Equal("2.0.0", release.PreviousVersion);
    }

    [Fact]
    public void Three_versions_in_a_row_absorb_both_of_the_ones_left_behind()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);
        Record(ledger, "1.2.4", Noon.AddMinutes(5));
        Record(ledger, "1.2.5", Noon.AddMinutes(9));

        Assert.Null(Record(ledger, "1.2.3", Noon.AddMinutes(10)));
        Assert.Null(Record(ledger, "1.2.4", Noon.AddMinutes(10)));
    }

    [Fact]
    public void An_uncommitted_release_is_offered_again()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);

        // The write failed, so nothing was committed.
        Assert.NotNull(ledger.Admit(Sighting("1.2.4", Noon.AddHours(1)), Noon.AddHours(1)));

        var retried = ledger.Admit(Sighting("1.2.4", Noon.AddHours(2)), Noon.AddHours(2));

        Assert.NotNull(retried);
        Assert.Equal("1.2.3", retried.PreviousVersion);
    }

    [Fact]
    public void Services_are_weighed_apart()
    {
        var ledger = Ledger();
        Record(ledger, "1.2.3", Noon);

        var release = ledger.Admit(new ServiceVersionSighting(Frontend, "1.2.3", Noon), Noon);

        Assert.NotNull(release);
        Assert.Null(release.PreviousVersion);
    }

    [Fact]
    public void Seeding_restores_what_a_restart_forgot()
    {
        var ledger = Ledger();
        ledger.Seed(Backend, "1.2.3", null, Noon.AddDays(-3), Noon);

        Assert.Null(Record(ledger, "1.2.3", Noon));
    }

    [Fact]
    public void Seeding_keeps_a_rollout_interrupted_by_a_restart_from_doubling_back()
    {
        var ledger = Ledger();
        // Restarted four minutes after 1.2.4 was recorded; 1.2.3 is still coming from live replicas.
        ledger.Seed(Backend, "1.2.4", "1.2.3", Noon.AddMinutes(-4), Noon);

        Assert.Null(Record(ledger, "1.2.3", Noon));
        Assert.Null(Record(ledger, "1.2.4", Noon.AddMinutes(1)));
    }

    [Fact]
    public void Seeding_lets_go_of_a_straggler_older_than_the_window()
    {
        var ledger = Ledger();
        ledger.Seed(Backend, "1.2.4", "1.2.3", Noon - Straggler - TimeSpan.FromMinutes(1), Noon);

        // The rollout was over long before the restart, so 1.2.3 arriving now is a rollback.
        var release = Record(ledger, "1.2.3", Noon);

        Assert.NotNull(release);
        Assert.Equal("1.2.4", release.PreviousVersion);
    }
}
