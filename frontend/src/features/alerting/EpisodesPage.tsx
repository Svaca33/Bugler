import { useInfiniteQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";

import { api, type Episode } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { describeMillis } from "@/lib/duration";
import { formatTime } from "@/lib/format";
import { serviceLabel } from "@/lib/serviceLabel";
import { severityRailClass } from "@/lib/severity";

const PAGE_SIZE = 100;

const GRID = "grid grid-cols-[3px_150px_230px_96px_110px_150px_1fr] items-center gap-3.5 px-5";

/** The Episodes within the viewer's Visibility Scope, newest first, refreshed while watched. */
export function EpisodesPage() {
  const catalog = useCatalog();
  const navigate = useNavigate();

  const episodes = useInfiniteQuery({
    queryKey: ["alerts", "episodes"],
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET("/api/alerting/episodes", {
        params: { query: { limit: PAGE_SIZE, beforeId: pageParam } },
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
      <div className="min-h-0 flex-1 overflow-auto">
        <div
          className={`${GRID} sticky top-0 z-10 h-[30px] border-y border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
        >
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
            const closed = episode.closedAt != null;
            const durationMs =
              (closed ? Date.parse(episode.closedAt!) : Date.now()) - Date.parse(episode.openedAt);
            return (
              <div
                key={episode.id}
                className={`${GRID} h-[37px] cursor-pointer border-b border-[#101F31] ${
                  closed ? "hover:bg-[#12243A]" : "bg-[rgba(229,84,74,0.07)] hover:bg-[#12243A]"
                }`}
                onClick={() => openLogs(episode)}
              >
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
                {closed ? (
                  episode.closeReason === "SensitivityOff" ? (
                    <span className="font-mono text-[10.5px] uppercase tracking-[0.08em] text-[#6E86A0]">
                      muted
                    </span>
                  ) : (
                    <span className="font-mono text-[10.5px] uppercase tracking-[0.08em] text-[#8CA1B8]">
                      all clear
                    </span>
                  )
                ) : (
                  <span className="flex items-center gap-1.5 font-mono text-[10.5px] uppercase tracking-[0.08em] text-severity-error">
                    <span className="size-1.5 animate-[bpulse_1.6s_ease-in-out_infinite] rounded-full bg-severity-error-rail" />
                    open
                  </span>
                )}
                <span className="whitespace-nowrap font-mono text-[11.5px] text-[#7D93AA]">
                  {describeMillis(durationMs)}
                </span>
                <span className="truncate whitespace-nowrap font-mono text-[11.5px]">
                  <span className="text-severity-error">{episode.errorCount} err</span>
                  {Number(episode.warnCount) > 0 && (
                    <span className="text-severity-warn"> · {episode.warnCount} warn</span>
                  )}
                </span>
                <span className="truncate font-mono text-[12.5px] text-[#DCE8F3]">
                  {episode.firstLogBody}
                </span>
              </div>
            );
          })}
          {items.length === 0 && !episodes.isPending && (
            <p className="py-16 text-center text-[#8CA1B8]">
              No episodes — no watched service has logged trouble yet.
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
