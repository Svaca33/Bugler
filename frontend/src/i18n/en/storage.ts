import type { StorageMessages } from "../sections/storage";

export const storage: StorageMessages = {
  title: "Storage",
  subtitle:
    "What each Service's telemetry costs, and how fast it is growing. Bytes marked ≈ are " +
    "estimates; the rate is measured over the last day.",
  rateUnitAria: "Ingest rate unit",
  rateUnit: {
    second: "per second",
    minute: "per minute",
    hour: "per hour",
    day: "per day",
    week: "per week (projected)",
    month: "per month (projected)",
  },
  projectedSuffix: " (projected)",
  refresh: "Refresh",
  measuring: "Measuring…",
  measuringTables: "Measuring the tables — this can take a moment on a loaded instance.",
  reportFailed: "The storage report did not answer. The instance may be busy.",
  tryAgain: "Try again",
  nothingRegistered: "Nothing is registered yet.",
  onDisk: (logs, traces, together) => `Logs ${logs} · Traces ${traces} · together ${together} on disk`,
  onDiskIncluded: "— tables, indexes and TOAST included",
  columns: {
    service: "SERVICE",
    logs: "LOGS",
    traces: "TRACES",
    total: "TOTAL",
    kept: "KEPT",
    ingest: unit => `INGEST ${unit}`,
    settlesAt: "SETTLES AT",
  },
  retentionPairTitle: "Log retention / trace retention",
  ingestSplit: (logs, traces) => `logs ≈ ${logs} · traces ≈ ${traces}`,
  settlesTitle:
    "Where the Footprint comes to rest if the last day's pace holds, each retention clock on its own",
  settledAtPace: (logDays, traceDays) =>
    `at the last day's pace, holding logs ${logDays} d and traces ${traceDays} d`,
  projectedFootnote: "* projected from the last day's measurement — nothing is measured beyond it.",
};
