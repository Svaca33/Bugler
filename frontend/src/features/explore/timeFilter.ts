/**
 * Time Filter model (ADR 0002, Exploration): either a Relative Range — an ISO-8601 duration the
 * server resolves against its own clock — or an Absolute Range of instants carrying an offset.
 * Never both; the API answers 400. Absent means no time constraint at all.
 */
export interface TimeFilterValue {
  range?: string;
  from?: string;
  to?: string;
}

/** Written into the URL on arrival, so what you are looking at is always spelled out. */
export const DEFAULT_RANGE = "PT1H";

/** Spread before a new Time Filter so the other form's parameters never linger in the URL. */
export const EMPTY_TIME: TimeFilterValue = { range: undefined, from: undefined, to: undefined };

export const RANGE_PRESETS: { value: string; label: string }[] = [
  { value: "PT5M", label: "Last 5 min" },
  { value: "PT15M", label: "Last 15 min" },
  { value: "PT1H", label: "Last 1 h" },
  { value: "P1D", label: "Last 24 h" },
  { value: "P7D", label: "Last 7 d" },
  { value: "P30D", label: "Last 30 d" },
];

export type RangeUnit = "minutes" | "hours" | "days" | "weeks" | "months";

export const RANGE_UNITS: { value: RangeUnit; label: string }[] = [
  { value: "minutes", label: "minutes" },
  { value: "hours", label: "hours" },
  { value: "days", label: "days" },
  { value: "weeks", label: "weeks" },
  { value: "months", label: "months" },
];

const DURATION =
  /^P(?!$)(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)W)?(?:(\d+)D)?(?:T(?!$)(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?$/;

const SECOND = 1000;
const MINUTE = 60 * SECOND;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** A week is 7 days and a month 30 days — the fixed lengths the server uses, so the two agree. */
export function toDuration(amount: number, unit: RangeUnit): string | undefined {
  if (!Number.isInteger(amount) || amount <= 0) return undefined;
  switch (unit) {
    case "minutes":
      return `PT${amount}M`;
    case "hours":
      return `PT${amount}H`;
    case "days":
      return `P${amount}D`;
    case "weeks":
      return `P${amount * 7}D`;
    case "months":
      return `P${amount * 30}D`;
  }
}

/** Length of a Relative Range in milliseconds, or undefined when the text is not one. */
export function durationMs(iso: string): number | undefined {
  const match = DURATION.exec(iso);
  if (match === null) return undefined;
  const [, years, months, weeks, days, hours, minutes, seconds] = match;
  const total =
    count(years) * 365 * DAY +
    count(months) * 30 * DAY +
    count(weeks) * 7 * DAY +
    count(days) * DAY +
    count(hours) * HOUR +
    count(minutes) * MINUTE +
    count(seconds) * SECOND;
  return total > 0 ? total : undefined;
}

/** Validates a URL search value into a Relative Range; anything malformed is dropped. */
export function asRange(value: unknown): string | undefined {
  return typeof value === "string" && durationMs(value) !== undefined ? value : undefined;
}

/** Validates a URL search value into one end of an Absolute Range; the offset is mandatory. */
export function asInstant(value: unknown): string | undefined {
  if (typeof value !== "string" || !/(Z|[+-]\d{2}:?\d{2})$/.test(value)) return undefined;
  return Number.isNaN(new Date(value).getTime()) ? undefined : value;
}

/** The label on the control: what window the list is showing right now. */
export function timeFilterLabel(value: TimeFilterValue): string {
  if (value.range !== undefined) return describeDuration(value.range);
  if (value.from !== undefined && value.to !== undefined) {
    return `${shortTime(value.from)} → ${shortTime(value.to)}`;
  }
  if (value.from !== undefined) return `From ${shortTime(value.from)}`;
  if (value.to !== undefined) return `Until ${shortTime(value.to)}`;
  return "All time";
}

/** Same phrasing as the presets, so "Last 45 min" reads like the ones you can click. */
export function describeDuration(iso: string): string {
  const preset = RANGE_PRESETS.find(option => option.value === iso);
  if (preset !== undefined) return preset.label;

  const match = DURATION.exec(iso);
  if (match === null) return iso;
  const parts: string[] = [];
  const units = ["y", "mo", "w", "d", "h", "min", "s"];
  for (const [index, unit] of units.entries()) {
    const amount = match[index + 1];
    if (amount !== undefined) parts.push(`${Number(amount)} ${unit}`);
  }
  return parts.length > 0 ? `Last ${parts.join(" ")}` : iso;
}

/** Names the window, so an empty list never reads as "nothing was ever ingested". */
export function emptyStateMessage(subject: string, value: TimeFilterValue): string {
  if (value.range !== undefined) {
    return `No ${subject} in the ${describeDuration(value.range).toLowerCase()}.`;
  }
  if (value.from !== undefined || value.to !== undefined) {
    return `No ${subject} in ${timeFilterLabel(value)}.`;
  }
  return `No ${subject} match the current filters.`;
}

/** The two next-wider presets, offered when the current window turned up nothing. */
export function widerPresets(value: TimeFilterValue): { value: string; label: string }[] {
  if (value.range === undefined) return [];
  const current = durationMs(value.range) ?? 0;
  return RANGE_PRESETS.filter(preset => (durationMs(preset.value) ?? 0) > current).slice(0, 2);
}

/** Browser-local wall clock from a datetime-local field → the instant the API takes. */
export function localInputToInstant(value: string): string | undefined {
  if (value === "") return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

/** The reverse, so reopening the editor shows the window in the same local time as the list. */
export function instantToLocalInput(instant: string | undefined): string {
  if (instant === undefined) return "";
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (part: number) => part.toString().padStart(2, "0");
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  );
}

function shortTime(instant: string): string {
  const date = new Date(instant);
  return `${date.toLocaleDateString()} ${date.getHours().toString().padStart(2, "0")}:${date
    .getMinutes()
    .toString()
    .padStart(2, "0")}`;
}

function count(value: string | undefined): number {
  return value === undefined ? 0 : Number(value);
}
