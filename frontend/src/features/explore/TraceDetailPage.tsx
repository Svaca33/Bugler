import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useState } from "react";

import { api, type TraceSpan } from "@/api/client";
import { Button } from "@/components/ui/button";

import { formatTime } from "./format";
import { JsonBlock } from "./LogDetailPanel";
import { layoutWaterfall } from "./waterfall";

const KIND_LABELS = ["UNSPECIFIED", "INTERNAL", "SERVER", "CLIENT", "PRODUCER", "CONSUMER"];

export function TraceDetailPage(props: { traceId: string }) {
  const [selected, setSelected] = useState<TraceSpan | null>(null);

  const trace = useQuery({
    queryKey: ["trace", props.traceId],
    queryFn: async () => {
      const { data, response } = await api.GET("/api/traces/{traceId}", {
        params: { path: { traceId: props.traceId } },
      });
      if (response.status === 404) return null;
      if (data === undefined) throw new Error("Failed to load trace");
      return data;
    },
  });

  if (trace.isPending) {
    return <p className="p-6 text-muted-foreground">Loading trace…</p>;
  }

  if (trace.data == null) {
    return <p className="p-6 text-muted-foreground">Trace not found (or not visible to you).</p>;
  }

  const rows = layoutWaterfall(trace.data.spans);

  return (
    <div className="flex h-full min-h-0">
      <div className="flex min-w-0 flex-1 flex-col gap-3 p-4">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-mono text-sm text-muted-foreground">{props.traceId}</h1>
          <Button asChild variant="secondary" size="sm">
            <Link to="/" search={{ traceId: props.traceId }}>
              View correlated logs
            </Link>
          </Button>
        </div>

        <div className="min-h-0 flex-1 overflow-auto rounded-md border" data-testid="waterfall">
          {rows.map(row => (
            <button
              key={row.span.id}
              type="button"
              onClick={() => setSelected(row.span)}
              className={`grid w-full grid-cols-[280px_1fr_90px] items-center gap-2 border-b px-3 py-1 text-left text-sm hover:bg-accent ${
                selected?.id === row.span.id ? "bg-accent" : ""
              }`}
            >
              <span
                className="truncate"
                style={{ paddingLeft: `${row.depth * 16}px` }}
                title={row.span.name}
              >
                {row.span.statusCode === 2 && <span className="mr-1 text-red-500">●</span>}
                {row.span.name}
              </span>
              <span className="relative h-4 rounded bg-muted">
                <span
                  className={`absolute inset-y-0 rounded ${
                    row.span.statusCode === 2 ? "bg-red-500" : "bg-sky-500"
                  }`}
                  style={{ left: `${row.offsetPercent}%`, width: `${row.widthPercent}%` }}
                />
              </span>
              <span className="text-right font-mono text-xs text-muted-foreground">
                {row.durationMs.toFixed(1)} ms
              </span>
            </button>
          ))}
        </div>
      </div>

      {selected !== null && (
        <aside className="flex w-96 shrink-0 flex-col gap-3 overflow-auto border-l bg-background p-4">
          <div className="flex items-center justify-between">
            <h2 className="truncate text-sm font-semibold">{selected.name}</h2>
            <Button variant="ghost" size="sm" onClick={() => setSelected(null)}>
              ✕
            </Button>
          </div>
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <DetailRow label="Span" value={selected.spanId} />
            <DetailRow label="Parent" value={selected.parentSpanId ?? "—"} />
            <DetailRow label="Kind" value={KIND_LABELS[Number(selected.kind)] ?? String(selected.kind)} />
            <DetailRow label="Service" value={selected.serviceName ?? "—"} />
            <DetailRow label="Start" value={formatTime(selected.startTime)} />
            <DetailRow
              label="Status"
              value={selected.statusCode === 2 ? `ERROR ${selected.statusMessage ?? ""}` : "OK"}
            />
          </dl>
          <section className="grid gap-1">
            <h3 className="text-xs font-medium uppercase text-muted-foreground">Attributes</h3>
            <JsonBlock value={selected.attributes} />
          </section>
          <section className="grid gap-1">
            <h3 className="text-xs font-medium uppercase text-muted-foreground">Events</h3>
            <JsonBlock value={selected.events} />
          </section>
        </aside>
      )}
    </div>
  );
}

function DetailRow(props: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted-foreground">{props.label}</dt>
      <dd className="break-all font-mono text-xs leading-5">{props.value}</dd>
    </>
  );
}
