import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { api, type LogRecord } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { FilterChip } from "@/components/ui/filter-chip";
import { Input } from "@/components/ui/input";
import { formatTime } from "@/lib/format";
import { severityClass, severityFilterOptions, severityLabel, severityRailClass } from "@/lib/severity";

import { AttributeFilterBar } from "./AttributeFilterBar";
import { toQueryParams, type AttributeFilter } from "./attributeFilters";
import { FilterGroup, FilterRail } from "./FilterRail";
import { FilterSelect } from "./FilterSelect";
import { tenantOf } from "./format";
import { LogDetailPanel } from "./LogDetailPanel";
import { LogVolumeChart } from "./LogVolumeChart";
import { facetOptions, serviceLabels, type SourceFilters } from "./sourceFilter";
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

export function LogsPage(props: {
  filters: LogFilters;
  onChange: (filters: LogFilters) => void;
  /** The open Log Record rides in the URL, so a mailed alert can point at one record. */
  selectedLogId?: number;
  onSelectLog: (id: number | undefined) => void;
}) {
  const { filters, onChange, selectedLogId, onSelectLog } = props;
  const catalog = useCatalog();
  const [live, setLive] = useState(false);
  const [search, setSearch] = useState(filters.q ?? "");

  const logs = useInfiniteQuery({
    queryKey: ["logs", filters],
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET("/api/logs", {
        params: {
          query: {
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
            limit: PAGE_SIZE,
            before: pageParam?.before,
            beforeId: pageParam?.beforeId,
          },
        },
      });
      if (error !== undefined) throw new Error("Failed to load logs");
      return data;
    },
    initialPageParam: undefined as { before: string; beforeId: number } | undefined,
    getNextPageParam: lastPage => {
      const last = lastPage.items.at(-1);
      return lastPage.items.length === PAGE_SIZE && last !== undefined
        ? { before: last.timestamp, beforeId: Number(last.id) }
        : undefined;
    },
    refetchInterval: live ? 5000 : false,
  });

  // The total belongs to the Filter, not to how far the reader has paged, so it is asked once per
  // Filter rather than once per page — and not at all while Live keeps moving the answer.
  const total = useQuery({
    queryKey: ["logs-count", filters],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/logs/count", {
        params: {
          query: {
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
          },
        },
      });
      if (error !== undefined) throw new Error("Failed to count log records");
      return data;
    },
    enabled: !live,
  });

  const items = logs.data?.pages.flatMap(page => page.items) ?? [];
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
      if (data === undefined) throw new Error("Failed to load the log record");
      return data;
    },
    enabled: selectedLogId !== undefined && selectedFromList === undefined,
  });
  const selected = selectedFromList ?? selectedFetch.data ?? null;

  // Never "N records": that reads as the total while it only ever counts what has been paged in.
  // Past the cap the total reads "1000+" — the count stops there rather than scanning a window of
  // millions to put an exact digit on the end of a number read as "a lot" either way.
  const counted = live
    ? `${items.length} loaded`
    : total.data === undefined
      ? `${items.length} loaded`
      : `${items.length} of ${total.data.total}${total.data.capped ? "+" : ""} records`;

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
          <FilterGroup label="SCOPE">
            <FilterChip
              className="w-full"
              removeLabel="Leave the trace"
              onRemove={() => onChange({ ...filters, traceId: undefined })}
            >
              Trace: {filters.traceId.slice(0, 8)}…
            </FilterChip>
          </FilterGroup>
        )}

        <FilterGroup label="SOURCE">
          <FilterSelect
            className="w-full"
            placeholder="All applications"
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
            placeholder="All namespaces"
            value={filters.namespace}
            options={facetOptions(applications, filters, "namespace").map(v => ({ value: v, label: v }))}
            onChange={namespace => onChange({ ...filters, namespace })}
          />
          <FilterSelect
            className="w-full"
            placeholder="All environments"
            value={filters.environment}
            options={facetOptions(applications, filters, "environment").map(v => ({ value: v, label: v }))}
            onChange={environment => onChange({ ...filters, environment })}
          />
          <FilterSelect
            className="w-full"
            placeholder="All services"
            value={filters.service}
            options={facetOptions(applications, filters, "service").map(v => ({ value: v, label: v }))}
            onChange={service => onChange({ ...filters, service })}
          />
        </FilterGroup>

        <FilterGroup label="TIME">
          <TimeFilterControl
            layout="column"
            value={filters}
            onChange={time => onChange({ ...filters, ...EMPTY_TIME, ...time })}
          />
        </FilterGroup>

        <FilterGroup label="SEVERITY">
          <FilterSelect
            className="w-full"
            placeholder="All severities"
            value={filters.severityMin?.toString()}
            options={severityFilterOptions
              .filter(o => o.value > 0)
              .map(o => ({ value: o.value.toString(), label: o.label }))}
            onChange={value =>
              onChange({ ...filters, severityMin: value === undefined ? undefined : Number(value) })
            }
          />
        </FilterGroup>

        <FilterGroup label="MESSAGE">
          <form
            className="flex flex-col gap-2"
            onSubmit={event => {
              event.preventDefault();
              onChange({ ...filters, q: search || undefined });
            }}
          >
            <Input
              placeholder="Search in message…"
              value={search}
              onChange={event => setSearch(event.target.value)}
            />
            <Button type="submit" variant="secondary" size="sm" className="w-full">
              Search
            </Button>
          </form>
        </FilterGroup>

        <FilterGroup label="ATTRIBUTES">
          <AttributeFilterBar
            layout="column"
            signal="logs"
            source={filters}
            filters={filters.filters ?? []}
            onChange={next => onChange({ ...filters, filters: next.length > 0 ? next : undefined })}
          />
        </FilterGroup>
      </FilterRail>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex items-center gap-3.5 px-5 py-3">
          <h1 className="text-sm font-semibold tracking-[-0.1px]">Log records</h1>
          {live && (
            <span className="flex items-center gap-1.5 font-mono text-[11px] text-primary">
              <span className="size-1.5 animate-[bpulse_1.6s_ease-in-out_infinite] rounded-full bg-primary" />
              refreshing every 5 s
            </span>
          )}
          {/* Live is not a filter — it is how often this list refetches, so it lives on the list. */}
          <div className="ml-auto flex items-center gap-3">
            <span className="font-mono text-[11.5px] text-[#6E86A0]">{counted}</span>
            <Button
              type="button"
              variant={live ? "default" : "outline"}
              size="sm"
              onClick={() => setLive(!live)}
            >
              {live ? "Live ●" : "Live"}
            </Button>
          </div>
        </div>

        {/* Pinned above the scroll, never inside it: the Volume is the frame of reference for
            where in time you are, and it is needed most while paging back through older records. */}
        <LogVolumeChart
          filters={filters}
          live={live}
          onNarrow={time => {
            // Narrowing to a stretch of the past is the opposite of watching what arrives.
            setLive(false);
            onChange({ ...filters, ...time });
          }}
        />

        <div className="min-h-0 flex-1 overflow-auto">
          <div
            className={`${GRID} sticky top-0 z-10 h-[30px] border-y border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
          >
            <span />
            <span>TIME</span>
            <span>SEVERITY</span>
            <span>SERVICE</span>
            <span>TENANT</span>
            <span>MESSAGE</span>
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
                <p className="text-[#8CA1B8]">{emptyStateMessage("log records", filters)}</p>
                {widerPresets(filters).length > 0 && (
                  <div className="flex items-center gap-2">
                    <span className="text-[#6E86A0] text-xs">Widen to</span>
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

        <div className="flex items-center gap-3 border-t border-[#17293D] px-5 py-2.5">
          <Button
            variant="outline"
            size="sm"
            disabled={!logs.hasNextPage || logs.isFetchingNextPage}
            onClick={() => logs.fetchNextPage()}
          >
            {logs.isFetchingNextPage ? "Loading…" : logs.hasNextPage ? "Load older" : "No older records"}
          </Button>
          <span className="font-mono text-[11px] text-[#6E86A0]">{counted}</span>
        </div>
      </div>

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
    </div>
  );
}
