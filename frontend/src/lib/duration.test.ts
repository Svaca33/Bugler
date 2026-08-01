import { expect, test } from "bun:test";

import {
  describeDuration,
  describeLiveMillis,
  describeMillis,
  durationMs,
  toDuration,
} from "./duration";

test("a live span ticks in bare seconds, padded minutes, then padded hours", () => {
  expect(describeLiveMillis(0)).toBe("0 s");
  expect(describeLiveMillis(36_000)).toBe("36 s");
  expect(describeLiveMillis(-500)).toBe("0 s");
  expect(describeLiveMillis(61_000)).toBe("1 min 01 s");
  expect(describeLiveMillis(22 * 60_000 + 36_000)).toBe("22 min 36 s");
  expect(describeLiveMillis(60 * 60_000)).toBe("1 h 00 min");
  expect(describeLiveMillis(3 * 3_600_000 + 5 * 60_000 + 59_000)).toBe("3 h 05 min");
});

test("a week is seven days and a month thirty, matching the server", () => {
  expect(toDuration(2, "minutes")).toBe("PT2M");
  expect(toDuration(3, "hours")).toBe("PT3H");
  expect(toDuration(4, "days")).toBe("P4D");
  expect(toDuration(2, "weeks")).toBe("P14D");
  expect(toDuration(3, "months")).toBe("P90D");
});

test("only a whole positive amount makes a range", () => {
  expect(toDuration(0, "hours")).toBeUndefined();
  expect(toDuration(-1, "hours")).toBeUndefined();
  expect(toDuration(1.5, "hours")).toBeUndefined();
  expect(toDuration(Number.NaN, "hours")).toBeUndefined();
});

test("durations are measured with the same fixed lengths the server uses", () => {
  expect(durationMs("PT15M")).toBe(15 * 60_000);
  expect(durationMs("P7D")).toBe(7 * 86_400_000);
  expect(durationMs("P1M")).toBe(30 * 86_400_000);
  expect(durationMs("PT1H30M")).toBe(90 * 60_000);
});

test("anything that is not a positive ISO duration is refused", () => {
  for (const text of ["15m", "PT0S", "-P1D", "P", "", "2026-07-28T14:12:00Z"]) {
    expect(durationMs(text)).toBeUndefined();
  }
});

test("a custom duration is phrased like the presets", () => {
  expect(describeDuration("PT1H")).toBe("Last 1 h");
  expect(describeDuration("PT45M")).toBe("Last 45 min");
  expect(describeDuration("P90D")).toBe("Last 90 d");
});

test("a millisecond span reads in words and never as zero", () => {
  expect(describeMillis(30_000)).toBe("under a minute");
  expect(describeMillis(42 * 60_000)).toBe("42 min");
  expect(describeMillis(75 * 60_000)).toBe("1 h 15 min");
  expect(describeMillis(26 * 3_600_000)).toBe("1 d 2 h");
});
