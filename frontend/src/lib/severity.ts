/** OTel severity numbers: 1-4 TRACE, 5-8 DEBUG, 9-12 INFO, 13-16 WARN, 17-20 ERROR, 21-24 FATAL. */
export function severityLabel(severityNumber: number): string {
  if (severityNumber >= 21) return "FATAL";
  if (severityNumber >= 17) return "ERROR";
  if (severityNumber >= 13) return "WARN";
  if (severityNumber >= 9) return "INFO";
  if (severityNumber >= 5) return "DEBUG";
  if (severityNumber >= 1) return "TRACE";
  return "UNSET";
}

/**
 * The four Severity Bands the OTel scale collapses into wherever Bugler shows severity. Coarser
 * than the six levels on purpose: a band is what a colour means, so one Log Record is one colour
 * in the row, in its rail and in the Volume chart above the list.
 */
export type SeverityBand = "error" | "warn" | "info" | "debug";

/** FATAL rides with ERROR; TRACE and UNSET ride with DEBUG — an undeclared severity is not a hue. */
export function severityBand(severityNumber: number): SeverityBand {
  if (severityNumber >= 17) return "error";
  if (severityNumber >= 13) return "warn";
  if (severityNumber >= 9) return "info";
  return "debug";
}

/**
 * Bands in stacking order, Error first: only a segment sitting on the baseline keeps a readable
 * shape across bars, and Error is the one band whose shape over time anyone reads (ADR 0003).
 * Colours are the `-fill` steps, lifted off the rail values which disappear when asked to fill.
 */
export const SEVERITY_BANDS: { key: SeverityBand; label: string; color: string }[] = [
  { key: "error", label: "Error", color: "var(--severity-error-fill)" },
  { key: "warn", label: "Warn", color: "var(--severity-warn-fill)" },
  { key: "info", label: "Info", color: "var(--severity-info-fill)" },
  { key: "debug", label: "Debug", color: "var(--severity-debug-fill)" },
];

/** The highest OTel severity each Band reaches, which is what a minimum-severity threshold tests. */
const BAND_CEILING: Record<SeverityBand, number> = { error: 24, warn: 16, info: 12, debug: 8 };

/**
 * The Bands a minimum-severity threshold still admits. The others cannot appear at all, so naming
 * them in a legend would promise a colour the reader will never see.
 */
export function bandsAdmittedBy(severityMin: number | undefined): typeof SEVERITY_BANDS {
  if (severityMin === undefined || severityMin <= 0) return SEVERITY_BANDS;
  return SEVERITY_BANDS.filter(band => BAND_CEILING[band.key] >= severityMin);
}

// Written out rather than interpolated: Tailwind scans source for literal class names.
const BAND_TEXT: Record<SeverityBand, string> = {
  error: "text-severity-error",
  warn: "text-severity-warn",
  info: "text-severity-info",
  debug: "text-severity-debug",
};

const BAND_RAIL: Record<SeverityBand, string> = {
  error: "bg-severity-error-rail",
  warn: "bg-severity-warn-rail",
  info: "bg-severity-info-rail",
  debug: "bg-severity-debug-rail",
};

export function severityClass(severityNumber: number): string {
  return BAND_TEXT[severityBand(severityNumber)];
}

/** Severity rail (the 3px bar at the left edge of a log row). */
export function severityRailClass(severityNumber: number): string {
  return BAND_RAIL[severityBand(severityNumber)];
}

/**
 * The rail of an episode, whose watch may deal in no severity bands at all. Such an episode still
 * reports trouble, and trouble is red — the rail must not fade to debug grey merely because there
 * is no log to grade. That an episode is not about logs is said by its badge instead, so the
 * colour keeps meaning exactly one thing.
 */
export function episodeRailClass(severity: number | string | null | undefined): string {
  return severityRailClass(severity == null ? 17 : Number(severity));
}

/** Threshold values for the "minimum severity" filter dropdown. */
export const severityFilterOptions = [
  { value: 0, label: "All severities" },
  { value: 9, label: "Info and above" },
  { value: 13, label: "Warn and above" },
  { value: 17, label: "Error and above" },
] as const;
