import { describe, expect, test } from "bun:test";

import type { Release } from "@/lib/releases";

import type { VolumeWindow } from "./logVolume";
import { markerLabel, releaseMarkers } from "./releaseMarkers";

const SERVICE = "11111111-1111-1111-1111-111111111111";
const NOON = Date.parse("2026-08-02T12:00:00.000Z");
const HOUR = 3_600_000;

/** Four hourly Buckets from noon, as the server would send them. */
const BUCKETS = [0, 1, 2, 3].map(n => ({ start: new Date(NOON + n * HOUR).toISOString() }));

const WINDOW: VolumeWindow = {
  from: BUCKETS[0]!.start,
  to: new Date(NOON + 4 * HOUR).toISOString(),
  now: new Date(NOON + 4 * HOUR).toISOString(),
  widthMs: HOUR,
};

function release(offsetMs: number, version: string): Release {
  return {
    serviceId: SERVICE,
    version,
    previousVersion: "1.0.0",
    at: new Date(NOON + offsetMs).toISOString(),
  };
}

describe("releaseMarkers", () => {
  test("a release sits on the bucket holding it, not the one nearest to it", () => {
    const markers = releaseMarkers([release(HOUR + 59 * 60_000, "1.2.3")], BUCKETS, WINDOW);

    expect(markers).toHaveLength(1);
    expect(markers[0]!.bucket).toBe(BUCKETS[1]!.start);
  });

  test("a release exactly on a bucket edge belongs to the bucket it opens", () => {
    const markers = releaseMarkers([release(2 * HOUR, "1.2.3")], BUCKETS, WINDOW);

    expect(markers[0]!.bucket).toBe(BUCKETS[2]!.start);
  });

  test("releases in one bucket collapse into one marker, oldest first", () => {
    const markers = releaseMarkers(
      [release(50 * 60_000, "1.2.5"), release(10 * 60_000, "1.2.4")],
      BUCKETS,
      WINDOW,
    );

    expect(markers).toHaveLength(1);
    expect(markers[0]!.releases.map(r => r.version)).toEqual(["1.2.4", "1.2.5"]);
  });

  test("markers come back oldest first whatever order the releases arrived in", () => {
    const markers = releaseMarkers(
      [release(3 * HOUR, "1.2.6"), release(0, "1.2.4"), release(2 * HOUR, "1.2.5")],
      BUCKETS,
      WINDOW,
    );

    expect(markers.map(m => m.bucket)).toEqual([
      BUCKETS[0]!.start,
      BUCKETS[2]!.start,
      BUCKETS[3]!.start,
    ]);
  });

  test("a release outside the rendered window has nowhere to sit", () => {
    expect(releaseMarkers([release(-HOUR, "1.2.3")], BUCKETS, WINDOW)).toEqual([]);
    expect(releaseMarkers([release(9 * HOUR, "1.2.3")], BUCKETS, WINDOW)).toEqual([]);
  });

  test("nothing is placed without buckets to place it on", () => {
    expect(releaseMarkers([release(0, "1.2.3")], [], WINDOW)).toEqual([]);
    expect(releaseMarkers([release(0, "1.2.3")], BUCKETS, { ...WINDOW, widthMs: 0 })).toEqual([]);
  });
});

describe("markerLabel", () => {
  test("one release reads as its version", () => {
    const markers = releaseMarkers([release(0, "1.2.3")], BUCKETS, WINDOW);

    expect(markerLabel(markers[0]!)).toBe("1.2.3");
  });

  test("several in one bucket read as how many", () => {
    const markers = releaseMarkers(
      [release(0, "1.2.3"), release(60_000, "1.2.4")],
      BUCKETS,
      WINDOW,
    );

    expect(markerLabel(markers[0]!)).toBe("2 releases");
  });
});
