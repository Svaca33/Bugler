import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";

import { FilterSelect } from "./FilterSelect";
import { formatTime } from "./format";

export interface TraceFilters {
  applicationId?: string;
  instanceId?: string;
  errorsOnly?: boolean;
}

const GRID = "grid grid-cols-[1fr_150px_200px_96px_66px_74px] items-center gap-4 px-5";

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
            instanceId: filters.instanceId,
            errorsOnly: filters.errorsOnly,
            limit: 100,
          },
        },
      });
      if (error !== undefined) throw new Error("Failed to load traces");
      return data;
    },
  });

  const applications = catalog.data?.applications ?? [];
  const instances =
    applications.find(a => a.id === filters.applicationId)?.instances ??
    applications.flatMap(a => a.instances);
  const items = traces.data?.items ?? [];

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex flex-wrap items-center gap-2 border-b border-[#17293D] bg-[#0B1826] px-[22px] py-3.5">
        <FilterSelect
          placeholder="All applications"
          value={filters.applicationId}
          options={applications.map(a => ({ value: a.id, label: a.name }))}
          onChange={applicationId => onChange({ ...filters, applicationId, instanceId: undefined })}
        />
        <FilterSelect
          placeholder="All instances"
          value={filters.instanceId}
          options={instances.map(i => ({ value: i.id, label: i.name }))}
          onChange={instanceId => onChange({ ...filters, instanceId })}
        />
        <Button
          variant={filters.errorsOnly ? "default" : "outline"}
          size="sm"
          onClick={() => onChange({ ...filters, errorsOnly: filters.errorsOnly ? undefined : true })}
        >
          Errors only
        </Button>
      </div>

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
                      className="font-mono text-xs text-[#B6C8DA] underline-offset-2 hover:underline"
                    >
                      {trace.traceId}
                    </Link>
                  )}
                </span>
                <span className="truncate font-mono text-[11.5px] text-[#A9BDD1]">
                  {trace.rootService}
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
            <p className="py-16 text-center text-[#8CA1B8]">No traces match the current filters.</p>
          )}
        </div>
      </div>
    </div>
  );
}
