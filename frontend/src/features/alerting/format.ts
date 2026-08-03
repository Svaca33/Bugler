import { getFormatLocale, getMessages } from "@/i18n/runtime";

/** "4th" — the recurrence badge counts which time this kind of trouble is burning. */
export function ordinal(n: number): string {
  return getMessages().alerting.format.ordinal(n);
}

/** "12:41:03" — the local wall clock of an instant, the grain the meta lines read at. */
export function clock(timestamp: string): string {
  return new Date(timestamp).toTimeString().slice(0, 8);
}

/** "12:44" — the clock without seconds, for marks where the minute is the story. */
export function clockShort(timestamp: string): string {
  return new Date(timestamp).toTimeString().slice(0, 5);
}

/** "TODAY · 31 JUL", "YESTERDAY · 30 JUL", then "WED · 29 JUL" — the day separators' voice. */
export function dayLabel(timestamp: string, now: number): string {
  const date = new Date(timestamp);
  const locale = getFormatLocale();
  const words = getMessages().alerting.format;
  const dayPart = date
    .toLocaleDateString(locale, { day: "2-digit", month: "short" })
    .toUpperCase();
  if (date.toDateString() === new Date(now).toDateString()) {
    return `${words.todayUpper} · ${dayPart}`;
  }
  if (date.toDateString() === new Date(now - 86_400_000).toDateString()) {
    return `${words.yesterdayUpper} · ${dayPart}`;
  }
  const weekday = date.toLocaleDateString(locale, { weekday: "short" }).toUpperCase();
  return `${weekday} · ${dayPart}`;
}

/** "today 09:12" for the current day, otherwise "29/07 17:41" — the recurrence history's stamps. */
export function historyStamp(timestamp: string, now: number): string {
  const date = new Date(timestamp);
  const time = clockShort(timestamp);
  if (date.toDateString() === new Date(now).toDateString()) {
    return getMessages().alerting.format.todayAt(time);
  }
  const day = date.toLocaleDateString(getFormatLocale(), { day: "2-digit", month: "2-digit" });
  return `${day} ${time}`;
}
