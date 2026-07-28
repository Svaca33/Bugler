import { describe, expect, test } from "bun:test";

import {
  asFilters,
  flattenLeaves,
  isActive,
  leafText,
  removeFilter,
  toQueryParams,
  upsertFilter,
  type AttributeFilter,
} from "./attributeFilters";

const tenant: AttributeFilter = { scope: "attribute", path: ["tenant.id"], value: "acme" };

describe("upsertFilter", () => {
  test("adds a new filter", () => {
    expect(upsertFilter([], tenant)).toEqual([tenant]);
  });

  test("replaces the value for the same scope+path", () => {
    const replaced = upsertFilter([tenant], { ...tenant, value: "beta" });
    expect(replaced).toEqual([{ ...tenant, value: "beta" }]);
  });

  test("keeps filters with the same path but different scope", () => {
    const resource: AttributeFilter = { scope: "resource", path: ["tenant.id"], value: "beta" };
    expect(upsertFilter([tenant], resource)).toHaveLength(2);
  });

  test("distinguishes a literal dotted key from a nested path", () => {
    const nested: AttributeFilter = { scope: "attribute", path: ["tenant", "id"], value: "x" };
    expect(upsertFilter([tenant], nested)).toHaveLength(2);
  });
});

describe("removeFilter and isActive", () => {
  test("removeFilter drops by scope+path", () => {
    expect(removeFilter([tenant], tenant)).toEqual([]);
  });

  test("isActive requires the exact value", () => {
    expect(isActive([tenant], tenant)).toBe(true);
    expect(isActive([tenant], { ...tenant, value: "beta" })).toBe(false);
  });
});

describe("toQueryParams", () => {
  test("splits scopes into attr/res JSON items", () => {
    const filters: AttributeFilter[] = [
      tenant,
      { scope: "resource", path: ["service.name"], value: "web" },
    ];
    expect(toQueryParams(filters)).toEqual({
      attr: ['{"path":["tenant.id"],"value":"acme"}'],
      res: ['{"path":["service.name"],"value":"web"}'],
    });
  });

  test("omits empty scopes", () => {
    expect(toQueryParams([tenant])).toEqual({
      attr: ['{"path":["tenant.id"],"value":"acme"}'],
      res: undefined,
    });
  });
});

describe("asFilters", () => {
  test("accepts valid filters and drops malformed entries", () => {
    expect(asFilters([tenant, { scope: "nope", path: ["x"], value: "y" }, { path: [] }])).toEqual([
      tenant,
    ]);
  });

  test("returns undefined for non-arrays and empty results", () => {
    expect(asFilters("x")).toBeUndefined();
    expect(asFilters([{}])).toBeUndefined();
  });
});

describe("flattenLeaves", () => {
  test("flattens nested objects to dot-path leaves; arrays and null stay unfilterable leaves", () => {
    const leaves = flattenLeaves({
      "tenant.id": "acme",
      retries: 3,
      ok: true,
      order: { total: 42, customer: { city: "Brno" } },
      flags: ["vip"],
      missing: null,
    });
    expect(leaves).toEqual([
      { path: ["tenant.id"], value: "acme", isScalar: true },
      { path: ["retries"], value: 3, isScalar: true },
      { path: ["ok"], value: true, isScalar: true },
      { path: ["order", "total"], value: 42, isScalar: true },
      { path: ["order", "customer", "city"], value: "Brno", isScalar: true },
      { path: ["flags"], value: ["vip"], isScalar: false },
      { path: ["missing"], value: null, isScalar: false },
    ]);
  });

  test("returns no leaves for non-objects", () => {
    expect(flattenLeaves(undefined)).toEqual([]);
    expect(flattenLeaves("x")).toEqual([]);
  });
});

describe("leafText", () => {
  test("matches the text form Postgres #>> yields", () => {
    expect(leafText("acme")).toBe("acme");
    expect(leafText(42)).toBe("42");
    expect(leafText(true)).toBe("true");
    expect(leafText(false)).toBe("false");
  });
});
