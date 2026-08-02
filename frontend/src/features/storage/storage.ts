/**
 * The arithmetic behind the Storage view, kept apart from rendering: unit conversion for the
 * Ingest Rate, the Settled Footprint projection, and the row shape the table prices.
 * Everything here works on one measured window — nothing is measured beyond it, so a week or
 * a month is an extrapolation and the UI must say so (`projected`).
 */

export type RateUnit = "second" | "minute" | "hour" | "day" | "week" | "month";

export interface RateStep {
  unit: RateUnit;
  label: string;
  ms: number;
  projected: boolean;
}

const DAY_MS = 24 * 60 * 60 * 1000;

const PER_DAY: RateStep = { unit: "day", label: "per day", ms: DAY_MS, projected: false };

/** A month of 30 days: the projection is honest about being a round number, not a calendar. */
export const RATE_UNITS: RateStep[] = [
  { unit: "second", label: "per second", ms: 1000, projected: false },
  { unit: "minute", label: "per minute", ms: 60 * 1000, projected: false },
  { unit: "hour", label: "per hour", ms: 60 * 60 * 1000, projected: false },
  PER_DAY,
  { unit: "week", label: "per week (projected)", ms: 7 * DAY_MS, projected: true },
  { unit: "month", label: "per month (projected)", ms: 30 * DAY_MS, projected: true },
];

export function rateStep(unit: RateUnit): RateStep {
  return RATE_UNITS.find(step => step.unit === unit) ?? PER_DAY;
}

/** One signal kind's numbers, already coerced out of the wire's `number | string` int64s. */
export interface KindStorage {
  footprintBytes: number;
  retentionDays: number;
  windowBytes: number;
}

export interface StorageRow {
  serviceId: string;
  logs: KindStorage;
  traces: KindStorage;
}

/** Bytes arriving per chosen unit, from what the measured window actually saw. */
export function ratePer(windowBytes: number, windowMs: number, unit: RateUnit): number {
  if (windowMs <= 0) return 0;
  return (windowBytes / windowMs) * rateStep(unit).ms;
}

/**
 * The Settled Footprint: where this Service comes to rest if today's pace holds — one day's
 * bytes times the Effective Retention, the log and trace clocks each on their own.
 */
export function settledBytes(row: StorageRow, windowMs: number): number {
  return (
    ratePer(row.logs.windowBytes, windowMs, "day") * row.logs.retentionDays +
    ratePer(row.traces.windowBytes, windowMs, "day") * row.traces.retentionDays
  );
}

export function totalFootprint(row: StorageRow): number {
  return row.logs.footprintBytes + row.traces.footprintBytes;
}
