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
            log.HasIndex(l => new { l.InstanceId, l.Timestamp }).IsDescending(false, true);
            log.HasIndex(l => l.TraceId);
            log.HasIndex(l => l.Attributes).HasMethod("gin");
        });
    }

    /// <summary>Schema shape of telemetry.log_records; never instantiated at runtime.</summary>
    public sealed class StoredLogRecord
    {
        public long Id { get; init; }
        public Guid InstanceId { get; init; }
        public DateTime Timestamp { get; init; }
        public DateTime? ObservedTimestamp { get; init; }
        public short SeverityNumber { get; init; }
        public string? SeverityText { get; init; }
        public string? Body { get; init; }
        public string? TraceId { get; init; }
        public string? SpanId { get; init; }
        public string? ServiceName { get; init; }
        public string? ScopeName { get; init; }
        public required string ResourceAttributes { get; init; }
        public required string Attributes { get; init; }
    }
}
