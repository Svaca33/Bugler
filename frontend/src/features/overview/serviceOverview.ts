import { getMessages } from "@/i18n/runtime";
import { durationMs } from "@/lib/duration";

/**
 * The Dashboard's judgment: joining the catalog with the two per-service aggregates, deciding
 * what state a Service is in, what its tile says, and what order the board reads in. Pure and
 * tested — the components only render what comes out of here.
 */

export type Facet = "app" | "namespace" | "environment" | "service";

export const ALL_FACETS: Facet[] = ["app", "namespace", "environment", "service"];

/**
 * Where a Service stands in the window: `open` (an Episode is open now), `recovered` (none open,
 * but at least one closed inside the window), `calm` (no Episode in the window at all). Named
 * apart from Alerting's states on purpose: "Quieted" is how one Episode closed, while these three
 * describe the Service — a Service whose Episodes were all Quieted is `recovered`, not quiet.
 */
export type ServiceStatus = "open" | "recovered" | "calm";

export interface VolumeBucket {
  start: string;
  error: number;
  warn: number;
  info: number;
  debug: number;
}

export interface ServiceVolume {
  serviceId: string;
  error: number;
  warn: number;
  info: number;
  debug: number;
  buckets: VolumeBucket[];
}

export interface VolumeByService {
  from: string;
  to: string;
  bucket: string;
  services: ServiceVolume[];
}

export interface EpisodeSummary {
  id: string;
  state: string;
  openedAt: string;
  closedAt: string | null;
  lastMatchAt: string;
  errorCount: number;
  warnCount: number;
  watch: string;
  firstMatchSeverity: number | null;
  firstMatchDetail: string | null;
}

export interface ServiceEpisodes {
  serviceId: string;
  open: number;
  quieted: number;
  solved: number;
  muted: number;
  latest: EpisodeSummary | null;
}

export interface CatalogEntry {
  serviceId: string;
  applicationId: string;
  applicationName: string;
  namespace: string;
  environment: string;
  name: string;
}

export interface OverviewInput {
  catalog: CatalogEntry[];
  /** Undefined while the aggregate is loading or failed — tiles then carry no volume at all. */
  volume: VolumeByService | undefined;
  /** Undefined while the aggregate is loading or failed — status falls back to `calm`. */
  episodes: EpisodesByService | undefined;
  groupBy: Facet[];
  troubleOnly: boolean;
}

export type EpisodesByService = { services: ServiceEpisodes[] };

export interface ServiceTile extends CatalogEntry {
  /** The facets the group heading does not already say; never repeats a word above it. */
  label: string;
  status: ServiceStatus;
  /** Present whenever the volume aggregate answered — a silent Service gets real zeros. */
  volume: ServiceVolume | undefined;
  /** Present whenever the episode aggregate answered — a calm Service gets real zeros. */
  episodes: ServiceEpisodes | undefined;
}

export interface OverviewGroup {
  key: string;
  services: ServiceTile[];
  /** Services with an open Episode — the group badge's "2 in trouble". */
  inTrouble: number;
  /** Services with only Quieted Episodes — the group badge's "1 quieted". */
  quieted: number;
}

export interface Overview {
  groups: OverviewGroup[];
  /** Tiles on the board after the trouble filter. */
  shown: number;
  /** Every Service the caller may see. */
  total: number;
}

/** "app,namespace,environment" from the URL → the known facets, in canonical chip order. */
export function parseGroupBy(value: string): Facet[] {
  const parts = value.split(",");
  return ALL_FACETS.filter(facet => parts.includes(facet));
}

/** "profilog · demo/prod · backend" — the facets asked for, namespace and environment collapsed. */
export function facetLabel(entry: CatalogEntry, facets: Facet[]): string {
  const parts: string[] = [];
  if (facets.includes("app")) parts.push(entry.applicationName);
  const namespace = facets.includes("namespace");
  const environment = facets.includes("environment");
  if (namespace && environment) parts.push(`${entry.namespace}/${entry.environment}`);
  else if (namespace) parts.push(entry.namespace);
  else if (environment) parts.push(entry.environment);
  if (facets.includes("service")) parts.push(entry.name);
  return parts.join(" · ");
}

export function serviceStatus(episodes: ServiceEpisodes | undefined): ServiceStatus {
  if (episodes === undefined) return "calm";
  if (episodes.open > 0) return "open";
  return episodes.quieted + episodes.solved + episodes.muted > 0 ? "recovered" : "calm";
}

/**
 * Where a tile sorts: open trouble first, quieted trouble second, the other recovered Services
 * (Solved or Muted in the window) third, calm last. Finer than ServiceStatus on purpose — a
 * quieted Service and one whose Episodes were solved both read `recovered`, but the quieted one
 * is the one nobody has looked at yet, so it must not hide among the all-clear cards.
 */
function tileRank(tile: ServiceTile): number {
  const episodes = tile.episodes;
  if (episodes === undefined) return 3;
  if (episodes.open > 0) return 0;
  if (episodes.quieted > 0) return 1;
  return episodes.solved + episodes.muted > 0 ? 2 : 3;
}

/** Inside the two trouble ranks, how much of that trouble there is decides the order. */
function rankCount(tile: ServiceTile): number {
  const episodes = tile.episodes;
  if (episodes === undefined) return 0;
  if (episodes.open > 0) return episodes.open;
  return episodes.quieted;
}

/**
 * Rank, then the rank's own Episode count, then error count descending, then label — always.
 * There is no sort control: a board that lets you bury an open Episode under an alphabetical
 * sort is not doing its job. A Service that recovered ten minutes ago outranks one that has
 * been silent all week; that is deliberate.
 */
export function compareTiles(a: ServiceTile, b: ServiceTile): number {
  const rank = tileRank(a) - tileRank(b);
  if (rank !== 0) return rank;
  const count = rankCount(b) - rankCount(a);
  if (count !== 0) return count;
  const errors = (b.volume?.error ?? 0) - (a.volume?.error ?? 0);
  if (errors !== 0) return errors;
  return a.label.localeCompare(b.label);
}

/** Groups read by the same judgment as their members: worst rank, most errors, then name. */
function compareGroups(a: OverviewGroup, b: OverviewGroup): number {
  // Sorted members mean the first tile carries the group's worst rank; a group is never empty.
  const first = a.services[0];
  const second = b.services[0];
  const rank = (first === undefined ? 3 : tileRank(first)) - (second === undefined ? 3 : tileRank(second));
  if (rank !== 0) return rank;
  const errors =
    b.services.reduce((sum, s) => sum + (s.volume?.error ?? 0), 0) -
    a.services.reduce((sum, s) => sum + (s.volume?.error ?? 0), 0);
  if (errors !== 0) return errors;
  return a.key.localeCompare(b.key);
}

/**
 * A Service the volume aggregate did not mention was silent — it renders as real zeros on the
 * same axis as everyone else, copied from any reported Service so the bar counts can never drift.
 */
function zeroBuckets(volume: VolumeByService): VolumeBucket[] {
  const template = volume.services[0]?.buckets;
  if (template !== undefined) {
    return template.map(b => ({ start: b.start, error: 0, warn: 0, info: 0, debug: 0 }));
  }

  // Nobody logged at all: synthesize the axis from the Resolved Window the response reports.
  const width = durationMs(volume.bucket);
  if (width === undefined) return [];
  const to = Date.parse(volume.to);
  const buckets: VolumeBucket[] = [];
  for (let start = Date.parse(volume.from); start < to && buckets.length < 2000; start += width) {
    buckets.push({ start: new Date(start).toISOString(), error: 0, warn: 0, info: 0, debug: 0 });
  }
  return buckets;
}

/**
 * The join, the grouping and the sort. The catalog is the spine: a Service missing from both
 * aggregates is a real Service that was silent, and it renders with zeros and `calm`. A serviceId
 * present in an aggregate but not the catalog is dropped — a Deletion that raced the poll.
 */
export function buildOverview(input: OverviewInput): Overview {
  const volumeBy = new Map(input.volume?.services.map(s => [s.serviceId, s]) ?? []);
  const episodesBy = new Map(input.episodes?.services.map(s => [s.serviceId, s]) ?? []);
  const complement = ALL_FACETS.filter(facet => !input.groupBy.includes(facet));

  const tiles = input.catalog.map((entry): ServiceTile => {
    const episodes =
      input.episodes === undefined
        ? undefined
        : episodesBy.get(entry.serviceId) ?? {
            serviceId: entry.serviceId, open: 0, quieted: 0, solved: 0, muted: 0, latest: null,
          };
    return {
      ...entry,
      label: facetLabel(entry, complement) || entry.name,
      status: serviceStatus(episodes),
      volume:
        input.volume === undefined
          ? undefined
          : volumeBy.get(entry.serviceId) ?? {
              serviceId: entry.serviceId, error: 0, warn: 0, info: 0, debug: 0,
              buckets: zeroBuckets(input.volume),
            },
      episodes,
    };
  });

  const shown = input.troubleOnly ? tiles.filter(tile => tile.status === "open") : tiles;

  const grouped = new Map<string, ServiceTile[]>();
  for (const tile of shown) {
    const key =
      input.groupBy.length === 0 ? getMessages().overview.allServices : facetLabel(tile, input.groupBy);
    const group = grouped.get(key);
    if (group === undefined) grouped.set(key, [tile]);
    else group.push(tile);
  }

  const groups = [...grouped.entries()]
    .map(([key, services]): OverviewGroup => {
      services.sort(compareTiles);
      return {
        key,
        services,
        inTrouble: services.filter(s => s.status === "open").length,
        quieted: services.filter(s => s.status !== "open" && (s.episodes?.quieted ?? 0) > 0).length,
      };
    })
    .sort(compareGroups);

  return { groups, shown: shown.length, total: tiles.length };
}
