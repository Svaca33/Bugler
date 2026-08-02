namespace Bugler.Ingestion.Storage;

/// <summary>
/// One Signal normalized for storage, whatever its kind. Only its Source is asked for here, and
/// only because a Signal PostgreSQL will not hold is worth nothing to whoever reads the log unless
/// it names the Service that sent it.
/// </summary>
internal interface ITelemetryRow
{
    Guid ServiceId { get; }
}
