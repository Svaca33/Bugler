import { describe, expect, test } from "bun:test";

import {
  asLifecycle,
  asOpened,
  effectiveLifecycle,
  effectiveOpened,
  normalizeLifecycle,
  openedFrom,
  resolveServiceIds,
} from "./alertsFilter";

const applications = [
  {
    id: "app-1",
    name: "Eshop",
    services: [
      { id: "s-1", namespace: "acme", environment: "prod", name: "web" },
      { id: "s-2", namespace: "acme", environment: "staging", name: "web" },
      { id: "s-3", namespace: "acme", environment: "prod", name: "worker" },
    ],
  },
  {
    id: "app-2",
    name: "Crm",
    services: [{ id: "s-4", namespace: "acme", environment: "prod", name: "backend" }],
  },
];

describe("resolveServiceIds", () => {
  test("no source facet means no narrowing at all", () => {
    expect(resolveServiceIds(applications, {})).toBeUndefined();
  });

  test("facets intersect, and an application pick scopes the rest", () => {
    expect(resolveServiceIds(applications, { environment: "prod" }))
      .toEqual(["s-1", "s-3", "s-4"]);
    expect(resolveServiceIds(applications, { applicationId: "app-1", environment: "prod" }))
      .toEqual(["s-1", "s-3"]);
    expect(resolveServiceIds(applications, { applicationId: "app-1", service: "web" }))
      .toEqual(["s-1", "s-2"]);
  });

  test("facets matching nothing resolve to an empty set, never to everything", () => {
    expect(resolveServiceIds(applications, { environment: "qa" })).toEqual([]);
  });
});

describe("lifecycle round-trip", () => {
  test("absent means the default trio, an empty array means none", () => {
    expect(effectiveLifecycle({})).toEqual(["Open", "Quieted", "Solved"]);
    expect(effectiveLifecycle({ lifecycle: [] })).toEqual([]);
  });

  test("a hand-rebuilt default trio normalizes back to a clean URL", () => {
    expect(normalizeLifecycle(["Solved", "Open", "Quieted"])).toBeUndefined();
    expect(normalizeLifecycle(["Open"])).toEqual(["Open"]);
    expect(normalizeLifecycle([])).toEqual([]);
  });

  test("parsing drops junk and keeps emptiness", () => {
    expect(asLifecycle(["Open", "bogus", "Muted"])).toEqual(["Open", "Muted"]);
    expect(asLifecycle([])).toEqual([]);
    expect(asLifecycle("Open")).toBeUndefined();
  });
});

describe("opened window", () => {
  test("absent is the 7-day default; 'all' switches the window off", () => {
    expect(effectiveOpened({})).toBe("P7D");
    expect(effectiveOpened({ opened: "all" })).toBeUndefined();
    expect(effectiveOpened({ opened: "P1D" })).toBe("P1D");
  });

  test("only the presets and 'all' survive parsing", () => {
    expect(asOpened("P30D")).toBe("P30D");
    expect(asOpened("all")).toBe("all");
    expect(asOpened("PT5M")).toBeUndefined();
  });

  test("the instant rolls with the clock", () => {
    const from = openedFrom({ opened: "P1D" });
    const expected = Date.now() - 86_400_000;
    expect(Math.abs(Date.parse(from!) - expected)).toBeLessThan(5_000);
    expect(openedFrom({ opened: "all" })).toBeUndefined();
  });
});
