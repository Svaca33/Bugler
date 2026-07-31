import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { formatTime } from "@/lib/format";

import { AttributeFilterBar } from "./AttributeFilterBar";
import { toQueryParams, type AttributeFilter } from "./attributeFilters";
import { FilterGroup, FilterRail } from "@/components/ui/filter-rail";
import { FilterSelect } from "@/components/ui/filter-select";
import { facetOptions, serviceLabels, type SourceFilters } from "@/lib/sourceFilter";
import { EMPTY_TIME, emptyStateMessage, widerPresets, type TimeFilterValue } from "./timeFilter";
import { TimeFilterControl } from "./TimeFilterControl";

export interface TraceFilters extends SourceFilters, TimeFilterValue {
  errorsOnly?: boolean;
  filters?: AttributeFilter[];
}

const GRID = "grid grid-cols-[1fr_196px_200px_96px_66px_74px] items-center gap-4 px-5";

export function TracesPage(props: { filters: TraceFilters; onChange: (filters: TraceFilters) => void }) {
  const { filters, onChange } = props;
  const catalog = useCatalog();

  const traces = useQuery({
    queryKey: ["traces", filters],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/traces", {
        params: {
          query: {
            applicationId: filters.applicationId,
            namespace: filters.namespace,
            environment: filters.environment,
            service: filters.service,
            range: filters.range,
            from: filters.from,
            to: filters.to,
            errorsOnly: filters.errorsOnly,
            ...toQueryParams(filters.filters ?? []),
            limit: 100,
          },
        },
      });
      if (error !== undefined) throw new Error("Failed to load traces");
      return data;
    },
  });

  const applications = catalog.data?.applications ?? [];
  const labels = serviceLabels(applications);
  const items = traces.data?.items ?? [];

  return (
    <div className="flex h-full min-h-0">
      <FilterRail
        canClear={Object.values(filters).some(value => value !== undefined)}
        onClear={() => onChange({})}
      >
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

        <FilterGroup label="STATUS">
          <Button
            variant={filters.errorsOnly ? "default" : "outline"}
            size="sm"
            className="w-full"
            onClick={() => onChange({ ...filters, errorsOnly: filters.errorsOnly ? undefined : true })}
          >
            Errors only
          </Button>
        </FilterGroup>

        <FilterGroup label="ATTRIBUTES">
          <AttributeFilterBar
            layout="column"
            signal="traces"
            source={filters}
            filters={filters.filters ?? []}
            onChange={next => onChange({ ...filters, filters: next.length > 0 ? next : undefined })}
          />
        </FilterGroup>
      </FilterRail>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex items-center gap-3.5 px-5 py-3">
          <h1 className="text-sm font-semibold tracking-[-0.1px]">Traces</h1>
          <span className="ml-auto font-mono text-[11.5px] text-[#6E86A0]">{items.length} traces</span>
        </div>

        <div className="min-h-0 flex-1 overflow-auto">
          <div
            className={`${GRID} sticky top-0 z-10 h-[30px] border-y border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
          >
            <span>ROOT SPAN</span>
            <span>SERVICE</span>
            <span>STARTED</span>
            <span className="text-right">DURATION</span>
            <span className="text-right">SPANS</span>
            <span>STATUS</span>
          </div>
          <div data-testid="trace-rows">
            {items.map(trace => {
              const slow = Number(trace.durationMs) >= 500;
              return (
                <div
                  key={trace.traceId}
                  className={`${GRID} h-[38px] border-b border-[#101F31] hover:bg-[#12243A] ${
                    trace.hasError ? "bg-[rgba(229,84,74,0.07)]" : ""
                  }`}
                >
                  <span className="min-w-0 truncate">
                    {trace.rootName != null ? (
                      <Link
                        to="/traces/$traceId"
                        params={{ traceId: trace.traceId }}
                        search={filters}
                        className={`text-[13px] font-medium underline-offset-2 hover:underline ${
                          trace.hasError ? "text-[#F6C170]" : "text-foreground"
                        }`}
                      >
                        {trace.rootName}
                      </Link>
                    ) : (
                      <Link
                        to="/traces/$traceId"
                        params={{ traceId: trace.traceId }}
                        search={filters}
                        className="font-mono text-xs text-[#B6C8DA] underline-offset-2 hover:underline"
                      >
                        {trace.traceId}
                      </Link>
                    )}
                  </span>
                  <span className="truncate font-mono text-[11.5px] text-[#A9BDD1]">
                    {(trace.rootServiceId != null ? labels.get(trace.rootServiceId) : undefined) ?? "â€”"}
                  </span>
                  <span className="whitespace-nowrap font-mono text-[11.5px] text-[#7D93AA]">
                    {formatTime(trace.startTime)}
                  </span>
                  <span
                    className={`text-right font-mono text-[11.5px] ${slow ? "text-primary" : "text-[#B6C8DA]"}`}
                  >
                    {Number(trace.durationMs).toFixed(0)} ms
                  </span>
                  <span className="text-right font-mono text-xs text-[#B6C8DA]">{trace.spanCount}</span>
                  <span>
                    {trace.hasError ? (
                      <span className="rounded-[5px] bg-[rgba(229,84,74,0.15)] px-[7px] py-0.5 font-mono text-[10.5px] font-medium text-[#F0685A]">
                        ERROR
                      </span>
                    ) : (
                      <span className="font-mono text-[11px] text-[#6E86A0]">OK</span>
                    )}
                  </span>
                </div>
              );
            })}
            {items.length === 0 && !traces.isPending && (
              <div className="flex flex-col items-center gap-3 py-16">
                <p className="text-[#8CA1B8]">{emptyStateMessage("traces", filters)}</p>
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
      </div>
    </div>
  );
}
