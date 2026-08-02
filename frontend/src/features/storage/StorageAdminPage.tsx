import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { api } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { formatBytes } from "@/lib/format";

import {
  RATE_UNITS,
  byCost,
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
 * bytes beside the Effective Retention that bounds it, costliest first. Bytes marked ≈ are
 * estimates — Services share tables, so the report divides what the tables really hold — and
 * the Ingest Rate is measured over the last day, whatever unit it is quoted in.
 */
export function StorageAdminPage() {
  const report = useStorageReport();
  const catalog = useCatalog();
  const [unit, setUnit] = useState<RateUnit>("day");

  const names = new Map<string, string>();
  for (const application of catalog.data?.applications ?? []) {
    for (const service of application.services) {
      names.set(
        service.id,
        `${application.name} · ${service.namespace}/${service.environment} · ${service.name}`,
      );
    }
  }

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-auto px-6 py-5">
      <div className="flex items-end gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-[17px] font-semibold tracking-[-0.3px]">Storage</h2>
          <p className="text-[12.5px] text-[#8CA1B8]">
            What each Service's telemetry costs, and how fast it is growing. Bytes marked ≈ are
            estimates; the rate is measured over the last day.
          </p>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Select value={unit} onValueChange={value => setUnit(value as RateUnit)}>
            <SelectTrigger size="sm" className="w-[196px]" aria-label="Ingest rate unit">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {RATE_UNITS.map(step => (
                <SelectItem key={step.unit} value={step.unit}>
                  {step.label}
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
            {report.isFetching ? "Measuring…" : "Refresh"}
          </Button>
        </div>
      </div>

      {report.data !== undefined && (
        <p className="font-mono text-[12px] text-[#B6C8DA]">
          Logs {formatBytes(report.data.logTableBytes)} · Traces{" "}
          {formatBytes(report.data.traceTableBytes)} · together{" "}
          {formatBytes(report.data.logTableBytes + report.data.traceTableBytes)} on disk
          <span className="text-[#5F7590]"> — tables, indexes and TOAST included</span>
        </p>
      )}

      {report.isPending ? (
        <p className="text-[12.5px] text-[#8CA1B8]">
          Measuring the tables — this can take a moment on a loaded instance.
        </p>
      ) : report.isError ? (
        <div className="flex items-center gap-3">
          <p className="text-[12.5px] text-[#E5544A]">
            The storage report did not answer. The instance may be busy.
          </p>
          <Button variant="outline" size="sm" className="h-7 px-2 text-xs" onClick={() => void report.refetch()}>
            Try again
          </Button>
        </div>
      ) : report.data.services.length === 0 ? (
        <p className="text-[12.5px] text-[#8CA1B8]">Nothing is registered yet.</p>
      ) : (
        <StorageTable
          rows={[...report.data.services].sort(byCost)}
          names={names}
          unit={unit}
          windowMs={report.data.windowMs}
        />
      )}
    </div>
  );
}

function StorageTable(props: {
  rows: StorageRow[];
  names: Map<string, string>;
  unit: RateUnit;
  windowMs: number;
}) {
  const heading = "px-1.5 text-left font-normal whitespace-nowrap";
  const unitLabel = rateStep(props.unit);

  return (
    <div
      data-testid="storage-rows"
      className="overflow-x-auto rounded-[11px] border border-[#1E344C] bg-card"
    >
      <table className="w-full border-collapse">
        <thead>
          <tr className="h-8 border-b border-[#17293D] bg-[#0B1826] font-mono text-[10px] tracking-[0.12em] text-[#5F7590]">
            <th className={`${heading} w-full pl-4`}>SERVICE</th>
            <th className={`${heading} text-right`}>LOGS</th>
            <th className={`${heading} text-right`}>TRACES</th>
            <th className={`${heading} text-right`}>TOTAL</th>
            <th className={`${heading} text-right`} title="Log retention / trace retention">
              KEPT
            </th>
            <th className={`${heading} text-right`}>
              INGEST {unitLabel.label.toUpperCase().replace(" (PROJECTED)", "*")}
            </th>
            <th
              className={`${heading} pr-4 text-right`}
              title="Where the Footprint comes to rest if the last day's pace holds, each retention clock on its own"
            >
              SETTLES AT
            </th>
          </tr>
        </thead>
        <tbody>
          {props.rows.map(row => (
            <StorageRowLine
              key={row.serviceId}
              row={row}
              label={props.names.get(row.serviceId)}
              unit={props.unit}
              windowMs={props.windowMs}
            />
          ))}
        </tbody>
      </table>
      {unitLabel.projected && (
        <p className="border-t border-[#101F31] px-4 py-2 text-[11px] text-[#5F7590]">
          * projected from the last day's measurement — nothing is measured beyond it.
        </p>
      )}
    </div>
  );
}

function StorageRowLine(props: {
  row: StorageRow;
  label: string | undefined;
  unit: RateUnit;
  windowMs: number;
}) {
  const { row } = props;
  const logRate = ratePer(row.logs.windowBytes, props.windowMs, props.unit);
  const traceRate = ratePer(row.traces.windowBytes, props.windowMs, props.unit);

  return (
    <tr data-testid="storage-row" className="h-10 border-b border-[#101F31]">
      <td className="w-full py-0 pr-1.5 pl-4 align-middle font-mono text-xs whitespace-nowrap">
        {props.label ?? <span className="text-[#4A6480]">{row.serviceId}</span>}
      </td>
      <ByteCell bytes={row.logs.footprintBytes} />
      <ByteCell bytes={row.traces.footprintBytes} />
      <ByteCell bytes={totalFootprint(row)} emphasis />
      <td
        className="px-1.5 text-right align-middle font-mono text-[11.5px] whitespace-nowrap text-[#B6C8DA]"
        title="Log retention / trace retention"
      >
        {row.logs.retentionDays} d / {row.traces.retentionDays} d
      </td>
      <ByteCell
        bytes={logRate + traceRate}
        title={`logs ≈ ${formatBytes(logRate)} · traces ≈ ${formatBytes(traceRate)}`}
      />
      <ByteCell
        bytes={settledBytes(row, props.windowMs)}
        emphasis
        className="pr-4"
        title={`at the last day's pace, holding logs ${row.logs.retentionDays} d and traces ${row.traces.retentionDays} d`}
      />
    </tr>
  );
}

function ByteCell(props: {
  bytes: number;
  emphasis?: boolean;
  className?: string;
  title?: string;
}) {
  const empty = props.bytes <= 0;
  return (
    <td
      title={props.title}
      className={`px-1.5 text-right align-middle font-mono text-[11.5px] whitespace-nowrap ${
        empty ? "text-[#4A6480]" : props.emphasis === true ? "text-[#DCE8F3]" : "text-[#B6C8DA]"
      } ${props.className ?? ""}`}
    >
      {empty ? "—" : `≈ ${formatBytes(props.bytes)}`}
    </td>
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
