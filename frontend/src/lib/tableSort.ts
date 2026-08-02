/**
 * Which column a table is sorted by, remembered in the browser. Like the detail width this is a
 * reading preference, not shareable state, so it stays out of the URL; each table remembers under
 * its own `bugler.<area>.<thing>` key.
 */

export interface TableSort {
  column: string;
  desc: boolean;
}

type SortStorage = Pick<Storage, "getItem" | "setItem">;

function storageOrNull(): SortStorage | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

/**
 * Anything malformed — or naming a column the table no longer sorts by — is dropped, never
 * guessed: the caller falls back to its default order.
 */
export function readTableSort(
  key: string,
  sortableColumns: readonly string[],
  storage: SortStorage | null = storageOrNull(),
): TableSort | undefined {
  try {
    const raw = storage?.getItem(key);
    if (raw == null) return undefined;
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== "object" || parsed === null) return undefined;
    const { column, desc } = parsed as Record<string, unknown>;
    if (typeof column !== "string" || typeof desc !== "boolean") return undefined;
    if (!sortableColumns.includes(column)) return undefined;
    return { column, desc };
  } catch {
    return undefined;
  }
}

export function writeTableSort(
  key: string,
  sort: TableSort,
  storage: SortStorage | null = storageOrNull(),
): void {
  try {
    storage?.setItem(key, JSON.stringify(sort));
  } catch {
    // Storage can be full or switched off. Losing a sort preference is not worth an error path.
  }
}
