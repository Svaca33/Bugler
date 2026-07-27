import { useInfiniteQuery } from "@tanstack/react-query";
import { useState } from "react";

import { api, type LogRecord } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import { FilterSelect } from "./FilterSelect";
import { formatTime, tenantOf } from "./format";
import { LogDetailPanel } from "./LogDetailPanel";
import { severityClass, severityFilterOptions, severityLabel } from "./severity";

export interface LogFilters {
  applicationId?: string;
  instanceId?: string;
  severityMin?: number;
  q?: string;
  tenant?: string;
  traceId?: string;
}

const PAGE_SIZE = 100;

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
            instanceId: filters.instanceId,
            severityMin: filters.severityMin,
            q: filters.q,
            tenant: filters.tenant,
            traceId: filters.traceId,
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
  const instances =
    applications.find(a => a.id === filters.applicationId)?.instances ??
    applications.flatMap(a => a.instances);

  return (
    <div className="flex h-full min-h-0">
      <div className="flex min-w-0 flex-1 flex-col gap-3 p-4">
        <form
          className="flex flex-wrap items-center gap-2"
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
              onChange({ ...filters, applicationId, instanceId: undefined })
            }
          />
          <FilterSelect
            placeholder="All instances"
            value={filters.instanceId}
            options={instances.map(i => ({ value: i.id, label: i.name }))}
            onChange={instanceId => onChange({ ...filters, instanceId })}
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
          <Input
            className="w-36"
            placeholder="Tenant"
            value={filters.tenant ?? ""}
            onChange={event => onChange({ ...filters, tenant: event.target.value || undefined })}
          />
          <Button type="submit" variant="secondary" size="sm">
            Search
          </Button>
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

        <div className="min-h-0 flex-1 overflow-auto rounded-md border">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-muted text-left text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">Time</th>
                <th className="px-3 py-2 font-medium">Severity</th>
                <th className="px-3 py-2 font-medium">Service</th>
                <th className="px-3 py-2 font-medium">Tenant</th>
                <th className="px-3 py-2 font-medium">Message</th>
              </tr>
            </thead>
            <tbody data-testid="log-rows">
              {items.map(log => (
                <tr
                  key={log.id}
                  className="cursor-pointer border-t hover:bg-accent"
                  onClick={() => setSelected(log)}
                >
                  <td className="whitespace-nowrap px-3 py-1.5 font-mono text-xs">
                    {formatTime(log.timestamp)}
                  </td>
                  <td
                    className={`px-3 py-1.5 font-mono text-xs ${severityClass(Number(log.severityNumber))}`}
                  >
                    {log.severityText || severityLabel(Number(log.severityNumber))}
                  </td>
                  <td className="px-3 py-1.5">{log.serviceName}</td>
                  <td className="px-3 py-1.5">{tenantOf(log)}</td>
                  <td className="max-w-0 truncate px-3 py-1.5" style={{ width: "60%" }}>
                    {log.body}
                  </td>
                </tr>
              ))}
              {items.length === 0 && !logs.isPending && (
                <tr>
                  <td colSpan={5} className="px-3 py-8 text-center text-muted-foreground">
                    No log records match the current filters.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="sm"
            disabled={!logs.hasNextPage || logs.isFetchingNextPage}
            onClick={() => logs.fetchNextPage()}
          >
            {logs.isFetchingNextPage ? "Loading…" : logs.hasNextPage ? "Load older" : "No older records"}
          </Button>
          <span className="text-xs text-muted-foreground">{items.length} records</span>
        </div>
      </div>

      {selected !== null && <LogDetailPanel log={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}
