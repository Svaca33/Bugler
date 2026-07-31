import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import type { ReactNode } from "react";

import { api, type Episode } from "@/api/client";
import { useCatalog, useCurrentUser } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { MIN_LIST_WIDTH } from "@/lib/detailWidth";
import { describeMillis } from "@/lib/duration";
import { severityRailClass } from "@/lib/severity";

import {
  effectiveLifecycle,
  hasSourceFilter,
  openedFrom,
  openedPhrase,
  resolveServiceIds,
  type AlertsFilters,
} from "./alertsFilter";
import { AlertsFilterRail } from "./AlertsFilterRail";
import { EpisodeDetailPanel } from "./EpisodeDetailPanel";
import { clock, dayLabel } from "./format";
import { LiveDuration } from "./LiveDuration";
import { OpenNowBand } from "./OpenNowBand";
import { indexServices, type KnownService } from "./serviceIndex";
import { StateBadge } from "./StateBadge";

const PAGE_SIZE = 100;
const REFETCH_MS = 30_000;

const GRID = "grid grid-cols-[3px_1fr_128px_86px] items-center gap-3.5 px-5";

/** The Episodes within the viewer's Visibility Scope: the burning ones on top, history below. */
export function EpisodesPage(props: {
  filters: AlertsFilters;
  onChange: (filters: AlertsFilters) => void;
  selectedId: string | undefined;
  onSelect: (id: string | undefined) => void;
}) {
  const { filters, onChange, selectedId, onSelect } = props;
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
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: {
            limit: PAGE_SIZE,
            beforeId: pageParam,
            state: lifecycle,
            from: openedFrom(filters),
            ...shared,
          },
        },
      });
      if (error !== undefined) throw new Error("Failed to load episodes");
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
      if (error !== undefined) throw new Error("Failed to load open episodes");
      return data.items;
    },
    refetchInterval: REFETCH_MS,
    enabled: ready,
  });

  const counts = useQuery({
    queryKey: ["alerts", "counts", nonLifecycle],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/alerting/episodes/counts", {
        params: { query: { from: openedFrom(filters), ...shared } },
      });
      if (error !== undefined) throw new Error("Failed to count episodes");
      return data;
    },
    refetchInterval: REFETCH_MS,
    enabled: ready,
  });

  const services = indexServices(applications);
  const items = episodes.data?.pages.flatMap(page => page.items) ?? [];
  const myName = currentUser.data?.displayName ?? currentUser.data?.email;

  const openLogs = (episode: Episode) => {
    const known = services.get(episode.serviceId);
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
        log: Number(episode.firstLogId),
      },
    });
  };

  const stateKey = { Open: "open", Quieted: "quieted", Solved: "solved", Muted: "muted" } as const;
  const total = counts.data === undefined
    ? undefined
    : lifecycle.reduce((sum, state) => sum + Number(counts.data[stateKey[state]]), 0);
  const windowPhrase = openedPhrase(filters);
  const counted = total === undefined
    ? `${items.length} loaded`
    : `${items.length} of ${total} episodes${windowPhrase !== undefined ? ` ${windowPhrase}` : ""}`;

  const narrowed = [
    filters.lifecycle, filters.ack, filters.applicationId, filters.namespace,
    filters.environment, filters.service, filters.opened, filters.q,
  ].some(value => value !== undefined);

  return (
    <div className="flex h-full min-h-0">
      <AlertsFilterRail filters={filters} counts={counts.data} onChange={onChange} />

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
              <span>EPISODE</span>
              <span>STATE</span>
              <span className="text-right">DURATION</span>
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
                  {narrowed
                    ? "No episodes match the state filter."
                    : "No episodes — no watched service has logged trouble yet."}
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
                ? "Loading…"
                : episodes.hasNextPage
                  ? "Load older"
                  : "No older episodes"}
            </Button>
            <span className="font-mono text-[11px] whitespace-nowrap text-[#6E86A0]">{counted}</span>
          </div>
        </ResizablePanel>

        {selectedId !== undefined && (
          <EpisodeDetailPanel
            id={selectedId}
            fromList={items.find(e => e.id === selectedId) ?? band.data?.find(e => e.id === selectedId)}
            services={services}
            onClose={() => onSelect(undefined)}
            onOpenLogs={openLogs}
          />
        )}
      </ResizablePanelGroup>
    </div>
  );
}

/** The history rows with one separator per calendar day in the loaded set. */
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
    rows.push(
      <EpisodeRow
        key={episode.id}
        episode={episode}
        known={props.services.get(episode.serviceId)}
        selected={episode.id === props.selectedId}
        myName={props.myName}
        onSelect={props.onSelect}
      />,
    );
  }

  return rows;
}

function EpisodeRow(props: {
  episode: Episode;
  known: KnownService | undefined;
  selected: boolean;
  myName: string | undefined;
  onSelect: (id: string) => void;
}) {
  const { episode, known, selected } = props;
  const muted = episode.state === "Muted";
  const open = episode.state === "Open";
  const heldByMe = episode.acknowledgedBy !== null && episode.acknowledgedBy === props.myName;
  const priorCount = Number(episode.priorCount);

  const owner = episode.acknowledgedBy !== null
    ? heldByMe ? "you" : episode.acknowledgedBy
    : episode.solvedBy !== null
      ? `solved by ${episode.solvedBy}`
      : undefined;

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
          muted ? "bg-severity-debug-rail" : severityRailClass(Number(episode.firstLogSeverity))
        }`}
      />

      <div className="flex min-w-0 flex-col gap-[3px]">
        <span
          className={`truncate text-[13px] ${
            muted ? "text-[#8CA1B8]" : selected ? "font-medium text-foreground" : "text-[#DCE8F3]"
          }`}
        >
          {episode.firstLogBody}
        </span>
        <span
          className={`flex min-w-0 items-center gap-[9px] overflow-hidden font-mono text-[11px] whitespace-nowrap ${
            muted ? "text-[#6E86A0]" : "text-[#7D93AA]"
          }`}
        >
          <span className={muted ? undefined : "text-[#A9BDD1]"}>{known?.facets.name ?? "—"}</span>
          <span>{clock(episode.openedAt)}</span>
          {muted ? (
            <span>alerting turned off during the episode</span>
          ) : (
            <>
              <span className="text-severity-error">{episode.errorCount} err</span>
              {Number(episode.warnCount) > 0 && (
                <span className="text-severity-warn">{episode.warnCount} warn</span>
              )}
              {priorCount > 0 && (
                <span
                  className="rounded-sm border border-[#2C4159] px-[5px] text-[#A9BDD1]"
                  title={`This kind of trouble burned ${priorCount === 1 ? "once" : `${priorCount} times`} before`}
                >
                  ×{priorCount + 1}
                </span>
              )}
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
