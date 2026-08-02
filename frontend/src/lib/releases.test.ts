import { describe, expect, test } from "bun:test";

import { buildTimelines, versionAt, type ReleasesResponse } from "./releases";

const BACKEND = "11111111-1111-1111-1111-111111111111";
const MOBILE = "22222222-2222-2222-2222-222222222222";
const NOON = "2026-08-02T12:00:00.000Z";

function at(minutes: number): string {
  return new Date(Date.parse(NOON) + minutes * 60_000).toISOString();
}

function payload(partial: Partial<ReleasesResponse>): ReleasesResponse {
  return { from: null, to: null, now: NOON, running: [], releases: [], ...partial };
}

describe("versionAt", () => {
  test("reads the version the window opened on when nothing changed since", () => {
    const timelines = buildTimelines(payload({
      running: [{ serviceId: BACKEND, version: "1.2.3", since: at(-600) }],
    }));

    expect(versionAt(timelines, BACKEND, at(0))).toEqual({ version: "1.2.3" });
  });

  test("a release inside the window takes over from the instant it happened", () => {
    const timelines = buildTimelines(payload({
      running: [{ serviceId: BACKEND, version: "1.2.3", since: at(-600) }],
      releases: [
        { serviceId: BACKEND, version: "1.2.4", previousVersion: "1.2.3", at: at(-30) },
      ],
    }));

    expect(versionAt(timelines, BACKEND, at(-31))?.version).toBe("1.2.3");
    expect(versionAt(timelines, BACKEND, at(-29))?.version).toBe("1.2.4");
  });

  test("a release within the hour is reported with how long before", () => {
    const timelines = buildTimelines(payload({
      releases: [
        { serviceId: BACKEND, version: "1.2.4", previousVersion: "1.2.3", at: at(-4) },
      ],
    }));

    expect(versionAt(timelines, BACKEND, at(0))).toEqual({
      version: "1.2.4",
      releasedMsBefore: 4 * 60_000,
    });
  });

  test("an older release still names the version but claims no connection", () => {
    const timelines = buildTimelines(payload({
      releases: [
        { serviceId: BACKEND, version: "1.2.4", previousVersion: "1.2.3", at: at(-61) },
      ],
    }));

    expect(versionAt(timelines, BACKEND, at(0))).toEqual({ version: "1.2.4" });
  });

  test("the version the window opened on is never read as a release, however recent", () => {
    const timelines = buildTimelines(payload({
      running: [{ serviceId: BACKEND, version: "1.2.3", since: at(-2) }],
    }));

    expect(versionAt(timelines, BACKEND, at(0))).toEqual({ version: "1.2.3" });
  });

  test("nothing is claimed about a service that declares no version", () => {
    const timelines = buildTimelines(payload({
      running: [{ serviceId: BACKEND, version: "1.2.3", since: at(-600) }],
    }));

    expect(versionAt(timelines, MOBILE, at(0))).toBeUndefined();
  });

  test("nothing is claimed before a service's history begins", () => {
    const timelines = buildTimelines(payload({
      releases: [
        { serviceId: BACKEND, version: "1.2.4", previousVersion: "1.2.3", at: at(-10) },
      ],
    }));

    expect(versionAt(timelines, BACKEND, at(-20))).toBeUndefined();
  });

  test("services are kept apart", () => {
    const timelines = buildTimelines(payload({
      running: [
        { serviceId: BACKEND, version: "1.2.3", since: at(-600) },
        { serviceId: MOBILE, version: "9.0.0", since: at(-600) },
      ],
      releases: [
        { serviceId: MOBILE, version: "9.1.0", previousVersion: "9.0.0", at: at(-5) },
      ],
    }));

    expect(versionAt(timelines, BACKEND, at(0))).toEqual({ version: "1.2.3" });
    expect(versionAt(timelines, MOBILE, at(0))?.version).toBe("9.1.0");
  });

  test("consecutive releases resolve to the last one at or before the instant", () => {
    const timelines = buildTimelines(payload({
      releases: [
        { serviceId: BACKEND, version: "1.2.4", previousVersion: "1.2.3", at: at(-40) },
        { serviceId: BACKEND, version: "1.2.5", previousVersion: "1.2.4", at: at(-20) },
        { serviceId: BACKEND, version: "1.2.6", previousVersion: "1.2.5", at: at(-3) },
      ],
    }));

    expect(versionAt(timelines, BACKEND, at(-30))?.version).toBe("1.2.4");
    expect(versionAt(timelines, BACKEND, at(0))).toEqual({
      version: "1.2.6",
      releasedMsBefore: 3 * 60_000,
    });
  });

  test("an empty payload claims nothing at all", () => {
    expect(versionAt(buildTimelines(undefined), BACKEND, at(0))).toBeUndefined();
    expect(versionAt(buildTimelines(payload({})), BACKEND, at(0))).toBeUndefined();
  });
});
