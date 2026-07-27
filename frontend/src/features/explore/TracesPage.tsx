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

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-center gap-2">
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

      <div className="overflow-auto rounded-md border">
        <table className="w-full text-sm">
          <thead className="bg-muted text-left text-muted-foreground">
            <tr>
              <th className="px-3 py-2 font-medium">Root span</th>
              <th className="px-3 py-2 font-medium">Service</th>
              <th className="px-3 py-2 font-medium">Started</th>
              <th className="px-3 py-2 font-medium text-right">Duration</th>
              <th className="px-3 py-2 font-medium text-right">Spans</th>
              <th className="px-3 py-2 font-medium">Status</th>
            </tr>
          </thead>
          <tbody data-testid="trace-rows">
            {(traces.data?.items ?? []).map(trace => (
              <tr key={trace.traceId} className="border-t hover:bg-accent">
                <td className="px-3 py-1.5">
                  <Link
                    to="/traces/$traceId"
                    params={{ traceId: trace.traceId }}
                    className="font-medium underline-offset-2 hover:underline"
                  >
                    {trace.rootName ?? trace.traceId}
                  </Link>
                </td>
                <td className="px-3 py-1.5">{trace.rootService}</td>
                <td className="whitespace-nowrap px-3 py-1.5 font-mono text-xs">
                  {formatTime(trace.startTime)}
                </td>
                <td className="px-3 py-1.5 text-right font-mono text-xs">
                  {Number(trace.durationMs).toFixed(0)} ms
                </td>
                <td className="px-3 py-1.5 text-right">{trace.spanCount}</td>
                <td className="px-3 py-1.5">
                  {trace.hasError ? (
                    <span className="rounded bg-red-500/15 px-1.5 py-0.5 text-xs font-medium text-red-500">
                      ERROR
                    </span>
                  ) : (
                    <span className="text-xs text-muted-foreground">OK</span>
                  )}
                </td>
              </tr>
            ))}
            {(traces.data?.items.length ?? 0) === 0 && !traces.isPending && (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-muted-foreground">
                  No traces match the current filters.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
