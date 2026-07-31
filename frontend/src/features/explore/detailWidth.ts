/**
 * How wide the reader wants the detail panel. Logs and Traces deliberately share one key: the
 * width answers "how much room do I want for a detail", which is not a per-page question.
 *
 * It is kept in pixels rather than as a fraction of the group because the two pages have different
 * amounts of room — Logs spend 254px on the filter rail, the trace waterfall spends none — so the
 * same fraction would restore two different widths.
 *
 * This is a reading preference, not shareable state, so it stays in the browser instead of riding
 * in the URL the way the open record does.
 */
const STORAGE_KEY = "bugler.explore.detail-width";

/** What the panel has always been, and what a double-click on the divider goes back to. */
export const DEFAULT_DETAIL_WIDTH = 384;

/** Below this the two-column `Time / Severity / Service` grid starts breaking words. */
export const MIN_DETAIL_WIDTH = 320;

/**
 * The list keeps at least this much. Its grid needs ~640px before MESSAGE collapses, but the whole
 * arrangement then stops fitting a narrow window — and a clipped message costs nothing while the
 * open detail is showing it in full right next door.
 */
export const MIN_LIST_WIDTH = 420;

type WidthStorage = Pick<Storage, "getItem" | "setItem">;

function storageOrNull(): WidthStorage | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

export function readDetailWidth(storage: WidthStorage | null = storageOrNull()): number {
  let raw: string | null = null;
  try {
    raw = storage?.getItem(STORAGE_KEY) ?? null;
  } catch {
    return DEFAULT_DETAIL_WIDTH;
  }
  if (raw === null) return DEFAULT_DETAIL_WIDTH;
  const width = Number(raw);
  // A stored width narrower than the minimum could only come from a hand-edited or stale entry;
  // there is no upper bound to check because the ceiling is whatever the list can spare today.
  return Number.isFinite(width) && width >= MIN_DETAIL_WIDTH ? width : DEFAULT_DETAIL_WIDTH;
}

export function writeDetailWidth(
  width: number,
  storage: WidthStorage | null = storageOrNull(),
): void {
  try {
    storage?.setItem(STORAGE_KEY, String(Math.round(width)));
  } catch {
    // Storage can be full or switched off. Losing a width preference is not worth an error path.
  }
}
