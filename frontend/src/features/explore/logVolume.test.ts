import { expect, test } from "bun:test";

import {
  axisNeedsDate,
  bucketEdge,
  bucketLabel,
  describeBucket,
  describeWidth,
  type VolumeWindow,
} from "./logVolume";

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** Instants are built from local wall-clock parts so the assertions hold in any zone. */
const at = (day: number, hour: number, minute = 0, second = 0) =>
  new Date(2026, 6, day, hour, minute, second).toISOString();

const oneHour: VolumeWindow = { from: at(29, 13, 3, 20), to: at(29, 14, 3, 20), widthMs: 30_000 };
const oneWeek: VolumeWindow = { from: at(23, 9), to: at(30, 9), widthMs: 2 * HOUR };
const oneMonth: VolumeWindow = { from: at(1, 0), to: at(30, 0), widthMs: DAY };

test("marks the Bucket the window opened inside as leading", () => {
  expect(bucketEdge(at(29, 13, 3, 0), oneHour)).toBe("leading");
  expect(bucketEdge(at(29, 13, 3, 30), oneHour)).toBe(null);
});

test("marks the Bucket that reaches past the window as trailing", () => {
  expect(bucketEdge(at(29, 14, 3, 0), oneHour)).toBe("trailing");
  expect(bucketEdge(at(29, 14, 2, 30), oneHour)).toBe(null);
});

test("leaves both edges whole when the window falls on Bucket boundaries", () => {
  const aligned: VolumeWindow = { from: at(29, 13), to: at(29, 14), widthMs: MINUTE };

  expect(bucketEdge(at(29, 13), aligned)).toBe(null);
  expect(bucketEdge(at(29, 13, 59), aligned)).toBe(null);
});

test("a window inside one day needs no date on the axis, one crossing midnight does", () => {
  expect(axisNeedsDate(oneHour)).toBe(false);
  expect(axisNeedsDate(oneWeek)).toBe(true);
});

test("an axis label carries only what the window makes ambiguous", () => {
  // Sub-minute Buckets: seconds matter, the date does not.
  expect(bucketLabel(at(29, 14, 3, 30), oneHour)).toMatch(/\d{2}:\d{2}:30/);

  // Across a week a bare clock time repeats seven times, so the day comes along.
  const weekly = bucketLabel(at(25, 14), oneWeek);
  expect(weekly).toContain("25");
  expect(weekly).toMatch(/\d{2}:\d{2}/);

  // Whole-day Buckets: the clock would read the same on every tick, so it is dropped.
  const monthly = bucketLabel(at(12, 0), oneMonth);
  expect(monthly).toContain("12");
  expect(monthly).not.toContain(":");
});

test("tells a Bucket still elapsing apart from one the window cut", () => {
  const start = at(29, 14, 3, 0);

  expect(describeBucket(start, oneHour, "trailing", false)).toContain("still elapsing");
  expect(describeBucket(start, oneHour, "trailing", true)).toContain("cut by the window");
  expect(describeBucket(start, oneHour, "leading", false)).toContain("only partly inside");
  expect(describeBucket(start, oneHour, null, false)).not.toContain("·");
});

test("a tooltip repeats the date only when the Bucket crosses midnight", () => {
  // Inside a single day the date is on neither end.
  expect(describeBucket(at(29, 14), oneHour, null, false)).not.toContain("29");

  // Across a week both ends sit on the same day, so it is named once.
  const sameDay = describeBucket(at(25, 14), oneWeek, null, false);
  expect(sameDay.match(/25/g)).toHaveLength(1);

  // A whole-day Bucket ends on the next day, which has to be spelled out.
  const overnight = describeBucket(at(12, 0), oneMonth, null, false);
  expect(overnight).toContain("12");
  expect(overnight).toContain("13");
});

test("names the Bucket width in the units the presets use", () => {
  expect(describeWidth(5_000)).toBe("5 s");
  expect(describeWidth(MINUTE)).toBe("1 min");
  expect(describeWidth(30 * MINUTE)).toBe("30 min");
  expect(describeWidth(12 * HOUR)).toBe("12 h");
  expect(describeWidth(DAY)).toBe("1 d");
});
