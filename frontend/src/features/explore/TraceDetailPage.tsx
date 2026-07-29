import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";

import { api, type TraceSpan } from "@/api/client";
import { useCatalog } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { formatTime } from "@/lib/format";

import { removeFilter, upsertFilter, type AttributeFilter } from "./attributeFilters";
import { AttributeLeafList } from "./AttributeLeafList";
import { JsonBlock } from "./LogDetailPanel";
import { serviceLabels } from "./sourceFilter";
import type { TraceFilters } from "./TracesPage";
import { layoutWaterfall } from "./waterfall";

const KIND_LABELS = ["UNSPECIFIED", "INTERNAL", "SERVER", "CLIENT", "PRODUCER", "CONSUMER"];

const GRID = "grid grid-cols-[300px_1fr_90px] items-center gap-3 px-5";

export function TraceDetailPage(props: { traceId: string; listFilters: TraceFilters }) {
  const { listFilters } = props;
  const [selected, setSelected] = useState<TraceSpan | null>(null);
  const navigate = useNavigate();
  const catalog = useCatalog();
  const labels = serviceLabels(catalog.data?.applications ?? []);

  // A span magnifier filters the traces LIST — apply the chip to the carried filters and go back.
  const toggle = (filter: AttributeFilter, active: boolean) => {
    const current = listFilters.filters ?? [];
    const next = active ? removeFilter(current, filter) : upsertFilter(current, filter);
    void navigate({
      to: "/traces",
      search: { ...listFilters, filters: next.length > 0 ? next : undefined },
    });
  };

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

  const spans = trace.data.spans;
  const rows = layoutWaterfall(spans);
  const totalMs =
    spans.length === 0
      ? 0
      : Math.max(...spans.map(s => Date.parse(s.endTime))) -
        Math.min(...spans.map(s => Date.parse(s.startTime)));
  const errorCount = spans.filter(s => s.statusCode === 2).length;

  return (
    <div className="flex h-full min-h-0">
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex items-center gap-3.5 border-b border-[#17293D] bg-[#0B1826] px-[22px] py-3.5">
          <Link
            to="/traces"
            search={listFilters}
            className="text-[12.5px] text-muted-foreground hover:text-foreground"
          >
            Traces
          </Link>
          <span className="text-[#2C4560]">/</span>
          <h1 className="min-w-0 truncate font-mono text-[13px] text-[#B6C8DA]">{props.traceId}</h1>
          <span className="ml-auto whitespace-nowrap font-mono text-[11.5px] text-[#6E86A0]">
            {totalMs.toFixed(0)} ms · {spans.length} spans
            {errorCount > 0 && (
              <>
                {" · "}
                <span className="text-[#F0685A]">
                  {errorCount} {errorCount === 1 ? "error" : "errors"}
                </span>
              </>
            )}
          </span>
          <Button asChild variant="secondary" size="sm">
            <Link to="/" search={{ traceId: props.traceId }}>
              View correlated logs
            </Link>
          </Button>
        </div>

        <div className="min-h-0 flex-1 overflow-auto" data-testid="waterfall">
          <div
            className={`${GRID} sticky top-0 z-10 h-[30px] border-y border-[#17293D] bg-background font-mono text-[10px] tracking-[0.12em] text-[#5F7590]`}
          >
            <span>SPAN</span>
            <span>TIMELINE · 0 → {totalMs.toFixed(0)} ms</span>
            <span className="text-right">DURATION</span>
          </div>
          {rows.map(row => {
            const isSelected = selected?.id === row.span.id;
            const isError = row.span.statusCode === 2;
            return (
              <button
                key={row.span.id}
                type="button"
                onClick={() => setSelected(row.span)}
                className={`${GRID} h-[34px] w-full border-b border-[#101F31] text-left ${
                  isSelected
                    ? "bg-[rgba(233,164,60,0.09)] shadow-[inset_2px_0_0_#E9A43C] hover:bg-[rgba(233,164,60,0.14)]"
                    : "hover:bg-[#12243A]"
                }`}
              >
                <span
                  className={`truncate text-[12.5px] ${
                    isSelected
                      ? "text-[#F6E3C4]"
                      : row.depth === 0
                        ? "text-foreground"
                        : "text-[#C6D6E6]"
                  }`}
                  style={{ paddingLeft: `${row.depth * 16}px` }}
                  title={row.span.name}
                >
                  {isError && <span className="mr-1 text-[10px] text-[#F0685A]">●</span>}
                  {row.span.name}
                </span>
                <span className="relative h-4 rounded bg-[#16283C]">
                  <span
                    className={`absolute inset-y-0 rounded ${isError ? "bg-destructive" : "bg-[#4A93C8]"}`}
                    style={{ left: `${row.offsetPercent}%`, width: `${row.widthPercent}%` }}
                  />
                </span>
                <span
                  className={`text-right font-mono text-[11px] ${isSelected ? "text-[#C6D6E6]" : "text-[#8CA1B8]"}`}
                >
                  {row.durationMs.toFixed(1)} ms
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {selected !== null && (
        <aside className="flex w-96 shrink-0 flex-col gap-[18px] overflow-auto border-l border-[#17293D] bg-[#0B1826] px-5 py-4">
          <div className="flex items-center gap-2">
            <h2 className="min-w-0 truncate text-sm font-semibold tracking-[-0.1px]" title={selected.name}>
              {selected.name}
            </h2>
            <button
              type="button"
              aria-label="Close"
              className="ml-auto rounded-[5px] px-1.5 py-0.5 text-[13px] text-muted-foreground hover:bg-[#16283C] hover:text-foreground"
              onClick={() => setSelected(null)}
            >
              ✕
            </button>
          </div>
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-[5px]">
            <DetailRow label="Span" value={selected.spanId} />
            <DetailRow label="Parent" value={selected.parentSpanId ?? "—"} />
            <DetailRow label="Kind" value={KIND_LABELS[Number(selected.kind)] ?? String(selected.kind)} />
            <DetailRow label="Service" value={labels.get(selected.serviceId) ?? "—"} />
            <DetailRow label="Start" value={formatTime(selected.startTime)} />
            <DetailRow
              label="Status"
              value={selected.statusCode === 2 ? `ERROR ${selected.statusMessage ?? ""}` : "OK"}
              valueClass={selected.statusCode === 2 ? "text-[#F0685A]" : undefined}
            />
          </dl>
          <section className="grid gap-1.5">
            <h3 className="font-mono text-[10px] uppercase tracking-[0.12em] text-[#5F7590]">
              Attributes
            </h3>
            <AttributeLeafList
              attributes={selected.attributes}
              scope="attribute"
              filters={listFilters.filters ?? []}
              onToggle={toggle}
            />
          </section>
          <section className="grid gap-1.5">
            <h3 className="font-mono text-[10px] uppercase tracking-[0.12em] text-[#5F7590]">
              Resource
            </h3>
            <AttributeLeafList
              attributes={selected.resourceAttributes}
              scope="resource"
              filters={listFilters.filters ?? []}
              onToggle={toggle}
            />
          </section>
          <section className="grid gap-1.5">
            <h3 className="font-mono text-[10px] uppercase tracking-[0.12em] text-[#5F7590]">
              Events
            </h3>
            <JsonBlock value={selected.events} />
          </section>
        </aside>
      )}
    </div>
  );
}

function DetailRow(props: { label: string; value: string; valueClass?: string }) {
  return (
    <>
      <dt className="text-[12.5px] text-muted-foreground">{props.label}</dt>
      <dd className={`break-all font-mono text-[11.5px] leading-5 ${props.valueClass ?? "text-[#DCE8F3]"}`}>
        {props.value}
      </dd>
    </>
  );
}
