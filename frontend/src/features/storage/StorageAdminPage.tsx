import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import type { ColumnDef } from "@tanstack/react-table";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { DataTable } from "@/components/ui/data-table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useT } from "@/i18n";
import { formatBytes } from "@/lib/format";

import {
  RATE_UNITS,
  ratePer,
  rateStep,
  settledBytes,
  totalFootprint,
  type KindStorage,
  type RateUnit,
  type StorageRow,
} from "./storage";

/**
 * The admin's ledger of what the stored telemetry costs: every registered Service, priced in
 * bytes beside the Effective Retention that bounds it, costliest first until a heading says
 * otherwise. Bytes marked ≈ are estimates — Services share tables, so the report divides what
 * the tables really hold — and the Ingest Rate is measured over the last day, whatever unit it
 * is quoted in.
 */
export function StorageAdminPage() {
  const t = useT();
  const report = useStorageReport();
  // Storage speaks of disk rather than of anybody's reading (ADR 0017), so a lens over one
  // person's view has no business narrowing it.
  const catalog = useCatalog({ scope: "all" });
  const [unit, setUnit] = useState<RateUnit>("day");

  const names = useMemo(() => {
    const map = new Map<string, string>();
    for (const application of catalog.data?.applications ?? []) {
      for (const service of application.services) {
        map.set(
          service.id,
          `${application.name} · ${service.namespace}/${service.environment} · ${service.name}`,
        );
      }
    }
    return map;
  }, [catalog.data]);

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex items-end gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-[17px] font-semibold tracking-[-0.3px]">{t.storage.title}</h2>
          <p className="text-[12.5px] text-[#8CA1B8]">{t.storage.subtitle}</p>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Select value={unit} onValueChange={value => setUnit(value as RateUnit)}>
            <SelectTrigger size="sm" className="w-[196px]" aria-label={t.storage.rateUnitAria}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {RATE_UNITS.map(step => (
                <SelectItem key={step.unit} value={step.unit}>
                  {t.storage.rateUnit[step.unit]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="ghost"
            size="sm"
            className="h-8 px-2.5 text-xs"
            disabled={report.isFetching}
            onClick={() => void report.refetch()}
          >
            {report.isFetching ? t.storage.measuring : t.storage.refresh}
          </Button>
        </div>
      </div>

      {report.data !== undefined && (
        <p className="font-mono text-[12px] text-[#B6C8DA]">
          {t.storage.onDisk(
            formatBytes(report.data.logTableBytes),
            formatBytes(report.data.traceTableBytes),
            formatBytes(report.data.logTableBytes + report.data.traceTableBytes),
          )}
          <span className="text-[#5F7590]"> {t.storage.onDiskIncluded}</span>
        </p>
      )}

      {report.isPending ? (
        <p className="text-[12.5px] text-[#8CA1B8]">{t.storage.measuringTables}</p>
      ) : report.isError ? (
        <div className="flex items-center gap-3">
          <p className="text-[12.5px] text-[#E5544A]">{t.storage.reportFailed}</p>
          <Button variant="outline" size="sm" className="h-7 px-2 text-xs" onClick={() => void report.refetch()}>
            {t.storage.tryAgain}
          </Button>
        </div>
      ) : report.data.services.length === 0 ? (
        <p className="text-[12.5px] text-[#8CA1B8]">{t.storage.nothingRegistered}</p>
      ) : (
        <StorageTable
          rows={byServiceId(report.data.services)}
          names={names}
          unit={unit}
          windowMs={report.data.windowMs}
        />
      )}
    </div>
  );
}

/** The rows' incoming order is what ties fall back to in every column — keep it deterministic. */
function byServiceId(services: StorageRow[]): StorageRow[] {
  return [...services].sort((a, b) => a.serviceId.localeCompare(b.serviceId));
}

function StorageTable(props: {
  rows: StorageRow[];
  names: Map<string, string>;
  unit: RateUnit;
  windowMs: number;
}) {
  const t = useT();
  const { names, unit, windowMs } = props;
  const step = rateStep(unit);

  const columns = useMemo<ColumnDef<StorageRow>[]>(() => {
    const label = (row: StorageRow) => names.get(row.serviceId) ?? row.serviceId;
    // "per week (projected)" → "PER WEEK*" — the projected suffix collapses into the asterisk.
    const ingestUnit = t.storage
      .rateUnit[unit].replace(t.storage.projectedSuffix, "*")
      .toUpperCase();
    return [
      {
        id: "service",
        accessorFn: label,
        header: t.storage.columns.service,
        sortDescFirst: false,
        sortingFn: (a, b) => label(a.original).localeCompare(label(b.original)),
        meta: { headerClassName: "w-full pl-4", cellClassName: "w-full pl-4 text-xs" },
        cell: context => {
          const row = context.row.original;
          return names.get(row.serviceId) ?? (
            <span className="text-[#4A6480]">{row.serviceId}</span>
          );
        },
      },
      {
        id: "logs",
        accessorFn: row => row.logs.footprintBytes,
        header: t.storage.columns.logs,
        sortDescFirst: true,
        meta: { headerClassName: "text-right", cellClassName: "text-right" },
        cell: context => <ByteValue bytes={context.row.original.logs.footprintBytes} />,
      },
      {
        id: "traces",
        accessorFn: row => row.traces.footprintBytes,
        header: t.storage.columns.traces,
        sortDescFirst: true,
        meta: { headerClassName: "text-right", cellClassName: "text-right" },
        cell: context => <ByteValue bytes={context.row.original.traces.footprintBytes} />,
      },
      {
        id: "total",
        accessorFn: totalFootprint,
        header: t.storage.columns.total,
        sortDescFirst: true,
        meta: { headerClassName: "text-right", cellClassName: "text-right" },
        cell: context => <ByteValue bytes={totalFootprint(context.row.original)} emphasis />,
      },
      {
        id: "kept",
        accessorFn: row => row.logs.retentionDays,
        header: t.storage.columns.kept,
        sortDescFirst: true,
        // The pair the cell shows, read left to right: log retention first, traces break ties.
        sortingFn: (a, b) =>
          a.original.logs.retentionDays - b.original.logs.retentionDays ||
          a.original.traces.retentionDays - b.original.traces.retentionDays,
        meta: {
          headerClassName: "text-right",
          headerTitle: t.storage.retentionPairTitle,
          cellClassName: "text-right text-[#B6C8DA]",
        },
        cell: context => {
          const row = context.row.original;
          return (
            <span title={t.storage.retentionPairTitle}>
              {row.logs.retentionDays} d / {row.traces.retentionDays} d
            </span>
          );
        },
      },
      {
        id: "ingest",
        accessorFn: row => row.logs.windowBytes + row.traces.windowBytes,
        header: t.storage.columns.ingest(ingestUnit),
        sortDescFirst: true,
        meta: { headerClassName: "text-right", cellClassName: "text-right" },
        cell: context => {
          const row = context.row.original;
          const logRate = ratePer(row.logs.windowBytes, windowMs, unit);
          const traceRate = ratePer(row.traces.windowBytes, windowMs, unit);
          return (
            <ByteValue
              bytes={logRate + traceRate}
              title={t.storage.ingestSplit(formatBytes(logRate), formatBytes(traceRate))}
            />
          );
        },
      },
      {
        id: "settles",
        accessorFn: row => settledBytes(row, windowMs),
        header: t.storage.columns.settlesAt,
        sortDescFirst: true,
        meta: {
          headerClassName: "pr-4 text-right",
          headerTitle: t.storage.settlesTitle,
          cellClassName: "pr-4 text-right",
        },
        cell: context => {
          const row = context.row.original;
          return (
            <ByteValue
              bytes={settledBytes(row, windowMs)}
              emphasis
              title={t.storage.settledAtPace(row.logs.retentionDays, row.traces.retentionDays)}
            />
          );
        },
      },
    ];
  }, [names, unit, t, windowMs]);

  return (
    <div
      data-testid="storage-rows"
      className="overflow-hidden rounded-[11px] border border-[#1E344C] bg-card"
    >
      <DataTable
        columns={columns}
        data={props.rows}
        defaultSort={[{ id: "total", desc: true }]}
        persistKey="bugler.admin.storage-sort"
        rowTestId="storage-row"
      />
      {step.projected && (
        <p className="border-t border-[#101F31] px-4 py-2 text-[11px] text-[#5F7590]">
          {t.storage.projectedFootnote}
        </p>
      )}
    </div>
  );
}

function ByteValue(props: { bytes: number; emphasis?: boolean; title?: string }) {
  const empty = props.bytes <= 0;
  return (
    <span
      title={props.title}
      className={
        empty ? "text-[#4A6480]" : props.emphasis === true ? "text-[#DCE8F3]" : "text-[#B6C8DA]"
      }
    >
      {empty ? "—" : `≈ ${formatBytes(props.bytes)}`}
    </span>
  );
}

function useStorageReport() {
  return useQuery({
    queryKey: ["admin", "storage"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/admin/storage");
      if (error !== undefined || data === undefined) {
        throw new Error("Failed to load the storage report");
      }
      return {
        windowStart: data.windowStart,
        windowEnd: data.windowEnd,
        windowMs: Date.parse(data.windowEnd) - Date.parse(data.windowStart),
        logTableBytes: Number(data.logTableBytes),
        traceTableBytes: Number(data.traceTableBytes),
        services: data.services.map(
          (service): StorageRow => ({
            serviceId: service.serviceId,
            logs: coerceKind(service.logs),
            traces: coerceKind(service.traces),
          }),
        ),
      };
    },
    // The report walks whole indexes server-side; nothing here is worth re-walking them for
    // behind the reader's back. Refresh is a button, not a poll.
    staleTime: Infinity,
    retry: false,
  });
}

/** The generated client types integers as `number | string`; numbers come out here. */
function coerceKind(kind: {
  footprintBytes: number | string;
  retentionDays: number | string;
  windowBytes: number | string;
}): KindStorage {
  return {
    footprintBytes: Number(kind.footprintBytes),
    retentionDays: Number(kind.retentionDays),
    windowBytes: Number(kind.windowBytes),
  };
}
