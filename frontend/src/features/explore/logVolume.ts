/**
 * Reading a Volume response: which Buckets the Resolved Window cut, and how to name them.
 *
 * The server reports the window and the common Bucket width and nothing else about the edges or
 * the labels — everything here is derivable from those two, so there is no extra field to keep in
 * step. Times render in the viewer's local zone even though Buckets are aligned in UTC, so a daily
 * bar in Central Europe is honestly labelled 02:00 rather than pretending to be midnight.
 */

import { getFormatLocale, getMessages } from "@/i18n/runtime";

const MINUTE = 60_000;
const DAY = 86_400_000;

/**
 * The Resolved Window a Volume came back with, the width its Buckets share, and the instant the
 * server resolved it at. `now` is the server's reading and never the viewer's: their clocks
 * disagree, and the whole point of it here is to be believed.
 */
export interface VolumeWindow {
  from: string;
  to: string;
  now: string;
  widthMs: number;
}

/**
 * A Bucket that renders lower than the traffic in it deserves: `leading` and `trailing` are cut by
 * the window, `elapsing` has simply not finished happening yet.
 */
export type BucketEdge = "leading" | "trailing" | "elapsing" | null;

/**
 * Buckets sit on multiples of their width, but a Resolved Window rarely does, so the first and
 * last Bucket of a Volume are usually only partly inside it. They render lower through no fault
 * of the traffic, which is why the chart has to mark them rather than let them read as a dip.
 *
 * The Bucket holding `now` is the case that cannot be read off the window at all. Its top is
 * stretched to cover the newest record, so a Bucket still filling closes exactly on `to` and looks
 * finished — which under Follow is the state the chart is in permanently, always ending on a dip
 * that is only the clock.
 */
export function bucketEdge(start: string, window: VolumeWindow): BucketEdge {
  const at = new Date(start).getTime();
  const closes = at + window.widthMs;
  const now = new Date(window.now).getTime();
  if (at < new Date(window.from).getTime()) return "leading";
  if (at <= now && now < closes) return "elapsing";
  if (closes > new Date(window.to).getTime()) return "trailing";
  return null;
}

/**
 * Whether a bare clock time still identifies a Bucket. Once a window reaches across local midnight
 * it does not: "14:00" appears once a day, and on a week-long window the axis would repeat it seven
 * times without saying which one you are looking at.
 */
export function axisNeedsDate(window: VolumeWindow): boolean {
  return new Date(window.from).toDateString() !== new Date(window.to).toDateString();
}

/**
 * The axis label for one Bucket, carrying exactly as much as the window makes ambiguous: seconds
 * when Buckets are shorter than a minute, a date once the window crosses a day, and no clock time
 * at all once a Bucket is a whole day or more — the time would read 00:00 on every tick.
 */
export function bucketLabel(start: string, window: VolumeWindow): string {
  const at = new Date(start);
  if (window.widthMs >= DAY) return day(at);

  const time = at.toLocaleTimeString(
    getFormatLocale(),
    window.widthMs < MINUTE
      ? { hour: "2-digit", minute: "2-digit", second: "2-digit" }
      : { hour: "2-digit", minute: "2-digit" },
  );
  return axisNeedsDate(window) ? `${day(at)} ${time}` : time;
}

/**
 * A short Bucket at the top has two causes that look identical and mean the opposite: the window
 * cut it, so nothing more is coming, or it has not finished elapsing, so it will grow. Which one
 * it is comes from the window's own `now` rather than from a guess about the Time Filter.
 */
export function describeBucket(start: string, window: VolumeWindow, edge: BucketEdge): string {
  const opened = new Date(start);
  const closed = new Date(opened.getTime() + window.widthMs);
  const dated = axisNeedsDate(window);
  // The closing end repeats the date only when the Bucket itself crosses midnight.
  const sameDay = opened.toDateString() === closed.toDateString();
  const span =
    `${dated ? `${day(opened)} ` : ""}${clock(opened, window.widthMs)}` +
    ` – ${dated && !sameDay ? `${day(closed)} ` : ""}${clock(closed, window.widthMs)}`;

  const words = getMessages().explore.volume;
  if (edge === "leading") return words.bucketPartlyInside(span);
  if (edge === "elapsing") return words.bucketElapsing(span);
  if (edge === "trailing") return words.bucketCut(span);
  return span;
}

/** The Bucket width in the same words the Time Filter presets use. */
export function describeWidth(widthMs: number): string {
  if (widthMs >= DAY) return `${Math.round(widthMs / DAY)} d`;
  if (widthMs >= 3_600_000) return `${Math.round(widthMs / 3_600_000)} h`;
  if (widthMs >= MINUTE) return `${Math.round(widthMs / MINUTE)} min`;
  return `${Math.round(widthMs / 1000)} s`;
}

function day(date: Date): string {
  return date.toLocaleDateString(getFormatLocale(), { day: "numeric", month: "numeric" });
}

function clock(date: Date, widthMs: number): string {
  return date.toLocaleTimeString(
    getFormatLocale(),
    widthMs < MINUTE
      ? { hour: "2-digit", minute: "2-digit", second: "2-digit" }
      : { hour: "2-digit", minute: "2-digit" },
  );
}
