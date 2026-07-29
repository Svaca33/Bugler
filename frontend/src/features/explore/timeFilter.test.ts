import { expect, test } from "bun:test";

import {
  asInstant,
  asRange,
  emptyStateMessage,
  instantToLocalInput,
  localInputToInstant,
  widerPresets,
} from "./timeFilter";

test("anything that is not a positive ISO duration is refused as a range", () => {
  for (const text of ["15m", "PT0S", "-P1D", "P", "", "2026-07-28T14:12:00Z"]) {
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
