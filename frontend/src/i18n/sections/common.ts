/**
 * Words shared across features: durations, severity thresholds, loading states. Severity Band
 * names themselves (Error, WARN, FATAL…) are the domain's vocabulary and stay untranslated —
 * only the prose around them is a language's.
 */
export interface CommonMessages {
  loading: string;
  loadFailed: {
    session: string;
    releases: string;
    catalog: string;
  };
  /** Labels of the clickable Relative Range presets, keyed by their ISO duration. */
  rangePreset: Record<string, string>;
  /** "Last 45 min" given "45 min" — custom spans phrased like the presets. */
  lastSpan(parts: string): string;
  underAMinute: string;
  rangeUnits: {
    minutes: string;
    hours: string;
    days: string;
    weeks: string;
    months: string;
  };
  severityFilter: {
    all: string;
    infoAndAbove: string;
    warnAndAbove: string;
    errorAndAbove: string;
  };
}
