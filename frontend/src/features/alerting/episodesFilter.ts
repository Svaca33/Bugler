import type { Catalog, Episode } from "@/api/client";
import { getMessages } from "@/i18n/runtime";
import { durationMs } from "@/lib/duration";

export type EpisodeStateName = Episode["state"];

/**
 * The Episodes tab's rail filters, as they ride in the URL. Absent `lifecycle` means the default
 * trio; an explicit empty array means nothing is checked. Absent `opened` means the default
 * window; the literal "all" means no window at all.
 */
export interface EpisodesFilters {
  lifecycle?: EpisodeStateName[];
  ack?: "none" | "me";
  applicationId?: string;
  namespace?: string;
  environment?: string;
  service?: string;
  opened?: string;
  q?: string;
}

export const EPISODE_STATES: EpisodeStateName[] = ["Open", "Quieted", "Solved", "Muted"];

/** Muted stays off by default — a muted episode is not trouble anyone chose to hear about. */
export const DEFAULT_LIFECYCLE: EpisodeStateName[] = ["Open", "Quieted", "Solved"];

export const DEFAULT_OPENED = "P7D";

/** The sentinel that keeps "no window" distinguishable from "the default window" in the URL. */
export const OPENED_ALL = "all";

/** The OPENED windows on offer; their labels and phrases live in the catalog under these keys. */
export const OPENED_PRESETS = ["P1D", "P7D", "P30D"] as const;

export function effectiveLifecycle(filters: EpisodesFilters): EpisodeStateName[] {
  return filters.lifecycle ?? DEFAULT_LIFECYCLE;
}

export function effectiveOpened(filters: EpisodesFilters): string | undefined {
  const opened = filters.opened ?? DEFAULT_OPENED;
  return opened === OPENED_ALL ? undefined : opened;
}

/** The instant the OPENED window starts at — computed at call time, so the window rolls. */
export function openedFrom(filters: EpisodesFilters): string | undefined {
  const opened = effectiveOpened(filters);
  if (opened === undefined) return undefined;
  const length = durationMs(opened);
  return length === undefined ? undefined : new Date(Date.now() - length).toISOString();
}

const HOUR_MS = 3_600_000;

/**
 * The instant to ask for Releases from, so every listed Episode can name the version it opened on:
 * an hour before the oldest of them, which is as far back as "4 minutes after 1.2.3" ever reaches.
 * Floored to the hour, or every page loaded would key a new query for a few seconds' difference.
 *
 * Derived from the Episodes on screen rather than from the window filter, because "all" has no
 * window and a Service that has never redeployed has no Release inside one either — only the
 * version reported as running at the start of a bounded window says what it is on.
 */
export function releasesFrom(openedAt: readonly string[], filters: EpisodesFilters): string {
  const listed = openedAt.map(Date.parse).filter(instant => !Number.isNaN(instant));
  const windowStart = openedFrom(filters);
  const oldest = Math.min(
    ...listed,
    windowStart === undefined ? Date.now() : Date.parse(windowStart),
  );
  return new Date(Math.floor((oldest - HOUR_MS) / HOUR_MS) * HOUR_MS).toISOString();
}

/** "in the last 7 d" — the footer's window phrase; undefined when no window narrows. */
export function openedPhrase(filters: EpisodesFilters): string | undefined {
  const opened = effectiveOpened(filters);
  return opened === undefined ? undefined : getMessages().alerting.filters.openedPhrase[opened];
}

export function hasSourceFilter(filters: EpisodesFilters): boolean {
  return (
    filters.applicationId !== undefined
    || filters.namespace !== undefined
    || filters.environment !== undefined
    || filters.service !== undefined
  );
}

type CatalogApplication = Catalog["applications"][number];

/**
 * The Services the SOURCE facets address, as the ids the API filters by. Undefined when no facet
 * narrows; possibly empty, and empty means "nothing can match" — never "everything".
 */
export function resolveServiceIds(
  applications: readonly CatalogApplication[],
  filters: EpisodesFilters,
): string[] | undefined {
  if (!hasSourceFilter(filters)) return undefined;
  return applications
    .filter(a => filters.applicationId === undefined || a.id === filters.applicationId)
    .flatMap(a => a.services)
    .filter(s =>
      (filters.namespace === undefined || s.namespace === filters.namespace)
      && (filters.environment === undefined || s.environment === filters.environment)
      && (filters.service === undefined || s.name === filters.service))
    .map(s => s.id);
}

/** Rebuilding the default trio by hand keeps the URL clean: set equality, not order. */
export function normalizeLifecycle(states: EpisodeStateName[]): EpisodeStateName[] | undefined {
  const isDefault = states.length === DEFAULT_LIFECYCLE.length
    && DEFAULT_LIFECYCLE.every(state => states.includes(state));
  return isDefault ? undefined : states;
}

export function asLifecycle(value: unknown): EpisodeStateName[] | undefined {
  if (!Array.isArray(value)) return undefined;
  return value.filter((entry): entry is EpisodeStateName =>
    EPISODE_STATES.includes(entry as EpisodeStateName));
}

export function asOpened(value: unknown): string | undefined {
  return value === OPENED_ALL || OPENED_PRESETS.some(preset => preset === value)
    ? (value as string)
    : undefined;
}

export function asAck(value: unknown): "none" | "me" | undefined {
  return value === "none" || value === "me" ? value : undefined;
}
