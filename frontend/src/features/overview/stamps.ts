import { getFormatLocale } from "@/i18n/runtime";

/** "27/07 14:00" — the axis stamps under a sparkline and each bar's own title. */
export function windowStamp(instant: string): string {
  const date = new Date(instant);
  const day = date.toLocaleDateString(getFormatLocale(), { day: "2-digit", month: "2-digit" });
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");
  return `${day} ${hours}:${minutes}`;
}

/** "12:04:41" — local wall clock of an instant, the same voice as the Episodes meta lines. */
export function clock(timestamp: string): string {
  return new Date(timestamp).toTimeString().slice(0, 8);
}
