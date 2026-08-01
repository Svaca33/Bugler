import { durationMs } from "@/lib/duration";

/**
 * The board's URL state, all in the route's validateSearch, so a board is linkable. Absent
 * parameters are the defaults (P1D, cards, volume shown, app·namespace·environment).
 */
export interface DashboardSearch {
  range?: string;
  layout?: "table";
  trouble?: true;
  volume?: false;
  groupBy?: string;
}

/**
 * The reader's own board, remembered in the browser: which window, layout, toggles and grouping
 * they last chose. The URL stays the authority — a deep link renders exactly what it names and
 * never overwrites the memory; only touching a control does.
 */
const STORAGE_KEY = "bugler.dashboard.board";

type BoardStorage = Pick<Storage, "getItem" | "setItem">;

function storageOrNull(): BoardStorage | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

/** One shape for the URL and the remembered copy: anything malformed is dropped, never guessed. */
export function sanitizeDashboardSearch(search: Record<string, unknown>): DashboardSearch {
  return {
    range:
      typeof search.range === "string" && durationMs(search.range) !== undefined
        ? search.range
        : undefined,
    layout: search.layout === "table" ? "table" : undefined,
    trouble: search.trouble === true || search.trouble === "true" ? true : undefined,
    volume: search.volume === false || search.volume === "false" ? false : undefined,
    groupBy: typeof search.groupBy === "string" ? search.groupBy : undefined,
  };
}

/** True when every parameter is absent — the clean `/dashboard` nothing needs to remember. */
export function isDefaultBoard(search: DashboardSearch): boolean {
  return (
    search.range === undefined &&
    search.layout === undefined &&
    search.trouble === undefined &&
    search.volume === undefined &&
    search.groupBy === undefined
  );
}

export function readDashboardSearch(
  storage: BoardStorage | null = storageOrNull(),
): DashboardSearch | undefined {
  try {
    const raw = storage?.getItem(STORAGE_KEY);
    if (raw == null) return undefined;
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== "object" || parsed === null) return undefined;
    const saved = sanitizeDashboardSearch(parsed as Record<string, unknown>);
    return isDefaultBoard(saved) ? undefined : saved;
  } catch {
    return undefined;
  }
}

export function writeDashboardSearch(
  search: DashboardSearch,
  storage: BoardStorage | null = storageOrNull(),
): void {
  try {
    storage?.setItem(STORAGE_KEY, JSON.stringify(search));
  } catch {
    // Storage can be full or switched off. Losing a board preference is not worth an error path.
  }
}
