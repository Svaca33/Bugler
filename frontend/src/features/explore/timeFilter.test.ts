import { expect, test } from "bun:test";

import {
  asInstant,
  asRange,
  describeDuration,
  durationMs,
  emptyStateMessage,
  instantToLocalInput,
  localInputToInstant,
  toDuration,
  widerPresets,
} from "./timeFilter";

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
    expect(asRange(text)).toBeUndefined();
  }
  expect(asRange("PT15M")).toBe("PT15M");
  expect(asRange(15)).toBeUndefined();
});

test("an instant without an offset never reaches the API", () => {
  expect(asInstant("2026-07-28T14:12:00Z")).toBe("2026-07-28T14:12:00Z");
  expect(asInstant("2026-07-28T16:12:00+02:00")).toBe("2026-07-28T16:12:00+02:00");
  expect(asInstant("2026-07-28T14:12:00")).toBeUndefined();
  expect(asInstant("yesterday")).toBeUndefined();
  expect(asInstant(undefined)).toBeUndefined();
});

test("a custom duration is phrased like the presets", () => {
  expect(describeDuration("PT1H")).toBe("Last 1 h");
  expect(describeDuration("PT45M")).toBe("Last 45 min");
  expect(describeDuration("P90D")).toBe("Last 90 d");
});

test("local wall clock survives the round trip through the wire format", () => {
  const local = "2026-07-28T14:12:30";
  const instant = localInputToInstant(local);

  expect(instant).toMatch(/Z$/);
  expect(instantToLocalInput(instant)).toBe(local);
  expect(localInputToInstant("")).toBeUndefined();
  expect(instantToLocalInput(undefined)).toBe("");
});

test("the empty state names the window instead of blaming the filters", () => {
  expect(emptyStateMessage("log records", { range: "PT1H" })).toBe("No log records in the last 1 h.");
  expect(emptyStateMessage("log records", {})).toBe("No log records match the current filters.");
});

test("widening is offered only upwards, and only for a relative range", () => {
  expect(widerPresets({ range: "PT1H" }).map(preset => preset.value)).toEqual(["P1D", "P7D"]);
  expect(widerPresets({ range: "P30D" })).toEqual([]);
  expect(widerPresets({ from: "2026-07-28T14:12:00Z" })).toEqual([]);
});
