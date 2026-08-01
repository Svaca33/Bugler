import { describe, expect, test } from "bun:test";

import {
  isDefaultBoard,
  readDashboardSearch,
  sanitizeDashboardSearch,
  writeDashboardSearch,
} from "./dashboardMemory";

function fakeStorage(initial: Record<string, string> = {}) {
  const entries = new Map(Object.entries(initial));
  return {
    getItem: (key: string) => entries.get(key) ?? null,
    setItem: (key: string, value: string) => void entries.set(key, value),
    dump: () => Object.fromEntries(entries),
  };
}

describe("sanitize", () => {
  test("anything malformed is dropped, never guessed", () => {
    expect(
      sanitizeDashboardSearch({
        range: "15m", layout: "grid", trouble: "yes", volume: 0, groupBy: 7,
      }),
    ).toEqual({
      range: undefined, layout: undefined, trouble: undefined, volume: undefined, groupBy: undefined,
    });
  });

  test("valid values survive, including a hand-typed range", () => {
    expect(
      sanitizeDashboardSearch({
        range: "P30D", layout: "table", trouble: true, volume: false, groupBy: "app,service",
      }),
    ).toEqual({ range: "P30D", layout: "table", trouble: true, volume: false, groupBy: "app,service" });
  });
});

describe("the remembered board", () => {
  test("what was written comes back, and defaults come back as nothing", () => {
    const storage = fakeStorage();
    writeDashboardSearch({ range: "P7D", layout: "table", groupBy: "app" }, storage);
    expect(readDashboardSearch(storage)).toEqual(
      sanitizeDashboardSearch({ range: "P7D", layout: "table", groupBy: "app" }),
    );

    // Setting everything back to defaults leaves nothing worth restoring.
    writeDashboardSearch({}, storage);
    expect(readDashboardSearch(storage)).toBeUndefined();
  });

  test("a corrupt or absent entry reads as nothing rather than throwing", () => {
    expect(readDashboardSearch(fakeStorage())).toBeUndefined();
    expect(readDashboardSearch(fakeStorage({ "bugler.dashboard.board": "{not json" }))).toBeUndefined();
    expect(readDashboardSearch(fakeStorage({ "bugler.dashboard.board": '"a string"' }))).toBeUndefined();
    expect(readDashboardSearch(null)).toBeUndefined();
  });

  test("isDefaultBoard tells the clean board from a configured one", () => {
    expect(isDefaultBoard({})).toBe(true);
    expect(isDefaultBoard({ trouble: true })).toBe(false);
  });
});
