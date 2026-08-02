import type { paths } from "@/api/schema";

export type ReleasesResponse =
  NonNullable<paths["/api/releases"]["get"]["responses"]["200"]["content"]["application/json"]>;

export type Release = ReleasesResponse["releases"][number];

/**
 * How recently a Release must have happened for an Episode to be worth reading beside it. Beyond
 * this the version still shows — it is a fact whenever it is known — but "4 minutes after 1.2.3"
 * turns into "6 days after 1.2.3", which claims a connection nobody would draw.
 */
export const RECENT_RELEASE_MS = 60 * 60 * 1000;

/** The Declared Version a Service was running at some instant, and what put it there. */
export interface VersionAtInstant {
  version: string;
  /**
   * How long before the instant the Release happened, in milliseconds — present only when it was
   * a Release (rather than the version Bugler already knew) and recent enough to mean something.
   */
  releasedMsBefore?: number;
}

/**
 * The Releases of one window arranged for lookup: per Service, what it was running as the window
 * opened followed by every change inside it, oldest first.
 */
export type ReleaseTimelines = ReadonlyMap<string, readonly TimelineEntry[]>;

interface TimelineEntry {
  at: number;
  version: string;
  /** False for the entry the window opened on: it says what was running, not that anything changed. */
  released: boolean;
}

export function buildTimelines(payload: ReleasesResponse | undefined): ReleaseTimelines {
  const timelines = new Map<string, TimelineEntry[]>();
  if (payload === undefined) return timelines;

  const push = (serviceId: string, entry: TimelineEntry) => {
    const entries = timelines.get(serviceId);
    if (entries === undefined) timelines.set(serviceId, [entry]);
    else entries.push(entry);
  };

  for (const running of payload.running) {
    push(running.serviceId, {
      at: Date.parse(running.since),
      version: running.version,
      released: false,
    });
  }

  for (const release of payload.releases) {
    push(release.serviceId, {
      at: Date.parse(release.at),
      version: release.version,
      released: true,
    });
  }

  // The server orders each half, but the two are merged here — and a Release stamped on a sender's
  // clock can land before the version the window opened on if that clock runs behind.
  for (const entries of timelines.values()) entries.sort((a, b) => a.at - b.at);
  return timelines;
}

/**
 * What a Service was running at an instant: the last entry at or before it. Undefined when nothing
 * is known — the Service declares no version, or its history begins after the instant asked about.
 */
export function versionAt(
  timelines: ReleaseTimelines,
  serviceId: string,
  instant: string | number,
): VersionAtInstant | undefined {
  const entries = timelines.get(serviceId);
  if (entries === undefined) return undefined;

  const at = typeof instant === "number" ? instant : Date.parse(instant);
  let found: TimelineEntry | undefined;
  for (const entry of entries) {
    if (entry.at > at) break;
    found = entry;
  }

  if (found === undefined) return undefined;
  const since = at - found.at;
  return found.released && since <= RECENT_RELEASE_MS
    ? { version: found.version, releasedMsBefore: since }
    : { version: found.version };
}
