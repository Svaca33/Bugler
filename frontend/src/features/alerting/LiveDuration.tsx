import { useSyncExternalStore } from "react";

import { describeLiveMillis } from "./format";

// One page-wide clock: however many durations tick, there is a single interval, and every
// subscriber re-renders on the same beat. The interval dies with its last subscriber.
const listeners = new Set<() => void>();
let now = Date.now();
let timer: ReturnType<typeof setInterval> | undefined;

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  if (timer === undefined) {
    now = Date.now();
    timer = setInterval(() => {
      now = Date.now();
      for (const notify of listeners) notify();
    }, 1000);
  }
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0 && timer !== undefined) {
      clearInterval(timer);
      timer = undefined;
    }
  };
}

/** The shared 1 s tick. Subscribing components re-render once per second — keep them leaf-sized. */
export function useNow(): number {
  return useSyncExternalStore(subscribe, () => now, () => now);
}

/** A span since `since` that advances without a refetch. */
export function LiveDuration(props: { since: string; className?: string }) {
  const tick = useNow();
  return <span className={props.className}>{describeLiveMillis(tick - Date.parse(props.since))}</span>;
}
