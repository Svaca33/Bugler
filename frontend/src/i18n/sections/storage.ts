/**
 * The Admin › Storage ledger: the heading, the rate unit picker, and the table that prices every
 * Service in bytes. Unit abbreviations (B/kB/MB/GB, d) stay untranslated.
 */
export interface StorageMessages {
  title: string;
  subtitle: string;
  /** Accessible name of the ingest rate unit picker. */
  rateUnitAria: string;
  /** The picker's labels, keyed by the RateUnit the arithmetic speaks. */
  rateUnit: {
    second: string;
    minute: string;
    hour: string;
    day: string;
    week: string;
    month: string;
  };
  /** The exact suffix the projected units carry — the column header swaps it for "*". */
  projectedSuffix: string;
  refresh: string;
  measuring: string;
  measuringTables: string;
  reportFailed: string;
  tryAgain: string;
  nothingRegistered: string;
  /** "Logs 1.2 GB · Traces 300 MB · together 1.5 GB on disk" — the sizes arrive formatted. */
  onDisk(logs: string, traces: string, together: string): string;
  /** The muted tail of the on-disk line. */
  onDiskIncluded: string;
  columns: {
    service: string;
    logs: string;
    traces: string;
    total: string;
    kept: string;
    /** "INGEST PER DAY" — the unit arrives uppercased, projected ones ending in "*". */
    ingest(unit: string): string;
    settlesAt: string;
  };
  /** Tooltip on the KEPT column and its cells — what the "7 d / 3 d" pair reads as. */
  retentionPairTitle: string;
  /** Tooltip splitting an ingest cell by kind; the rates arrive formatted. */
  ingestSplit(logs: string, traces: string): string;
  /** Tooltip on the SETTLES AT column header. */
  settlesTitle: string;
  /** Tooltip on a settles cell: the pace and both retention clocks. */
  settledAtPace(logDays: number, traceDays: number): string;
  /** The footnote under the table while a projected unit is chosen. */
  projectedFootnote: string;
}
