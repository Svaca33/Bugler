import { useInfiniteQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { ChevronRightIcon } from "lucide-react";
import { useState } from "react";

import { api, type Episode } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { describeMillis } from "@/lib/duration";
import { formatTime } from "@/lib/format";
import { serviceLabel } from "@/lib/serviceLabel";
import { severityRailClass } from "@/lib/severity";

import { EpisodeDetail } from "./EpisodeDetail";
import { StateBadge } from "./StateBadge";

const PAGE_SIZE = 100;

const GRID = "grid grid-cols-[26px_3px_150px_230px_130px_110px_150px_1fr] items-center gap-3.5 px-5";

const STATES: Episode["state"][] = ["Open", "Quieted", "Solved", "Muted"];

/** The Episodes within the viewer's Visibility Scope, newest first, refreshed while watched. */
export function EpisodesPage() {
  const catalog = useCatalog();
  const navigate = useNavigate();
  const [states, setStates] = useState<Episode["state"][]>([]);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const episodes = useInfiniteQuery({
    queryKey: ["alerts", "episodes", states],
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: {
          query: {
            limit: PAGE_SIZE,
            beforeId: pageParam,
            state: states.length > 0 ? states : undefined,
          },
        },
      });
      if (error !== undefined) throw new Error("Failed to load episodes");
      return data;
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: lastPage =>
      lastPage.items.length === PAGE_SIZE ? lastPage.items.at(-1)!.id : undefined,
    refetchInterval: 30_000,
  });

  const services = new Map(
    (catalog.data?.applications ?? []).flatMap(application =>
      application.services.map(service => [
        service.id,
        { application, facets: service },
      ] as const)),
  );

  const items = episodes.data?.pages.flatMap(page => page.items) ?? [];

  const toggleState = (state: Episode["state"]) =>
    setStates(current =>
      current.includes(state) ? current.filter(s => s !== state) : [...current, state]);

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

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-center gap-2 px-5 py-2">
        <span className="font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">STATE</span>
        {STATES.map(state => (
          <button
            key={state}
            type="button"
            className={`h-6 rounded-md border px-2 font-mono text-[10.5px] uppercase tracking-[0.08em] ${
              states.includes(state)
                ? "border-[#2C4159] bg-[#12243A] text-[#DCE8F3]"
                : "border-[#17293D] text-[#6E86A0] hover:text-[#A9BDD1]"
            }`}
            onClick={() => toggleState(state)}
          >
            {state.toLowerCase()}
          </button>
        ))}
      </div>

      <div className="min-h-0 flex-1 overflow-auto">
        <div
          className={`${GRID} sticky top-0 z-10 h-[30px] border-y border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
        >
          <span />
          <span />
          <span>OPENED</span>
          <span>SERVICE</span>
          <span>STATUS</span>
          <span>DURATION</span>
          <span>COUNTED</span>
          <span>FIRST LOG</span>
        </div>
        <div data-testid="episode-rows">
          {items.map(episode => {
            const known = services.get(episode.serviceId);
            const open = episode.state === "Open";
            const expanded = expandedId === episode.id;
            const durationMs =
              (episode.closedAt != null ? Date.parse(episode.closedAt) : Date.now())
                - Date.parse(episode.openedAt);
            return (
              <div key={episode.id}>
                <div
                  data-testid="episode-row"
                  className={`${GRID} h-[37px] cursor-pointer border-b border-[#101F31] ${
                    open ? "bg-[rgba(229,84,74,0.07)] hover:bg-[#12243A]" : "hover:bg-[#12243A]"
                  }`}
                  onClick={() => openLogs(episode)}
                >
                  <button
                    type="button"
                    aria-label={expanded ? "Collapse episode" : "Expand episode"}
                    aria-expanded={expanded}
                    className="flex size-6 items-center justify-center rounded-sm text-[#5F7590] hover:bg-[#17293D] hover:text-[#A9BDD1]"
                    onClick={event => {
                      event.stopPropagation();
                      setExpandedId(expanded ? null : episode.id);
                    }}
                  >
                    <ChevronRightIcon
                      className={`size-3.5 transition-transform ${expanded ? "rotate-90" : ""}`}
                    />
                  </button>
                  <span
                    className={`h-[15px] w-[3px] rounded-[2px] ${severityRailClass(Number(episode.firstLogSeverity))}`}
                  />
                  <span className="whitespace-nowrap font-mono text-[11.5px] text-[#7D93AA]">
                    {formatTime(episode.openedAt)}
                  </span>
                  <span className="truncate font-mono text-[11.5px] text-[#A9BDD1]">
                    {known !== undefined
                      ? `${known.application.name} · ${serviceLabel(known.facets)}`
                      : "—"}
                  </span>
                  <span className="flex min-w-0 items-center gap-1.5">
                    <StateBadge state={episode.state} />
                    {episode.acknowledgedBy !== null && (
                      <span
                        className="truncate font-mono text-[10.5px] text-[#8CA1B8]"
                        title={`Acknowledged by ${episode.acknowledgedBy}`}
                      >
                        · ack
                      </span>
                    )}
                  </span>
                  <span className="whitespace-nowrap font-mono text-[11.5px] text-[#7D93AA]">
                    {describeMillis(durationMs)}
                  </span>
                  <span className="truncate whitespace-nowrap font-mono text-[11.5px]">
                    <span className="text-severity-error">{episode.errorCount} err</span>
                    {Number(episode.warnCount) > 0 && (
                      <span className="text-severity-warn"> · {episode.warnCount} warn</span>
                    )}
                  </span>
                  <span className="flex min-w-0 items-center gap-2 font-mono text-[12.5px] text-[#DCE8F3]">
                    {Number(episode.priorCount) > 0 && (
                      <span
                        className="shrink-0 rounded-sm border border-[#2C4159] px-1 text-[10.5px] text-[#8CA1B8]"
                        title={`This kind of trouble burned ${Number(episode.priorCount) === 1 ? "once" : `${episode.priorCount} times`} before`}
                      >
                        ×{Number(episode.priorCount) + 1}
                      </span>
                    )}
                    <span className="truncate">{episode.firstLogBody}</span>
                  </span>
                </div>
                {expanded && <EpisodeDetail episode={episode} />}
              </div>
            );
          })}
          {items.length === 0 && !episodes.isPending && (
            <p className="py-16 text-center text-[#8CA1B8]">
              {states.length > 0
                ? "No episodes match the state filter."
                : "No episodes — no watched service has logged trouble yet."}
            </p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-3 border-t border-[#17293D] px-5 py-2.5">
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
        <span className="font-mono text-[11px] text-[#6E86A0]">{items.length} loaded</span>
      </div>
    </div>
  );
}
