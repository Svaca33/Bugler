/** ISO-8601 durations as the server reads them: fixed lengths, no calendar arithmetic. */

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

/**
 * A running span in words that tick every second: bare seconds under a minute, "M min SS s"
 * under an hour, "H h MM min" above it. `describeMillis` stays for closed spans, where seconds
 * would be noise.
 */
export function describeLiveMillis(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000));
  if (totalSeconds < 60) return `${totalSeconds} s`;
  const minutes = Math.floor(totalSeconds / 60);
  if (minutes < 60) return `${minutes} min ${String(totalSeconds % 60).padStart(2, "0")} s`;
  const hours = Math.floor(minutes / 60);
  return `${hours} h ${String(minutes % 60).padStart(2, "0")} min`;
}

/** A span measured in milliseconds, in words: "1 h 15 min", "42 min", "under a minute". */
export function describeMillis(milliseconds: number): string {
  if (milliseconds < MINUTE) return "under a minute";
  const days = Math.floor(milliseconds / DAY);
  const hours = Math.floor((milliseconds % DAY) / HOUR);
  const minutes = Math.floor((milliseconds % HOUR) / MINUTE);
  const parts: string[] = [];
  if (days > 0) parts.push(`${days} d`);
  if (hours > 0) parts.push(`${hours} h`);
  if (minutes > 0) parts.push(`${minutes} min`);
  return parts.join(" ");
}

function count(value: string | undefined): number {
  return value === undefined ? 0 : Number(value);
}
