import { useEffect, useRef, useState } from "react";

import type { LogRecord } from "@/api/client";

import {
  FOLLOW_FAILURES,
  FOLLOW_INTERVAL_MS,
  followCursor,
  mergeFollowed,
  type FollowCursor,
} from "./follow";

/** What a followed list holds, and what the last tick did to it. */
export interface FollowState {
  items: LogRecord[];
  /** How many records the last tick put on top — the exact distance the scroll has to travel back. */
  added: number;
  /** Rises once per tick that brought something. Anything wanting to move with the stream reads it. */
  pulse: number;
}

const IDLE: FollowState = { items: [], added: 0, pulse: 0 };

/**
 * Drives a followed list: seeds it from what is already on screen, then reads forward from its
 * newest record every interval.
 *
 * A tick that fails is retried rather than reported, but only so many times: a Follow that has
 * quietly stopped working looks exactly like a Follow nothing is arriving into, and that is the one
 * confusion worth spending a mode on. Giving up hands the list back to the ordinary query, which
 * reports failure the way every other query in Bugler does.
 */
export function useFollow(props: {
  enabled: boolean;
  /** Identifies what is being followed; a new one starts over rather than mixing two Filters. */
  key: string;
  /** What to start from — read once, when Follow starts. */
  seed: () => LogRecord[];
  fetchNewer: (cursor: FollowCursor | undefined, signal: AbortSignal) => Promise<LogRecord[]>;
  onGiveUp: () => void;
}): FollowState {
  const { enabled, key } = props;
  const [state, setState] = useState<FollowState>(IDLE);

  // The callbacks are rebuilt on every render of the page; the loop must not be.
  const latest = useRef(props);
  latest.current = props;

  useEffect(() => {
    if (!enabled) {
      setState(IDLE);
      return;
    }

    const controller = new AbortController();
    const seeded = latest.current.seed();
    let cursor = followCursor(seeded);
    let failures = 0;
    let timer: ReturnType<typeof setTimeout> | undefined;
    setState({ items: seeded, added: 0, pulse: 0 });

    const schedule = () => {
      timer = setTimeout(() => void tick(), FOLLOW_INTERVAL_MS);
    };

    const tick = async () => {
      // A hidden tab is throttled to a tick a minute anyway, so it pauses cleanly instead of
      // dribbling; coming back runs one immediately.
      if (document.hidden) {
        schedule();
        return;
      }

      try {
        const incoming = await latest.current.fetchNewer(cursor, controller.signal);
        if (controller.signal.aborted) return;
        failures = 0;
        if (incoming.length > 0) {
          cursor = followCursor(incoming) ?? cursor;
          setState(previous => ({
            items: mergeFollowed(previous.items, incoming),
            added: incoming.length,
            pulse: previous.pulse + 1,
          }));
        }
      } catch {
        if (controller.signal.aborted) return;
        failures += 1;
        if (failures >= FOLLOW_FAILURES) {
          latest.current.onGiveUp();
          return;
        }
      }

      schedule();
    };

    const onVisible = () => {
      if (document.hidden || controller.signal.aborted) return;
      clearTimeout(timer);
      void tick();
    };

    document.addEventListener("visibilitychange", onVisible);
    schedule();

    return () => {
      controller.abort();
      clearTimeout(timer);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [enabled, key]);

  return state;
}
