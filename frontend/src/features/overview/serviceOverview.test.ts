import { describe, expect, test } from "bun:test";

import {
  buildOverview,
  facetLabel,
  type CatalogEntry,
  type EpisodesByService,
  type ServiceEpisodes,
  type VolumeByService,
} from "./serviceOverview";

const backend: CatalogEntry = {
  serviceId: "svc-backend", applicationId: "app-1", applicationName: "profilog",
  namespace: "demo", environment: "prod", name: "backend",
};
const mobile: CatalogEntry = {
  serviceId: "svc-mobile", applicationId: "app-1", applicationName: "profilog",
  namespace: "demo", environment: "prod", name: "mobile",
};
const staging: CatalogEntry = {
  serviceId: "svc-staging", applicationId: "app-1", applicationName: "profilog",
  namespace: "sebetov", environment: "stg", name: "backend",
};

function episodesOf(...rows: Partial<ServiceEpisodes>[]): EpisodesByService {
  return {
    services: rows.map(row => ({
      serviceId: "svc-backend", open: 0, quieted: 0, solved: 0, muted: 0, latest: null, ...row,
    })),
  };
}

function volumeOf(...rows: { serviceId: string; error: number }[]): VolumeByService {
  return {
    from: "2026-07-31T14:00:00Z",
    to: "2026-08-01T14:00:00Z",
    bucket: "PT1H",
    services: rows.map(row => ({
      serviceId: row.serviceId, error: row.error, warn: 0, info: 0, debug: 0,
      buckets: [
        { start: "2026-07-31T14:00:00Z", error: row.error, warn: 0, info: 0, debug: 0 },
        { start: "2026-07-31T15:00:00Z", error: 0, warn: 0, info: 0, debug: 0 },
      ],
    })),
  };
}

describe("the sort", () => {
  test("open outranks recovered outranks calm, whatever the error counts say", () => {
    const overview = buildOverview({
      catalog: [backend, mobile, staging],
      volume: volumeOf(
        { serviceId: "svc-backend", error: 999 },
        { serviceId: "svc-mobile", error: 5 },
        { serviceId: "svc-staging", error: 50 },
      ),
      episodes: episodesOf(
        { serviceId: "svc-mobile", open: 1 },
        { serviceId: "svc-staging", quieted: 2 },
      ),
      groupBy: [],
      troubleOnly: false,
    });

    const group = overview.groups[0]!;
    // mobile is open with 5 errors; backend's 999 errors cannot bury it.
    expect(group.services.map(s => s.serviceId))
      .toEqual(["svc-mobile", "svc-staging", "svc-backend"]);
    expect(group.services.map(s => s.status)).toEqual(["open", "recovered", "calm"]);
  });

  test("trouble counts order their own rank, and quieted outranks the other recovered", () => {
    const services: CatalogEntry[] = ["one", "two", "three", "four", "five", "six"].map(name => ({
      serviceId: `svc-${name}`, applicationId: "app-1", applicationName: "profilog",
      namespace: "demo", environment: "prod", name,
    }));

    const overview = buildOverview({
      catalog: services,
      volume: undefined,
      episodes: episodesOf(
        { serviceId: "svc-one", open: 1 },
        { serviceId: "svc-two", open: 3 },
        { serviceId: "svc-three", quieted: 1 },
        { serviceId: "svc-four", quieted: 5 },
        { serviceId: "svc-five", solved: 2 },
      ),
      groupBy: [],
      troubleOnly: false,
    });

    // Open by open count, quieted by quieted count, solved-recovered next, calm last.
    expect(overview.groups[0]!.services.map(s => s.name))
      .toEqual(["two", "one", "four", "three", "five", "six"]);
  });

  test("within a status errors decide, and the label settles a tie", () => {
    const overview = buildOverview({
      catalog: [mobile, backend, staging],
      volume: volumeOf(
        { serviceId: "svc-backend", error: 10 },
        { serviceId: "svc-mobile", error: 10 },
        { serviceId: "svc-staging", error: 40 },
      ),
      episodes: episodesOf(),
      groupBy: [],
      troubleOnly: false,
    });

    // staging's 40 errors first; backend and mobile tie on 10 and fall back to the label.
    expect(overview.groups[0]!.services.map(s => s.name)).toEqual(["backend", "backend", "mobile"]);
    expect(overview.groups[0]!.services[0]!.serviceId).toBe("svc-staging");
  });

  test("groups read by the same judgment as their members", () => {
    const overview = buildOverview({
      catalog: [backend, staging],
      volume: volumeOf({ serviceId: "svc-backend", error: 999 }),
      episodes: episodesOf({ serviceId: "svc-staging", open: 1 }),
      groupBy: ["namespace", "environment"],
      troubleOnly: false,
    });

    // sebetov/stg holds the open Episode, so its group heads the board despite fewer errors.
    expect(overview.groups.map(g => g.key)).toEqual(["sebetov/stg", "demo/prod"]);
    expect(overview.groups[0]!.inTrouble).toBe(1);
    expect(overview.groups[1]!.quieted).toBe(0);
  });
});

describe("headings and labels", () => {
  test("the tile says only what the heading does not", () => {
    const combos: [string[], string, string][] = [
      [["app", "namespace", "environment"], "profilog · demo/prod", "backend"],
      [[], "All services", "profilog · demo/prod · backend"],
      [["app"], "profilog", "demo/prod · backend"],
      [["service"], "backend", "profilog · demo/prod"],
    ];
    for (const [groupBy, heading, label] of combos) {
      const overview = buildOverview({
        catalog: [backend], volume: undefined, episodes: undefined,
        groupBy: groupBy as ("app" | "namespace" | "environment" | "service")[],
        troubleOnly: false,
      });
      expect(overview.groups[0]!.key).toBe(heading);
      expect(overview.groups[0]!.services[0]!.label).toBe(label);
    }
  });

  test("grouping by everything still leaves the tile its bare name", () => {
    const overview = buildOverview({
      catalog: [backend], volume: undefined, episodes: undefined,
      groupBy: ["app", "namespace", "environment", "service"], troubleOnly: false,
    });
    expect(overview.groups[0]!.services[0]!.label).toBe("backend");
  });

  test("a lone namespace or environment stands uncollapsed", () => {
    expect(facetLabel(backend, ["namespace"])).toBe("demo");
    expect(facetLabel(backend, ["app", "environment", "service"])).toBe("profilog · prod · backend");
  });
});

describe("the join", () => {
  test("a silent service survives with real zeros on the shared axis", () => {
    const overview = buildOverview({
      catalog: [backend, mobile],
      volume: volumeOf({ serviceId: "svc-backend", error: 3 }),
      episodes: episodesOf(),
      groupBy: [],
      troubleOnly: false,
    });

    const silent = overview.groups[0]!.services.find(s => s.serviceId === "svc-mobile")!;
    expect(silent.status).toBe("calm");
    expect(silent.volume!.error).toBe(0);
    // The axis is copied, never re-derived: same bar count, same edges.
    expect(silent.volume!.buckets.map(b => b.start))
      .toEqual(volumeOf({ serviceId: "x", error: 0 }).services[0]!.buckets.map(b => b.start));
  });

  test("an aggregate row without a catalog entry is a deletion that raced the poll", () => {
    const overview = buildOverview({
      catalog: [backend],
      volume: volumeOf({ serviceId: "svc-ghost", error: 7 }),
      episodes: episodesOf({ serviceId: "svc-ghost", open: 1 }),
      groupBy: [],
      troubleOnly: false,
    });

    expect(overview.groups[0]!.services.map(s => s.serviceId)).toEqual(["svc-backend"]);
  });

  test("a missing aggregate leaves the tile without it instead of faking zeros", () => {
    const overview = buildOverview({
      catalog: [backend], volume: undefined, episodes: undefined,
      groupBy: [], troubleOnly: false,
    });

    const tile = overview.groups[0]!.services[0]!;
    expect(tile.volume).toBeUndefined();
    expect(tile.episodes).toBeUndefined();
    expect(tile.status).toBe("calm");
  });

  test("the trouble filter keeps only open services and the counts say so", () => {
    const overview = buildOverview({
      catalog: [backend, mobile, staging],
      volume: undefined,
      episodes: episodesOf({ serviceId: "svc-mobile", open: 1 }, { serviceId: "svc-staging", quieted: 1 }),
      groupBy: [],
      troubleOnly: true,
    });

    expect(overview.shown).toBe(1);
    expect(overview.total).toBe(3);
    expect(overview.groups[0]!.services[0]!.serviceId).toBe("svc-mobile");
  });

  test("a quieted service is recovered, not quiet — the words stay apart", () => {
    const overview = buildOverview({
      catalog: [backend],
      volume: undefined,
      episodes: episodesOf({ serviceId: "svc-backend", quieted: 3 }),
      groupBy: [],
      troubleOnly: false,
    });

    expect(overview.groups[0]!.services[0]!.status).toBe("recovered");
    expect(overview.groups[0]!.quieted).toBe(1);
  });
});
