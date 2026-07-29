import { expect, test } from "bun:test";

import { describeDuration, durationMs, toDuration } from "./duration";

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
