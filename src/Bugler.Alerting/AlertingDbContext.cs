using Bugler.Alerting.Deliveries;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Alerting.Subscriptions;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bugler.Alerting;

public sealed class AlertingDbContext(DbContextOptions<AlertingDbContext> options)
    : DbContext(options)
{
    // Converter instances rather than lambdas: they apply to nullable and required properties alike.
    private static readonly ValueConverter<ApplicationId, Guid> ApplicationIdConverter =
        new(id => id.Value, value => new ApplicationId(value));

    private static readonly ValueConverter<ServiceId, Guid> ServiceIdConverter =
        new(id => id.Value, value => new ServiceId(value));

    public DbSet<ApplicationAlertingSettings> ApplicationSettings => Set<ApplicationAlertingSettings>();
    public DbSet<ServiceAlertingSettings> ServiceSettings => Set<ServiceAlertingSettings>();
    public DbSet<FingerprintQuietWindow> FingerprintQuietWindows => Set<FingerprintQuietWindow>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<PollCursor> PollCursor => Set<PollCursor>();
    public DbSet<SeenLogId> SeenLogIds => Set<SeenLogId>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("alerting");

        modelBuilder.Entity<ApplicationAlertingSettings>(settings =>
        {
            settings.HasKey(s => s.ApplicationId);
            settings.Property(s => s.ApplicationId).HasConversion(ApplicationIdConverter);
            settings.Property(s => s.ChatWebhookUrl).HasMaxLength(1000);
            settings.ToTable(table => table.HasCheckConstraint(
                "ck_application_settings_quiet_window", "quiet_window_minutes >= 1"));
        });

        modelBuilder.Entity<ServiceAlertingSettings>(settings =>
        {
            settings.HasKey(s => s.ServiceId);
            settings.Property(s => s.ServiceId).HasConversion(ServiceIdConverter);
            settings.Property(s => s.ApplicationId).HasConversion(ApplicationIdConverter);
            settings.HasIndex(s => s.ApplicationId);
            settings.ToTable(table => table.HasCheckConstraint(
                "ck_service_settings_quiet_window", "quiet_window_minutes >= 1"));
        });

        modelBuilder.Entity<FingerprintQuietWindow>(window =>
        {
            // The key is the pair an Episode is told apart by — the same one detection groups on.
            window.HasKey(w => new { w.ServiceId, w.Fingerprint });
            window.Property(w => w.ServiceId).HasConversion(ServiceIdConverter);
            window.Property(w => w.ApplicationId).HasConversion(ApplicationIdConverter);
            window.Property(w => w.Fingerprint).HasMaxLength(300);
            window.HasIndex(w => w.ApplicationId);
            window.ToTable(table => table.HasCheckConstraint(
                "ck_fingerprint_quiet_windows_bounds",
                $"quiet_window_minutes BETWEEN 1 AND {FingerprintQuietWindow.MaxMinutes}"));
        });

        modelBuilder.Entity<Subscription>(subscription =>
        {
            subscription.Property(s => s.ApplicationId).HasConversion(ApplicationIdConverter);
            subscription.Property(s => s.ServiceId).HasConversion(ServiceIdConverter);
            // A Subscription points at exactly one target — an Application or a Service.
            subscription.ToTable(table => table.HasCheckConstraint(
                "ck_subscriptions_one_target", "(application_id IS NULL) <> (service_id IS NULL)"));
            subscription.HasIndex(s => new { s.UserId, s.ApplicationId })
                .IsUnique().HasFilter("application_id IS NOT NULL");
            subscription.HasIndex(s => new { s.UserId, s.ServiceId })
                .IsUnique().HasFilter("service_id IS NOT NULL");
            subscription.HasIndex(s => s.ApplicationId);
            subscription.HasIndex(s => s.ServiceId);
        });

        modelBuilder.Entity<Episode>(episode =>
        {
            episode.Property(e => e.ServiceId).HasConversion(ServiceIdConverter);
            episode.Property(e => e.ApplicationId).HasConversion(ApplicationIdConverter);
            episode.Property(e => e.Fingerprint).HasMaxLength(300);
            episode.Property(e => e.FirstMatchDetail).HasMaxLength(500);
            // The invariant: at most one open Episode per kind of trouble per Service.
            // Also the open-episode scan.
            // Named HasIndex overloads make two distinct indexes over the same columns; the
            // explicit database names keep the snake_case convention from renaming them.
            episode.HasIndex(e => new { e.ServiceId, e.Fingerprint }, "one_open_per_kind")
                .IsUnique()
                .HasFilter("closed_at IS NULL")
                .HasDatabaseName("ix_episodes_one_open_per_kind");
            // The full history of a kind of trouble: recurrence counts and the grouped UI read.
            episode.HasIndex(e => new { e.ServiceId, e.Fingerprint }, "kind_history")
                .HasDatabaseName("ix_episodes_kind_history");
            episode.HasIndex(e => e.ApplicationId);
            episode.HasIndex(e => new { e.OpenedAt, e.Id });
        });

        modelBuilder.Entity<JournalEntry>(entry =>
        {
            // Append-only (ADR 0006): rows are inserted and read, never updated; they die with
            // their Episode. The one query is "this Episode's Journal, oldest first".
            entry.HasOne<Episode>().WithMany()
                .HasForeignKey(e => e.EpisodeId).OnDelete(DeleteBehavior.Cascade);
            entry.HasIndex(e => e.EpisodeId);
        });

        modelBuilder.Entity<Delivery>(delivery =>
        {
            delivery.Property(d => d.LastError).HasMaxLength(2000);
            delivery.HasOne<Episode>().WithMany()
                .HasForeignKey(d => d.EpisodeId).OnDelete(DeleteBehavior.Cascade);
            // Mail goes to a User; chat goes to the Application's webhook, so it names nobody.
            delivery.ToTable(table => table.HasCheckConstraint(
                "ck_deliveries_mail_names_a_user", "(channel = 1) = (user_id IS NOT NULL)"));
            // The sender's only query: pending rows that have come due.
            delivery.HasIndex(d => d.NextAttemptAt)
                .HasFilter("delivered_at IS NULL AND lapsed_at IS NULL");
            // Belt and braces: one Alert and one All Clear per Episode, channel and recipient.
            delivery.HasIndex(d => new { d.EpisodeId, d.Kind, d.Channel, d.UserId })
                .IsUnique().AreNullsDistinct(false);
        });

        modelBuilder.Entity<PollCursor>(cursor =>
        {
            cursor.ToTable("poll_cursor");
            cursor.Property(c => c.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<SeenLogId>(seen =>
        {
            seen.HasKey(s => s.LogId);
            seen.Property(s => s.LogId).ValueGeneratedNever();
        });
    }
}
