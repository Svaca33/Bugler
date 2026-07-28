/**
 * Attribute Filter model (ADR 0001, Exploration): explicit scope + path segments + exact value.
 * Paths are never parsed from dotted text — dots inside OTel keys make that ambiguous.
 */
export type FilterScope = "attribute" | "resource";

export interface AttributeFilter {
  scope: FilterScope;
  path: string[];
  value: string;
}

export function sameKey(
  a: Pick<AttributeFilter, "scope" | "path">,
  b: Pick<AttributeFilter, "scope" | "path">,
): boolean {
  return a.scope === b.scope && a.path.length === b.path.length && a.path.every((s, i) => s === b.path[i]);
}

/** True when this exact scope+path+value is currently filtered (drives the − magnifier). */
export function isActive(filters: AttributeFilter[], filter: AttributeFilter): boolean {
  return filters.some(f => sameKey(f, filter) && f.value === filter.value);
}

/** Adds the filter; an existing filter on the same scope+path is replaced, never duplicated. */
export function upsertFilter(filters: AttributeFilter[], filter: AttributeFilter): AttributeFilter[] {
  return [...filters.filter(f => !sameKey(f, filter)), filter];
}

export function removeFilter(filters: AttributeFilter[], filter: AttributeFilter): AttributeFilter[] {
  return filters.filter(f => !sameKey(f, filter));
}

/** API wire format: repeated `attr`/`res` query items, each a JSON {"path":[…],"value":"…"}. */
export function toQueryParams(filters: AttributeFilter[]): { attr?: string[]; res?: string[] } {
  const serialize = (scope: FilterScope) =>
    filters.filter(f => f.scope === scope).map(f => JSON.stringify({ path: f.path, value: f.value }));
  const attr = serialize("attribute");
  const res = serialize("resource");
  return { attr: attr.length > 0 ? attr : undefined, res: res.length > 0 ? res : undefined };
}

/** Display form only — joined with dots, not parseable back (segments may contain dots). */
export function displayPath(path: string[]): string {
  return path.join(".");
}

/** Validates a URL search value into filters; anything malformed is dropped. */
export function asFilters(value: unknown): AttributeFilter[] | undefined {
  if (!Array.isArray(value)) return undefined;
  const filters = value.filter(
    (item): item is AttributeFilter =>
      typeof item === "object" &&
      item !== null &&
      ((item as AttributeFilter).scope === "attribute" || (item as AttributeFilter).scope === "resource") &&
      Array.isArray((item as AttributeFilter).path) &&
      (item as AttributeFilter).path.length > 0 &&
      (item as AttributeFilter).path.every(s => typeof s === "string" && s.length > 0) &&
      typeof (item as AttributeFilter).value === "string",
  );
  return filters.length > 0 ? filters : undefined;
}

export interface AttributeLeaf {
  path: string[];
  value: unknown;
  /** Scalars get a magnifier; arrays and other non-scalars render as JSON without one. */
  isScalar: boolean;
}

/** Flattens a jsonb attributes object into leaf rows; nested objects become dot-path rows. */
export function flattenLeaves(attributes: unknown): AttributeLeaf[] {
  if (typeof attributes !== "object" || attributes === null || Array.isArray(attributes)) return [];
  const leaves: AttributeLeaf[] = [];
  const walk = (node: Record<string, unknown>, prefix: string[]) => {
    for (const [key, value] of Object.entries(node)) {
      const path = [...prefix, key];
      if (typeof value === "object" && value !== null && !Array.isArray(value)) {
        walk(value as Record<string, unknown>, path);
      } else {
        // JSON null is not filterable: `#>>` yields SQL NULL, which text equality never matches.
        leaves.push({ path, value, isScalar: value !== null && typeof value !== "object" });
      }
    }
  };
  walk(attributes as Record<string, unknown>, []);
  return leaves;
}

/** The exact text `#>>` yields for a scalar, so a magnifier chip matches what SQL compares. */
export function leafText(value: unknown): string {
  if (typeof value === "string") return value;
  if (typeof value === "boolean") return value ? "true" : "false";
  return String(value);
}
