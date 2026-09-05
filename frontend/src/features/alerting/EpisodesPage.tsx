import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useState, type ReactNode } from "react";

import { api, type Episode } from "@/api/client";
import { useCatalog, useCurrentUser, useReleases } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { useT } from "@/i18n";
import { MIN_LIST_WIDTH } from "@/lib/detailWidth";
import { describeMillis } from "@/lib/duration";
import { LiveDuration } from "@/lib/LiveDuration";
import { buildTimelines, type ReleaseTimelines } from "@/lib/releases";
import { episodeRailClass } from "@/lib/severity";

import {
  effectiveLifecycle,
  hasSourceFilter,
  openedFrom,
  openedPhrase,
  releasesFrom,
  resolveServiceIds,
  type EpisodesFilters,
} from "./episodesFilter";
import { EpisodesFilterRail } from "./EpisodesFilterRail";
import { EpisodeDetailPanel } from "./EpisodeDetailPanel";
import { clock, dayLabel, historyStamp } from "./format";
import { HealthCheckBadge } from "./HealthCheckBadge";
import { MachineHandBadges } from "./MachineHandBadges";
import { OpenNowBand } from "./OpenNowBand";
import { QuietWindowBadge } from "./QuietWindowBadge";
import { GroupingMarks } from "./GroupingMarks";
import { Participants } from "./Participants";
import { indexServices, type KnownService } from "./serviceIndex";
import { StateBadge } from "./StateBadge";

const PAGE_SIZE = 100;
const HISTORY_PAGE = 25;
const REFETCH_MS = 30_000;

const GRID = "grid grid-cols-[3px_1fr_128px_86px] items-center gap-3.5 px-5";

/** The Episodes within the viewer's Visibility Scope: the burning ones on top, history below. */
export function EpisodesPage(props: {
  filters: EpisodesFilters;
  onChange: (filters: EpisodesFilters) => void;
  selectedId: string | undefined;
  onSelect: (id: string | undefined) => void;
}) {
  const { filters, onChange, selectedId, onSelect } = props;
  const t = useT();
  const catalog = useCatalog();
  const currentUser = useCurrentUser();
  const navigate = useNavigate();

  const lifecycle = effectiveLifecycle(filters);
  const applications = catalog.data?.applications ?? [];
  const sourceActive = hasSourceFilter(filters);
  const serviceIds = catalog.data === undefined ? undefined : resolveServiceIds(applications, filters);
  // A source filter that matches no registered service matches no episodes: the queries stay
  // idle rather than asking the server for "everything".
  const sourceEmpty = sourceActive && catalog.data !== undefined && serviceIds?.length === 0;
  const ready = !sourceActive || (catalog.data !== undefined && !sourceEmpty);

  const shared = {
    serviceId: serviceIds !== undefined && serviceIds.length > 0 ? serviceIds : undefined,
    q: filters.q,
    acknowledged: filters.ack,
  };

  const episodes = useInfiniteQuery({
    queryKey: ["alerts", "episodes", filters],
    queryFn: async ({ pageParam }) => {
      // One row per kind of trouble, faced by its latest episode; every filter — the shared
      // ones here and the lifecycle above — judges the face, so a group shows or hides whole.
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: {
            latestPerFingerprint: true,
            // The filed-away ones are out of the everyday view until the rail asks for them;
            // the face of a kind is still chosen over all of its Episodes, so a filed face
            // takes its kind out of the list rather than handing the row to its history.
            includeArchived: filters.archived,
            limit: PAGE_SIZE,
            beforeId: pageParam,
            state: lifecycle,
            from: openedFrom(filters),
            ...shared,
          },
        },
      });
      if (error !== undefined) throw new Error(t.alerting.errors.loadEpisodes);
      return data;
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: lastPage =>
      lastPage.items.length === PAGE_SIZE ? lastPage.items.at(-1)!.id : undefined,
    refetchInterval: REFETCH_MS,
    enabled: ready && lifecycle.length > 0,
  });

  // The band's own query: the lifecycle boxes must never hide what is burning, so only the
  // non-lifecycle filters key and narrow it.
  const nonLifecycle = { ...filters, lifecycle: undefined };
  const band = useQuery({
    queryKey: ["alerts", "open-band", nonLifecycle],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: { state: ["Open"], limit: 100, from: openedFrom(filters), ...shared },
        },
      });
      if (error !== undefined) throw new Error(t.alerting.errors.loadOpenEpisodes);
      return data.items;
    },
    refetchInterval: REFETCH_MS,
    enabled: ready,
  });

  const counts = useQuery({
    queryKey: ["alerts", "counts", nonLifecycle],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes/counts", {
        params: {
          query: {
            latestPerFingerprint: true,
            includeArchived: filters.archived,
            from: openedFrom(filters),
            ...shared,
          },
        },
      });
      if (error !== undefined) throw new Error(t.alerting.errors.countEpisodes);
      return data;
    },
    refetchInterval: REFETCH_MS,
    enabled: ready,
  });

  const services = indexServices(applications);
  const items = episodes.data?.pages.flatMap(page => page.items) ?? [];
  const myName = currentUser.data?.displayName ?? currentUser.data?.email;

  // Joined in the browser rather than server-side: Releases are Ingestion's and reach the UI
  // through Exploration, while Episodes are Alerting's, and no endpoint answers for both
  // (ADR 0013). The two arrive apart and meet here, on service id and instant.
  const releases = useReleases({
    applicationId: filters.applicationId,
    namespace: filters.namespace,
    environment: filters.environment,
    service: filters.service,
    from: releasesFrom(
      [...items, ...(band.data ?? [])].map(episode => episode.openedAt),
      filters,
    ),
  }, { enabled: ready });
  const timelines = buildTimelines(releases.data);

  const openLogs = (episode: Episode) => {
    const known = episode.openedByServiceId === null
      ? undefined
      : services.get(episode.openedByServiceId);
    // The window starts a little before the opening: the first log's own timestamp predates
    // the detection that opened the episode, and the list must not hide the panel's subject.
    const windowStart = new Date(Date.parse(episode.openedAt) - 5 * 60_000).toISOString();
    navigate({
      to: "/",
      search: {
        applicationId: known?.application.id,
        namespace: known?.facets.namespace,
        environment: known?.facets.environment,
        service: known?.facets.name,
        severityMin: 13,
        from: windowStart,
        // Only a watch that deals in log records has one to anchor on.
        log: episode.firstMatchLogId === null ? undefined : Number(episode.firstMatchLogId),
      },
    });
  };

  const stateKey = { Open: "open", Quieted: "quieted", Solved: "solved", Muted: "muted" } as const;
  const total = counts.data === undefined
    ? undefined
    : lifecycle.reduce((sum, state) => sum + Number(counts.data[stateKey[state]]), 0);
  const windowPhrase = openedPhrase(filters);
  const counted = total === undefined
    ? t.alerting.list.loadedCount(items.length)
    : t.alerting.list.countOfTotal(items.length, total, windowPhrase);

  const narrowed = [
    filters.lifecycle, filters.ack, filters.archived, filters.applicationId, filters.namespace,
    filters.environment, filters.service, filters.opened, filters.q,
  ].some(value => value !== undefined);

  return (
    <div className="flex h-full min-h-0">
      <EpisodesFilterRail filters={filters} counts={counts.data} onChange={onChange} />

      <ResizablePanelGroup className="min-w-0 flex-1">
        <ResizablePanel
          id="list"
          minSize={`${MIN_LIST_WIDTH}px`}
          className="flex h-full min-w-0 flex-col"
        >
          <OpenNowBand
            episodes={band.data ?? []}
            services={services}
            refreshedAt={band.dataUpdatedAt === 0 ? undefined : band.dataUpdatedAt}
            onSelect={onSelect}
          />

          <div className="min-h-0 flex-1 overflow-auto">
            <div
              className={`${GRID} sticky top-0 z-10 h-7 border-b border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
            >
              <span />
              <span>{t.alerting.list.columnEpisode}</span>
              <span>{t.alerting.list.columnState}</span>
              <span className="text-right">{t.alerting.list.columnDuration}</span>
            </div>
            <div data-testid="episode-rows">
              <EpisodeRows
                items={items}
                services={services}
                selectedId={selectedId}
                myName={myName}
                onSelect={onSelect}
              />
              {items.length === 0 && !episodes.isLoading && (
                <p className="py-16 text-center text-[#8CA1B8]">
                  {narrowed ? t.alerting.list.emptyFiltered : t.alerting.list.emptyNoTrouble}
                </p>
              )}
            </div>
          </div>

          <div className="flex items-center gap-3 border-t border-[#17293D] px-5 py-[9px]">
            <Button
              variant="outline"
              size="sm"
              disabled={!episodes.hasNextPage || episodes.isFetchingNextPage}
              onClick={() => episodes.fetchNextPage()}
            >
              {episodes.isFetchingNextPage
                ? t.common.loading
                : episodes.hasNextPage
                  ? t.alerting.list.loadOlder
                  : t.alerting.list.nothingOlder}
            </Button>
            <span className="font-mono text-[11px] whitespace-nowrap text-[#6E86A0]">{counted}</span>
          </div>
        </ResizablePanel>

        {selectedId !== undefined && (
          <EpisodeDetailPanel
            id={selectedId}
            fromList={items.find(e => e.id === selectedId) ?? band.data?.find(e => e.id === selectedId)}
            services={services}
            timelines={timelines}
            onClose={() => onSelect(undefined)}
            onOpenLogs={openLogs}
            onSelectEpisode={onSelect}
          />
        )}
      </ResizablePanelGroup>
    </div>
  );
}

/** One kind of trouble per row — its latest episode — with one separator per calendar day. */
function EpisodeRows(props: {
  items: Episode[];
  services: Map<string, KnownService>;
  selectedId: string | undefined;
  myName: string | undefined;
  onSelect: (id: string) => void;
}) {
  // Day labels only move at midnight — a render-time clock is enough, no ticking here. The only
  // things that tick per second are the LiveDuration leaves inside the open rows.
  const now = Date.now();

  // The unfolded kinds of trouble. Keyed by the group, not the face, so a fold survives the
  // refetch that replaces the face with a newer episode.
  const [unfolded, setUnfolded] = useState<ReadonlySet<string>>(new Set());
  const toggle = (key: string) =>
    setUnfolded(previous => {
      const next = new Set(previous);
      if (!next.delete(key)) next.add(key);
      return next;
    });

  const dayCounts = new Map<string, number>();
  for (const episode of props.items) {
    const day = new Date(episode.openedAt).toDateString();
    dayCounts.set(day, (dayCounts.get(day) ?? 0) + 1);
  }

  const rows: ReactNode[] = [];
  let previousDay: string | undefined;
  for (const episode of props.items) {
    const day = new Date(episode.openedAt).toDateString();
    if (day !== previousDay) {
      previousDay = day;
      rows.push(
        <div
          key={`day-${day}`}
          className="flex h-[26px] items-center gap-2.5 bg-background px-5 font-mono text-[10px] tracking-[0.12em] text-[#5F7590]"
        >
          <span className="whitespace-nowrap">{dayLabel(episode.openedAt, now)}</span>
          <span className="h-px flex-1 bg-[#101F31]" />
          <span className="text-[#6E86A0]">{dayCounts.get(day)}</span>
        </div>,
      );
    }
    // One kind of trouble in one Episode Scope (ADR 0034), not one Service's any more.
    const groupKey = `${episode.scopeKey}\u0000${episode.fingerprint}`;
    const expanded = unfolded.has(groupKey);
    rows.push(
      <EpisodeRow
        key={episode.id}
        episode={episode}
        services={props.services}
        selected={episode.id === props.selectedId}
        myName={props.myName}
        expanded={expanded}
        onToggleHistory={Number(episode.priorCount) > 0 ? () => toggle(groupKey) : undefined}
        onSelect={props.onSelect}
      />,
    );
    if (expanded) {
      rows.push(
        <GroupHistory
          key={`history-${groupKey}`}
          face={episode}
          selectedId={props.selectedId}
          onSelect={props.onSelect}
        />,
      );
    }
  }

  return rows;
}

function EpisodeRow(props: {
  episode: Episode;
  services: Map<string, KnownService>;
  selected: boolean;
  myName: string | undefined;
  expanded: boolean;
  onToggleHistory: (() => void) | undefined;
  onSelect: (id: string) => void;
}) {
  const { episode, selected } = props;
  const t = useT();
  const muted = episode.state === "Muted";
  const open = episode.state === "Open";
  const heldByMe = episode.acknowledgedBy !== null && episode.acknowledgedBy === props.myName;
  const priorCount = Number(episode.priorCount);

  // A fresh, unclaimed episode still tells who sat on the earlier one of its kind — Solve wipes
  // those marks, so whatever this names is unresolved work someone believed they were on.
  const owner = episode.acknowledgedBy !== null
    ? heldByMe ? t.alerting.list.you : episode.acknowledgedBy
    : episode.solvedBy !== null
      ? t.alerting.list.solvedBy(episode.solvedBy)
      : episode.earlierAcknowledgedBy !== null
        ? t.alerting.list.earlierAck(
          episode.earlierAcknowledgedBy === props.myName
            ? t.alerting.list.you
            : episode.earlierAcknowledgedBy,
        )
        : undefined;

  // The recurrence badge doubles as the fold: the count is the reason the history exists, so it
  // is also the handle to it. Groups with nothing earlier get no handle at all.
  const historyToggle = props.onToggleHistory !== undefined && (
    <button
      type="button"
      className="cursor-pointer rounded-sm border border-[#2C4159] px-[5px] text-[#A9BDD1] hover:border-[#44607F] hover:text-[#DCE8F3]"
      title={props.expanded
        ? t.alerting.list.hideEarlier
        : t.alerting.list.showEarlier(priorCount)}
      onClick={event => {
        event.stopPropagation();
        props.onToggleHistory?.();
      }}
    >
      ×{priorCount + 1} {props.expanded ? "▴" : "▾"}
    </button>
  );

  return (
    <div
      data-testid="episode-row"
      className={`${GRID} cursor-pointer border-b border-[#101F31] py-[9px] ${
        selected ? "bg-[#12243A] shadow-[inset_2px_0_0_#E9A43C]" : "hover:bg-[#12243A]"
      }`}
      onClick={() => props.onSelect(episode.id)}
    >
      <span
        className={`min-h-[30px] w-[3px] self-stretch rounded-[2px] ${
          muted ? "bg-severity-debug-rail" : episodeRailClass(episode.firstMatchSeverity)
        }`}
      />

      <div className="flex min-w-0 flex-col gap-[3px]">
        <span
          className={`truncate text-[13px] ${
            muted ? "text-[#8CA1B8]" : selected ? "font-medium text-foreground" : "text-[#DCE8F3]"
          }`}
        >
          {episode.title}
        </span>
        <span
          className={`flex min-w-0 items-center gap-[9px] overflow-hidden font-mono text-[11px] whitespace-nowrap ${
            muted ? "text-[#6E86A0]" : "text-[#7D93AA]"
          }`}
        >
          {/* Which Services and versions are in it: the Episode states its own versions now. */}
          <Participants episode={episode} services={props.services} muted={muted} />
          <span>{clock(episode.openedAt)}</span>
          {/* Only ever on screen while the rail asks for the filed ones: it says why this row is
              here, never anything about the trouble itself. */}
          {episode.archivedAt !== null && (
            <span className="text-[#6E86A0]">{t.alerting.list.archived}</span>
          )}
          {muted ? (
            <>
              <span>{t.alerting.list.mutedDuringEpisode}</span>
              {historyToggle}
            </>
          ) : (
            <>
              {episode.watch === "HealthCheck" ? (
                // Nothing was logged, so there is nothing to count: how long says it instead.
                <HealthCheckBadge />
              ) : (
                <>
                  <span className="text-severity-error">
                    {t.alerting.list.errCount(episode.errorCount)}
                  </span>
                  {Number(episode.warnCount) > 0 && (
                    <span className="text-severity-warn">
                      {t.alerting.list.warnCount(episode.warnCount)}
                    </span>
                  )}
                </>
              )}
              {historyToggle}
              <GroupingMarks episode={episode} />
              <QuietWindowBadge episode={episode} />
              <MachineHandBadges episode={episode} />
              {owner !== undefined && <span className="truncate">{owner}</span>}
            </>
          )}
        </span>
      </div>

      <StateBadge state={episode.state} />

      {open ? (
        <LiveDuration
          since={episode.openedAt}
          className="text-right font-mono text-[11.5px] text-[#DCE8F3]"
        />
      ) : (
        <span className="text-right font-mono text-[11.5px] text-[#7D93AA]">
          {describeMillis(Date.parse(episode.closedAt!) - Date.parse(episode.openedAt))}
        </span>
      )}
    </div>
  );
}

/**
 * The unfolded past of one kind of trouble: every episode before the face, newest first.
 * Deliberately outside the table's filters — the fold shows the group's whole history, not the
 * current narrowing — and paged by the same keyset the table rides on.
 */
function GroupHistory(props: {
  face: Episode;
  selectedId: string | undefined;
  onSelect: (id: string) => void;
}) {
  const { face } = props;
  const t = useT();
  const history = useInfiniteQuery({
    queryKey: ["alerts", "group-history", face.scopeKey, face.fingerprint],
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: {
            scopeKey: face.scopeKey,
            fingerprint: face.fingerprint,
            beforeId: pageParam,
            limit: HISTORY_PAGE,
          },
        },
      });
      if (error !== undefined) throw new Error(t.alerting.errors.loadEpisodeHistory);
      return data;
    },
    // Starting the keyset at the face keeps it out of its own history.
    initialPageParam: face.id as string | undefined,
    getNextPageParam: lastPage =>
      lastPage.items.length === HISTORY_PAGE ? lastPage.items.at(-1)!.id : undefined,
  });

  const items = history.data?.pages.flatMap(page => page.items) ?? [];
  const now = Date.now();

  return (
    <div data-testid="episode-history" className="border-b border-[#101F31] bg-[#0A1725]">
      {items.map(episode => (
        <HistoryRow
          key={episode.id}
          episode={episode}
          now={now}
          selected={episode.id === props.selectedId}
          onSelect={props.onSelect}
        />
      ))}
      {history.isPending && (
        <p className="px-5 py-2 pl-[52px] font-mono text-[11px] text-[#5F7590]">{t.common.loading}</p>
      )}
      {history.hasNextPage && (
        <button
          type="button"
          className="flex h-[30px] w-full cursor-pointer items-center px-5 pl-[52px] font-mono text-[11px] text-[#6E86A0] hover:text-[#A9BDD1]"
          disabled={history.isFetchingNextPage}
          onClick={() => history.fetchNextPage()}
        >
          {history.isFetchingNextPage ? t.common.loading : t.alerting.list.loadOlder}
        </button>
      )}
    </div>
  );
}

/**
 * One earlier episode of the kind, in the table's columns but a quieter voice: when it opened,
 * what it burned through, and how it ended. The body is not repeated — it is on the face above.
 */
function HistoryRow(props: {
  episode: Episode;
  now: number;
  selected: boolean;
  onSelect: (id: string) => void;
}) {
  const { episode, selected } = props;
  const t = useT();
  return (
    <div
      data-testid="history-row"
      className={`${GRID} cursor-pointer py-[7px] ${
        selected ? "bg-[#12243A] shadow-[inset_2px_0_0_#E9A43C]" : "hover:bg-[#12243A]"
      }`}
      onClick={() => props.onSelect(episode.id)}
    >
      <span />
      <span className="flex min-w-0 items-center gap-[9px] overflow-hidden pl-4 font-mono text-[11px] whitespace-nowrap text-[#7D93AA]">
        <span>{historyStamp(episode.openedAt, props.now)}</span>
        <span className="text-severity-error">{t.alerting.list.errCount(episode.errorCount)}</span>
        {Number(episode.warnCount) > 0 && (
          <span className="text-severity-warn">{t.alerting.list.warnCount(episode.warnCount)}</span>
        )}
        {episode.solvedBy !== null && (
          <span className="truncate">{t.alerting.list.solvedBy(episode.solvedBy)}</span>
        )}
      </span>
      <StateBadge state={episode.state} />
      {episode.closedAt == null ? (
        <LiveDuration
          since={episode.openedAt}
          className="text-right font-mono text-[11.5px] text-[#DCE8F3]"
        />
      ) : (
        <span className="text-right font-mono text-[11.5px] text-[#7D93AA]">
          {describeMillis(Date.parse(episode.closedAt) - Date.parse(episode.openedAt))}
        </span>
      )}
    </div>
  );
}
