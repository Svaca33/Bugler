import { getFormatLocale, getMessages } from "@/i18n/runtime";
import { describeDuration, durationMs, rangePresets } from "@/lib/duration";

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
  const words = getMessages().explore.timeFilter;
  if (value.range !== undefined) return describeDuration(value.range);
  if (value.from !== undefined && value.to !== undefined) {
    return `${shortTime(value.from)} → ${shortTime(value.to)}`;
  }
  if (value.from !== undefined) return words.fromInstant(shortTime(value.from));
  if (value.to !== undefined) return words.untilInstant(shortTime(value.to));
  return words.allTime;
}

/** Names the window, so an empty list never reads as "nothing was ever ingested". */
export function emptyStateMessage(
  subject: "logRecords" | "traces",
  value: TimeFilterValue,
): string {
  const words = getMessages().explore.timeFilter;
  const named = words.subjects[subject];
  if (value.range !== undefined) {
    return words.noneInLast(named, describeDuration(value.range));
  }
  if (value.from !== undefined || value.to !== undefined) {
    return words.noneIn(named, timeFilterLabel(value));
  }
  return words.noneMatch(named);
}

/** The two next-wider presets, offered when the current window turned up nothing. */
export function widerPresets(value: TimeFilterValue): { value: string; label: string }[] {
  if (value.range === undefined) return [];
  const current = durationMs(value.range) ?? 0;
  return rangePresets().filter(preset => (durationMs(preset.value) ?? 0) > current).slice(0, 2);
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
  return `${date.toLocaleDateString(getFormatLocale())} ${date.getHours().toString().padStart(2, "0")}:${date
    .getMinutes()
    .toString()
    .padStart(2, "0")}`;
}
