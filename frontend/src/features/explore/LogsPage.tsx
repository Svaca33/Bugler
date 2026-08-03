import { useInfiniteQuery, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLayoutEffect, useRef, useState } from "react";

import { api, type LogRecord } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { FilterChip } from "@/components/ui/filter-chip";
import { Input } from "@/components/ui/input";
import { ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { useT } from "@/i18n";
import { getMessages } from "@/i18n/runtime";
import { formatTime } from "@/lib/format";
import { severityClass, severityFilterOptions, severityLabel, severityRailClass } from "@/lib/severity";

import { AttributeFilterBar } from "./AttributeFilterBar";
import { toQueryParams, type AttributeFilter } from "./attributeFilters";
import { FOLLOW_PAGE, ROW_HEIGHT } from "./follow";
import { useFollow } from "./useFollow";
import { MIN_LIST_WIDTH } from "@/lib/detailWidth";
import { FilterGroup, FilterRail } from "@/components/ui/filter-rail";
import { FilterSelect } from "@/components/ui/filter-select";
import { tenantOf } from "./format";
import { LogDetailPanel } from "./LogDetailPanel";
import { LogVolumeChart } from "./LogVolumeChart";
import { RefreshButton } from "./RefreshButton";
import { facetOptions, serviceLabels, type SourceFilters } from "@/lib/sourceFilter";
import { EMPTY_TIME, emptyStateMessage, widerPresets, type TimeFilterValue } from "./timeFilter";
import { TimeFilterControl } from "./TimeFilterControl";

export interface LogFilters extends SourceFilters, TimeFilterValue {
  severityMin?: number;
  q?: string;
  traceId?: string;
  filters?: AttributeFilter[];
}

const PAGE_SIZE = 100;

const GRID = "grid grid-cols-[3px_172px_66px_196px_96px_1fr] items-center gap-3.5 px-5";

/** Everything the Filter narrows by, in the shape the API takes it — the list, its total and
 *  Follow's own ticks all read it from here so none of them can drift from the others. */
function logQuery(filters: LogFilters) {
  return {
    applicationId: filters.applicationId,
    namespace: filters.namespace,
    environment: filters.environment,
    service: filters.service,
    severityMin: filters.severityMin,
    range: filters.range,
    from: filters.from,
    to: filters.to,
    q: filters.q,
    traceId: filters.traceId,
    ...toQueryParams(filters.filters ?? []),
  };
}

export function LogsPage(props: {
  filters: LogFilters;
  onChange: (filters: LogFilters) => void;
  /** The open Log Record rides in the URL, so a mailed alert can point at one record. */
  selectedLogId?: number;
  onSelectLog: (id: number | undefined) => void;
}) {
  const { filters, onChange, selectedLogId, onSelectLog } = props;
  const t = useT();
  const catalog = useCatalog();
  const queryClient = useQueryClient();
  const [follow, setFollow] = useState(false);
  const [search, setSearch] = useState(filters.q ?? "");
  const scroller = useRef<HTMLDivElement>(null);

  const logs = useInfiniteQuery({
    queryKey: ["logs", filters],
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET("/api/logs", {
        params: {
          query: {
            ...logQuery(filters),
            limit: PAGE_SIZE,
            before: pageParam?.before,
            beforeId: pageParam?.beforeId,
          },
        },
      });
      if (error !== undefined) throw new Error(getMessages().explore.loadFailed.logs);
      return data;
    },
    initialPageParam: undefined as { before: string; beforeId: number } | undefined,
    getNextPageParam: lastPage => {
      const last = lastPage.items.at(-1);
      return lastPage.items.length === PAGE_SIZE && last !== undefined
        ? { before: last.timestamp, beforeId: Number(last.id) }
        : undefined;
    },
  });

  const paged = logs.data?.pages.flatMap(page => page.items) ?? [];

  // Follow reads forward from the newest record on screen. It is a second list rather than more
  // pages of this one: it holds only what it has been handed, in the order it arrived.
  const followed = useFollow({
    enabled: follow,
    key: JSON.stringify(logQuery(filters)),
    seed: () => paged,
    fetchNewer: async (cursor, signal) => {
      const { data, error } = await api.GET("/api/logs", {
        signal,
        params: { query: { ...logQuery(filters), limit: FOLLOW_PAGE, ...cursor } },
      });
      if (error !== undefined) throw new Error(getMessages().explore.loadFailed.logs);
      return data.items;
    },
    onGiveUp: () => setFollow(false),
  });

  // New records land above the viewport, so the browser would push everything the reader is looking
  // at downwards. Rows are one fixed height, which makes the correction exact rather than nearly.
  useLayoutEffect(() => {
    const element = scroller.current;
    if (element === null || followed.added === 0 || element.scrollTop === 0) return;
    element.scrollTop += followed.added * ROW_HEIGHT;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one correction per tick, not per render
  }, [followed.pulse]);

  const startFollowing = () => {
    setFollow(true);
    // Following is a claim to be watching the end of the stream, and a Filter that ends before the
    // stream does contradicts it. Only the top goes; where the window opens is still the reader's.
    if (filters.to !== undefined) onChange({ ...filters, to: undefined });
  };

  // One click renews everything the page is showing. The list is *reset* rather than invalidated:
  // invalidation would re-walk every paged-in page on cursors cut for a window that has moved,
  // while the reader pressing Refresh is asking for a fresh first page, not their place in history.
  // Everything else on the page — total, Volume, Release markers, catalog, the open record —
  // follows along so no panel sits stale beside a fresh list.
  const refresh = () => {
    void queryClient.resetQueries({ queryKey: ["logs", filters] });
    void queryClient.invalidateQueries({ queryKey: ["logs-count", filters] });
    void queryClient.invalidateQueries({ queryKey: ["logs-volume"] });
    void queryClient.invalidateQueries({ queryKey: ["releases"] });
    void queryClient.invalidateQueries({ queryKey: ["catalog"] });
    void queryClient.invalidateQueries({ queryKey: ["log"] });
  };

  const stopFollowing = () => {
    setFollow(false);
    // A followed list passed over records without saying so, so it must not be paged on afterwards:
    // what replaces it is an ordinary first page of the same Filter, contiguous as far as it goes.
    void queryClient.resetQueries({ queryKey: ["logs", filters] });
  };

  // The total belongs to the Filter, not to how far the reader has paged, so it is asked once per
  // Filter rather than once per page — and not at all while Follow keeps moving the answer.
  const total = useQuery({
    queryKey: ["logs-count", filters],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/logs/count", {
        params: { query: logQuery(filters) },
      });
      if (error !== undefined) throw new Error(getMessages().explore.loadFailed.logCount);
      return data;
    },
    enabled: !follow,
  });

  const items = follow ? followed.items : paged;
  const applications = catalog.data?.applications ?? [];
  const labels = serviceLabels(applications);

  // The selected record usually sits in the loaded pages; a deep-linked one may not — the
  // current Filter can exclude it — so it is fetched alone rather than clipped away.
  const selectedFromList =
    selectedLogId === undefined ? undefined : items.find(log => Number(log.id) === selectedLogId);
  const selectedFetch = useQuery({
    queryKey: ["log", selectedLogId],
    queryFn: async () => {
      const { data, response } = await api.GET("/api/logs/{id}", {
        params: { path: { id: selectedLogId! } },
      });
      if (response.status === 404) return null;
      if (data === undefined) throw new Error(getMessages().explore.loadFailed.logRecord);
      return data;
    },
    enabled: selectedLogId !== undefined && selectedFromList === undefined,
  });
  const selected = selectedFromList ?? selectedFetch.data ?? null;

  // Never "N records": that reads as the total while it only ever counts what has been paged in.
  // Past the cap the total reads "1000+" — the count stops there rather than scanning a window of
  // millions to put an exact digit on the end of a number read as "a lot" either way. A followed
  // list is counted nowhere at all: it is a window on what is arriving rather than an account of
  // it, and what it declines to say it declines to say in numbers too (Exploration ADR 0004).
  const counted =
    total.data === undefined
      ? t.explore.logs.loaded(items.length)
      : t.explore.logs.counted(items.length, Number(total.data.total), total.data.capped);

  return (
    <div className="flex h-full min-h-0">
      {/* `filters` never carries the `log` key, so an open record alone does not light Clear all. */}
      <FilterRail
        canClear={Object.values(filters).some(value => value !== undefined)}
        onClear={() => {
          setSearch("");
          onChange({});
        }}
      >
        {filters.traceId !== undefined && (
          <FilterGroup label={t.explore.rail.scopeGroup}>
            <FilterChip
              className="w-full"
              removeLabel={t.explore.rail.leaveTrace}
              onRemove={() => onChange({ ...filters, traceId: undefined })}
            >
              {t.explore.rail.traceScope(filters.traceId.slice(0, 8))}
            </FilterChip>
          </FilterGroup>
        )}

        <FilterGroup label={t.explore.rail.sourceGroup}>
          <FilterSelect
            className="w-full"
            placeholder={t.explore.rail.allApplications}
            value={filters.applicationId}
            options={applications.map(a => ({ value: a.id, label: a.name }))}
            onChange={applicationId =>
              onChange({
                ...filters,
                applicationId,
                namespace: undefined,
                environment: undefined,
                service: undefined,
              })
            }
          />
          <FilterSelect
            className="w-full"
            placeholder={t.explore.rail.allNamespaces}
            value={filters.namespace}
            options={facetOptions(applications, filters, "namespace").map(v => ({ value: v, label: v }))}
            onChange={namespace => onChange({ ...filters, namespace })}
          />
          <FilterSelect
            className="w-full"
            placeholder={t.explore.rail.allEnvironments}
            value={filters.environment}
            options={facetOptions(applications, filters, "environment").map(v => ({ value: v, label: v }))}
            onChange={environment => onChange({ ...filters, environment })}
          />
          <FilterSelect
            className="w-full"
            placeholder={t.explore.rail.allServices}
            value={filters.service}
            options={facetOptions(applications, filters, "service").map(v => ({ value: v, label: v }))}
            onChange={service => onChange({ ...filters, service })}
          />
        </FilterGroup>

        <FilterGroup label={t.explore.rail.timeGroup}>
          <TimeFilterControl
            layout="column"
            value={filters}
            onChange={time => onChange({ ...filters, ...EMPTY_TIME, ...time })}
          />
        </FilterGroup>

        <FilterGroup label={t.explore.rail.severityGroup}>
          <FilterSelect
            className="w-full"
            placeholder={t.common.severityFilter.all}
            value={filters.severityMin?.toString()}
            options={severityFilterOptions()
              .filter(o => o.value > 0)
              .map(o => ({ value: o.value.toString(), label: o.label }))}
            onChange={value =>
              onChange({ ...filters, severityMin: value === undefined ? undefined : Number(value) })
            }
          />
        </FilterGroup>

        <FilterGroup label={t.explore.rail.messageGroup}>
          <form
            className="flex flex-col gap-2"
            onSubmit={event => {
              event.preventDefault();
              onChange({ ...filters, q: search || undefined });
            }}
          >
            <Input
              placeholder={t.explore.rail.searchPlaceholder}
              value={search}
              onChange={event => setSearch(event.target.value)}
            />
            <Button type="submit" variant="secondary" size="sm" className="w-full">
              {t.explore.rail.search}
            </Button>
          </form>
        </FilterGroup>

        <FilterGroup label={t.explore.rail.attributesGroup}>
          <AttributeFilterBar
            layout="column"
            signal="logs"
            source={filters}
            filters={filters.filters ?? []}
            onChange={next => onChange({ ...filters, filters: next.length > 0 ? next : undefined })}
          />
        </FilterGroup>
      </FilterRail>

      <ResizablePanelGroup className="min-w-0 flex-1">
        <ResizablePanel
          id="list"
          minSize={`${MIN_LIST_WIDTH}px`}
          className="flex h-full min-w-0 flex-col"
        >
          <div className="flex items-center gap-3.5 px-5 py-3">
            <h1 className="text-sm font-semibold tracking-[-0.1px]">{t.explore.logs.title}</h1>
            {/* The one thing a followed list says about itself: that it is still running. Without
                it a stream nothing is arriving into looks the same as one that has stopped. */}
            {follow && (
              <span className="size-1.5 animate-[bpulse_1.6s_ease-in-out_infinite] rounded-full bg-primary" />
            )}
            {/* Follow is not a filter — it is how this list is being read, so it lives on the list. */}
            <div className="ml-auto flex items-center gap-3">
              {!follow && <span className="font-mono text-[11.5px] text-[#6E86A0]">{counted}</span>}
              {/* Follow already renews the list on its own tick, so Refresh steps aside with the
                  paging and the count: two renewers over one list could only disagree. */}
              {!follow && (
                <RefreshButton
                  busy={logs.isFetching && !logs.isFetchingNextPage}
                  onRefresh={refresh}
                />
              )}
              <Button
                type="button"
                variant={follow ? "default" : "outline"}
                size="sm"
                onClick={() => (follow ? stopFollowing() : startFollowing())}
              >
                {follow ? t.explore.logs.following : t.explore.logs.follow}
              </Button>
            </div>
          </div>

          {/* Pinned above the scroll, never inside it: the Volume is the frame of reference for
              where in time you are, and it is needed most while paging back through older records. */}
          <LogVolumeChart
            filters={filters}
            follow={follow}
            pulse={followed.pulse}
            onNarrow={time => {
              // Narrowing to a stretch of the past is the opposite of watching what arrives.
              if (follow) stopFollowing();
              onChange({ ...filters, ...time });
            }}
          />

          {/* Scroll anchoring is off because the correction above is exact and the browser's is a
              guess; two of them would fight over the same pixels. */}
          <div ref={scroller} className="min-h-0 flex-1 overflow-auto [overflow-anchor:none]">
            <div
              className={`${GRID} sticky top-0 z-10 h-[30px] border-y border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
            >
              <span />
              <span>{t.explore.logs.headers.time}</span>
              <span>{t.explore.logs.headers.severity}</span>
              <span>{t.explore.logs.headers.service}</span>
              <span>{t.explore.logs.headers.tenant}</span>
              <span>{t.explore.logs.headers.message}</span>
            </div>
            <div data-testid="log-rows">
              {items.map(log => {
                const severity = Number(log.severityNumber);
                const isSelected = selectedLogId !== undefined && Number(log.id) === selectedLogId;
                const isError = severity >= 17;
                const rowBackground = isSelected
                  ? "bg-[rgba(233,164,60,0.09)] shadow-[inset_2px_0_0_#E9A43C] hover:bg-[rgba(233,164,60,0.14)]"
                  : isError
                    ? "bg-[rgba(229,84,74,0.07)] hover:bg-[#12243A]"
                    : "hover:bg-[#12243A]";
                return (
                  <div
                    key={log.id}
                    className={`${GRID} h-[37px] cursor-pointer border-b border-[#101F31] ${rowBackground}`}
                    onClick={() => onSelectLog(Number(log.id))}
                  >
                    <span className={`h-[15px] w-[3px] rounded-[2px] ${severityRailClass(severity)}`} />
                    <span
                      className={`whitespace-nowrap font-mono text-[11.5px] ${isSelected ? "text-[#B6C8DA]" : "text-[#7D93AA]"}`}
                    >
                      {formatTime(log.timestamp)}
                    </span>
                    <span
                      className={`truncate font-mono text-[10.5px] font-medium uppercase tracking-[0.08em] ${severityClass(severity)}`}
                    >
                      {log.severityText || severityLabel(severity)}
                    </span>
                    <span
                      className={`truncate font-mono text-[11.5px] ${isSelected ? "text-[#C6D6E6]" : "text-[#A9BDD1]"}`}
                    >
                      {labels.get(log.serviceId) ?? "—"}
                    </span>
                    <span className="truncate font-mono text-[11.5px] text-[#7D93AA]">
                      {tenantOf(log) || "—"}
                    </span>
                    <span
                      className={`truncate font-mono text-[12.5px] ${
                        isSelected ? "text-[#F6E3C4]" : severity < 9 ? "text-[#A9BDD1]" : "text-[#DCE8F3]"
                      }`}
                    >
                      {log.body}
                    </span>
                  </div>
                );
              })}
              {items.length === 0 && !logs.isPending && (
                <div className="flex flex-col items-center gap-3 py-16">
                  <p className="text-[#8CA1B8]">{emptyStateMessage("logRecords", filters)}</p>
                  {widerPresets(filters).length > 0 && (
                    <div className="flex items-center gap-2">
                      <span className="text-[#6E86A0] text-xs">{t.explore.timeFilter.widenTo}</span>
                      {widerPresets(filters).map(preset => (
                        <Button
                          key={preset.value}
                          variant="outline"
                          size="sm"
                          onClick={() => onChange({ ...filters, ...EMPTY_TIME, range: preset.value })}
                        >
                          {preset.label}
                        </Button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Paging on under a followed list would join two lists that are not one: below the
              oldest record Follow was handed there is nothing to carry on from. Leaving Follow is
              what gives the reader a list to page. */}
          {!follow && (
            <div className="flex items-center gap-3 border-t border-[#17293D] px-5 py-2.5">
              <Button
                variant="outline"
                size="sm"
                disabled={!logs.hasNextPage || logs.isFetchingNextPage}
                onClick={() => logs.fetchNextPage()}
              >
                {logs.isFetchingNextPage
                  ? t.common.loading
                  : logs.hasNextPage
                    ? t.explore.logs.loadOlder
                    : t.explore.logs.noOlderRecords}
              </Button>
              <span className="font-mono text-[11px] text-[#6E86A0]">{counted}</span>
            </div>
          )}
        </ResizablePanel>

        {selected !== null && (
          <LogDetailPanel
            log={selected}
            filters={filters.filters ?? []}
            onFiltersChange={next =>
              onChange({ ...filters, filters: next.length > 0 ? next : undefined })
            }
            onClose={() => onSelectLog(undefined)}
          />
        )}
      </ResizablePanelGroup>
    </div>
  );
}
