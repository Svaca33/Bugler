import { useInfiniteQuery } from "@tanstack/react-query";
import { useState } from "react";

import { api, type LogRecord } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import { AttributeFilterBar } from "./AttributeFilterBar";
import { toQueryParams, type AttributeFilter } from "./attributeFilters";
import { FilterSelect } from "./FilterSelect";
import { formatTime, tenantOf } from "./format";
import { LogDetailPanel } from "./LogDetailPanel";
import { severityClass, severityFilterOptions, severityLabel, severityRailClass } from "./severity";
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

export function LogsPage(props: { filters: LogFilters; onChange: (filters: LogFilters) => void }) {
  const { filters, onChange } = props;
  const catalog = useCatalog();
  const [live, setLive] = useState(false);
  const [search, setSearch] = useState(filters.q ?? "");
  const [selected, setSelected] = useState<LogRecord | null>(null);

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

  const items = logs.data?.pages.flatMap(page => page.items) ?? [];
  const applications = catalog.data?.applications ?? [];
  const labels = serviceLabels(applications);

  return (
    <div className="flex h-full min-h-0">
      <div className="flex min-w-0 flex-1 flex-col">
        <form
          className="flex flex-wrap items-center gap-2 border-b border-[#17293D] bg-[#0B1826] px-[22px] py-3.5"
          onSubmit={event => {
            event.preventDefault();
            onChange({ ...filters, q: search || undefined });
          }}
        >
          <FilterSelect
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
            placeholder="All namespaces"
            value={filters.namespace}
            options={facetOptions(applications, filters, "namespace").map(v => ({ value: v, label: v }))}
            onChange={namespace => onChange({ ...filters, namespace })}
          />
          <FilterSelect
            placeholder="All environments"
            value={filters.environment}
            options={facetOptions(applications, filters, "environment").map(v => ({ value: v, label: v }))}
            onChange={environment => onChange({ ...filters, environment })}
          />
          <FilterSelect
            placeholder="All services"
            value={filters.service}
            options={facetOptions(applications, filters, "service").map(v => ({ value: v, label: v }))}
            onChange={service => onChange({ ...filters, service })}
          />
          <TimeFilterControl
            value={filters}
            onChange={time => onChange({ ...filters, ...EMPTY_TIME, ...time })}
          />
          <FilterSelect
            placeholder="All severities"
            value={filters.severityMin?.toString()}
            options={severityFilterOptions
              .filter(o => o.value > 0)
              .map(o => ({ value: o.value.toString(), label: o.label }))}
            onChange={value =>
              onChange({ ...filters, severityMin: value === undefined ? undefined : Number(value) })
            }
          />
          <Input
            className="w-56"
            placeholder="Search in message…"
            value={search}
            onChange={event => setSearch(event.target.value)}
          />
          <Button type="submit" variant="secondary" size="sm">
            Search
          </Button>
          <AttributeFilterBar
            signal="logs"
            source={filters}
            filters={filters.filters ?? []}
            onChange={next =>
              onChange({ ...filters, filters: next.length > 0 ? next : undefined })
            }
          />
          <Button
            type="button"
            variant={live ? "default" : "outline"}
            size="sm"
            onClick={() => setLive(!live)}
          >
            {live ? "Live ●" : "Live"}
          </Button>
          {filters.traceId !== undefined && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => onChange({ ...filters, traceId: undefined })}
            >
              Trace: {filters.traceId.slice(0, 8)}… ✕
            </Button>
          )}
        </form>

        <div className="flex items-center gap-3.5 px-5 py-3">
          <h1 className="text-sm font-semibold tracking-[-0.1px]">Log records</h1>
          {live && (
            <span className="flex items-center gap-1.5 font-mono text-[11px] text-primary">
              <span className="size-1.5 animate-[bpulse_1.6s_ease-in-out_infinite] rounded-full bg-primary" />
              refreshing every 5 s
            </span>
          )}
          <span className="ml-auto font-mono text-[11.5px] text-[#6E86A0]">
            {items.length} records
          </span>
        </div>

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
              const isSelected = selected?.id === log.id;
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
                  onClick={() => setSelected(log)}
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
          <span className="font-mono text-[11px] text-[#6E86A0]">{items.length} records</span>
        </div>
      </div>

      {selected !== null && (
        <LogDetailPanel
          log={selected}
          filters={filters.filters ?? []}
          onFiltersChange={next =>
            onChange({ ...filters, filters: next.length > 0 ? next : undefined })
          }
          onClose={() => setSelected(null)}
        />
      )}
    </div>
  );
}
