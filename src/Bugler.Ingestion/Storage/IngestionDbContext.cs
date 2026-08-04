using Microsoft.EntityFrameworkCore;

namespace Bugler.Ingestion.Storage;

/// <summary>
/// Defines the telemetry schema for EF migrations only. The hot path never touches
/// this context — writes go through binary COPY (<see cref="LogWriter"/>).
/// </summary>
public sealed class IngestionDbContext(DbContextOptions<IngestionDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("telemetry");

        modelBuilder.Entity<StoredLogRecord>(log =>
        {
            log.ToTable("log_records");
            log.Property(l => l.ResourceAttributes).HasColumnType("jsonb");
            log.Property(l => l.Attributes).HasColumnType("jsonb");
            log.Property(l => l.TraceId).HasMaxLength(32);
            log.Property(l => l.SpanId).HasMaxLength(16);
            log.HasIndex(l => new { l.ServiceId, l.Timestamp }).IsDescending(false, true);
            log.HasIndex(l => l.TraceId);
            // Serves the Exploration log list, its total and its Volume (ADR 0012): `WHERE
            // service_id = ANY(@services) AND timestamp … ORDER BY timestamp DESC LIMIT n`.
            // Timestamp leads so the index already stands in the order asked for and the LIMIT
            // stops the scan; the other two columns keep the Source Filter and the severity
            // bands off the heap.
            log.HasIndex(l => new { l.Timestamp, l.ServiceId, l.SeverityNumber })
                .IsDescending(true, false, false);
            // Serves the Alerting detection poll (ADR 0010): `WHERE id > @from AND
            // severity_number >= 17|13 ORDER BY id` — both floors imply this predicate.
            log.HasIndex(l => l.Id).HasFilter("severity_number >= 13")
                .HasDatabaseName("ix_log_records_alerting_poll");
        });

        modelBuilder.Entity<StoredSpan>(span =>
        {
            span.ToTable("spans");
            span.Property(s => s.TraceId).HasMaxLength(32);
            span.Property(s => s.SpanId).HasMaxLength(16);
            span.Property(s => s.ParentSpanId).HasMaxLength(16);
            span.Property(s => s.ResourceAttributes).HasColumnType("jsonb");
            span.Property(s => s.Attributes).HasColumnType("jsonb");
            span.Property(s => s.Events).HasColumnType("jsonb");
            span.Property(s => s.Links).HasColumnType("jsonb");
            span.HasIndex(s => new { s.ServiceId, s.StartTime }).IsDescending(false, true);
            span.HasIndex(s => s.TraceId);
            // Serves the Exploration traces list (ADR 0026): the list draws its page from Root Spans
            // here and then aggregates only those traces through ix_spans_trace_id. The filter keeps
            // this to one entry per trace, `start_time` leads so the LIMIT stops the scan, and the
            // two payload columns answer the Source Filter without touching the heap.
            span.HasIndex(s => s.StartTime)
                .IsDescending(true)
                .HasFilter("parent_span_id IS NULL")
                .IncludeProperties(s => new { s.ServiceId, s.TraceId })
                .HasDatabaseName("ix_spans_roots");
        });

        modelBuilder.Entity<StoredRelease>(release =>
        {
            release.ToTable("releases");
            release.Property(r => r.Version).HasMaxLength(DeclaredVersionLimit);
            release.Property(r => r.PreviousVersion).HasMaxLength(DeclaredVersionLimit);
            // Serves both Exploration reads (ADR 0016): the Releases inside a window, and the
            // Declared Version in effect at its start. Both run on `observed_at` — the sender's
            // clock, the one the Time Filter and the Volume are drawn on. The recorder's own
            // startup read orders by `recorded_at` instead and is left to a scan: it happens once
            // per process over a table that holds one row per Release, not per Signal.
            release.HasIndex(r => new { r.ServiceId, r.ObservedAt }).IsDescending(false, true);
        });
    }

    /// <summary>
    /// The longest Declared Version stored. OTel bounds neither the length nor the format of
    /// `service.version`, and the recorder holds the current one per Service in memory — so a
    /// sender cannot be left free to declare a megabyte of it. Longer is read as no version at all.
    /// </summary>
    public const int DeclaredVersionLimit = 128;

    /// <summary>Schema shape of telemetry.spans; never instantiated at runtime.</summary>
    public sealed class StoredSpan
    {
        public long Id { get; init; }
        public Guid ServiceId { get; init; }
        public required string TraceId { get; init; }
        public required string SpanId { get; init; }
        public string? ParentSpanId { get; init; }
        public required string Name { get; init; }
        public short Kind { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public short StatusCode { get; init; }
        public string? StatusMessage { get; init; }
        public string? ScopeName { get; init; }
        public required string ResourceAttributes { get; init; }
        public required string Attributes { get; init; }
        public required string Events { get; init; }
        public required string Links { get; init; }
    }

    /// <summary>Schema shape of telemetry.log_records; never instantiated at runtime.</summary>
    public sealed class StoredLogRecord
    {
        public long Id { get; init; }
        public Guid ServiceId { get; init; }
        public DateTime Timestamp { get; init; }
        public DateTime? ObservedTimestamp { get; init; }
        public short SeverityNumber { get; init; }
        public string? SeverityText { get; init; }
        public string? Body { get; init; }
        public string? TraceId { get; init; }
        public string? SpanId { get; init; }
        public string? ScopeName { get; init; }
        public required string ResourceAttributes { get; init; }
        public required string Attributes { get; init; }
    }

    /// <summary>Schema shape of telemetry.releases; never instantiated at runtime.</summary>
    public sealed class StoredRelease
    {
        public long Id { get; init; }
        public Guid ServiceId { get; init; }
        public required string Version { get; init; }

        /// <summary>
        /// Null on the row that only establishes what a Service was already running: nothing
        /// preceded it, so it is not a Release (ADR 0016). Every other row is one.
        /// </summary>
        public string? PreviousVersion { get; init; }

        /// <summary>
        /// When the Release happened on the sender's clock — the earliest Signal in the Batch that
        /// first carried the new Declared Version. What the Volume and the Time Filter are drawn on.
        /// </summary>
        public DateTime ObservedAt { get; init; }

        /// <summary>
        /// When Bugler noticed, on the server's clock. Not for display: it is what the order of
        /// Releases is decided by, because a sender's clock can run backwards and a replayed
        /// Export Request can carry an old timestamp, and neither may reorder history.
        /// </summary>
        public DateTime RecordedAt { get; init; }
    }
}
