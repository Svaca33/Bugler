import { getFormatLocale, getMessages } from "@/i18n/runtime";
import type { Release } from "@/lib/releases";

import type { VolumeWindow } from "./logVolume";

/**
 * The most markers a window may carry and still be labelled. Past this the versions collide into
 * each other and the axis under them, and a wall of unreadable text says less than a row of clean
 * ticks does — the detail is a hover away either way.
 */
export const MAX_LABELLED_MARKERS = 6;

/** Every Release that fell inside one Bucket, drawn as a single tick on it. */
export interface ReleaseMarker {
  /** The Bucket's start — the chart's own category value, so the tick lands on a bar. */
  bucket: string;
  releases: readonly Release[];
}

/**
 * Places Releases on the Buckets holding them. A Bucket is as precise as this chart gets — at a
 * day's width a marker is a day wide — so the instant itself stays for the hover, where it can be
 * written out rather than pointed at.
 *
 * Bucket edges sit on multiples of the common width measured from the epoch (ADR 0003), which is
 * what lets an instant be snapped arithmetically instead of searched for.
 */
export function releaseMarkers(
  releases: readonly Release[],
  buckets: readonly { start: string }[],
  window: VolumeWindow,
): ReleaseMarker[] {
  if (window.widthMs <= 0 || buckets.length === 0) return [];

  const byStart = new Map(buckets.map(bucket => [Date.parse(bucket.start), bucket.start]));
  const markers = new Map<string, Release[]>();

  for (const release of releases) {
    const at = Date.parse(release.at);
    if (Number.isNaN(at)) continue;
    const bucket = byStart.get(Math.floor(at / window.widthMs) * window.widthMs);
    // A Release outside the rendered window simply has nowhere to sit; the window is the answer
    // the reader asked for and is not stretched to reach it.
    if (bucket === undefined) continue;
    const held = markers.get(bucket);
    if (held === undefined) markers.set(bucket, [release]);
    else held.push(release);
  }

  return [...markers]
    .map(([bucket, held]) => ({
      bucket,
      releases: [...held].sort((a, b) => Date.parse(a.at) - Date.parse(b.at)),
    }))
    .sort((a, b) => Date.parse(a.bucket) - Date.parse(b.bucket));
}

/** The tick's label: the version itself while there is room for it, otherwise how many. */
export function markerLabel(marker: ReleaseMarker): string {
  const [first] = marker.releases;
  return marker.releases.length === 1 && first !== undefined
    ? first.version
    : getMessages().explore.volume.releasesCount(marker.releases.length);
}

/** What the marker says in full — one line per Release, service names resolved by the caller. */
export function describeMarker(
  marker: ReleaseMarker,
  serviceLabels: ReadonlyMap<string, string>,
): string {
  return marker.releases
    .map(release => {
      const service = serviceLabels.get(release.serviceId);
      const when = new Date(release.at).toLocaleString(getFormatLocale());
      return `${when} · ${service === undefined ? "" : `${service} · `}`
        + `${release.previousVersion} → ${release.version}`;
    })
    .join("\n");
}
