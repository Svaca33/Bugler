/** "4th" — the recurrence badge counts which time this kind of trouble is burning. */
export function ordinal(n: number): string {
  const tail = n % 100;
  const suffix = tail >= 11 && tail <= 13
    ? "th"
    : n % 10 === 1 ? "st" : n % 10 === 2 ? "nd" : n % 10 === 3 ? "rd" : "th";
  return `${n}${suffix}`;
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
  const dayPart = date
    .toLocaleDateString("en-GB", { day: "2-digit", month: "short" })
    .toUpperCase();
  if (date.toDateString() === new Date(now).toDateString()) return `TODAY · ${dayPart}`;
  if (date.toDateString() === new Date(now - 86_400_000).toDateString()) {
    return `YESTERDAY · ${dayPart}`;
  }
  const weekday = date.toLocaleDateString("en-GB", { weekday: "short" }).toUpperCase();
  return `${weekday} · ${dayPart}`;
}

/** "today 09:12" for the current day, otherwise "29/07 17:41" — the recurrence history's stamps. */
export function historyStamp(timestamp: string, now: number): string {
  const date = new Date(timestamp);
  const time = clockShort(timestamp);
  if (date.toDateString() === new Date(now).toDateString()) return `today ${time}`;
  return `${date.toLocaleDateString("en-GB", { day: "2-digit", month: "2-digit" })} ${time}`;
}
