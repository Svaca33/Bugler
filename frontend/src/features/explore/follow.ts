/**
 * Follow: reading the log list as the end of the stream rather than as an answer to the Filter.
 *
 * The rules live here rather than in the component because they are the feature — how far the list
 * reads forward, how much of a burst it is willing to carry, and how much of itself it keeps. See
 * Exploration ADR 0004 for why none of it is ever announced on screen.
 */

import type { LogRecord } from "@/api/client";

/** How often Follow asks what has arrived. Below a reader's threshold for noticing a delay. */
export const FOLLOW_INTERVAL_MS = 2000;

/**
 * The most Log Records one tick carries. A Service emitting thousands a second would otherwise
 * hand the browser everything it stored; past this the newest are shown and the rest are passed
 * over in silence — a followed list does not claim to be complete.
 */
export const FOLLOW_PAGE = 100;

/** The most a followed list holds. What no longer fits falls off the oldest end. */
export const FOLLOW_CAP = 2000;

/** One row, to the pixel — which is what lets the scroll be corrected exactly rather than nearly. */
export const ROW_HEIGHT = 37;

/** Consecutive failed ticks after which Follow stops rather than looking like a quiet stream. */
export const FOLLOW_FAILURES = 3;

/** Where a followed list has read up to: the newest Log Record it holds. */
export interface FollowCursor {
  after: string;
  afterId: number;
}

/** The cursor for a list ordered newest first, or nothing when there is no record to read from. */
export function followCursor(items: readonly LogRecord[]): FollowCursor | undefined {
  const newest = items[0];
  return newest === undefined ? undefined : { after: newest.timestamp, afterId: Number(newest.id) };
}

/**
 * What arrived, laid on top of what was there. Both sides come newest first and everything incoming
 * is newer than everything held, so this concatenates rather than sorts; the de-duplication is for
 * the one tick that can overlap — the first one after Follow starts on an empty list, which has no
 * cursor to read forward from.
 */
export function mergeFollowed(
  existing: readonly LogRecord[],
  incoming: readonly LogRecord[],
): LogRecord[] {
  const seen = new Set(incoming.map(record => String(record.id)));
  const kept = existing.filter(record => !seen.has(String(record.id)));
  return [...incoming, ...kept].slice(0, FOLLOW_CAP);
}
