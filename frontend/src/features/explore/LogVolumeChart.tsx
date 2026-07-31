import { useQuery } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

import { api } from "@/api/client";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Chart,
  ChartTooltip,
  ChartTooltipContent,
  ReferenceArea,
  XAxis,
  YAxis,
  type ChartConfig,
} from "@/components/ui/chart";
import { durationMs } from "@/lib/duration";
import { bandsAdmittedBy, SEVERITY_BANDS } from "@/lib/severity";

import { toQueryParams, type AttributeFilter } from "./attributeFilters";
import {
  axisNeedsDate,
  bucketEdge,
  bucketLabel,
  describeBucket,
  describeWidth,
  type VolumeWindow,
} from "./logVolume";
import type { SourceFilters } from "@/lib/sourceFilter";
import { EMPTY_TIME, type TimeFilterValue } from "./timeFilter";

/** Everything the chart narrows by â€” the very same criteria the list under it narrows by. */
export interface VolumeFilters extends SourceFilters, TimeFilterValue {
  severityMin?: number;
  q?: string;
  traceId?: string;
  filters?: AttributeFilter[];
}

const CHART_CONFIG: ChartConfig = Object.fromEntries(
  SEVERITY_BANDS.map(band => [band.key, { label: band.label, color: band.color }]),
);

/** Tall enough for a stack to be readable, short enough not to eat the list. */
const BAR_AREA = "h-[176px]";

export function LogVolumeChart(props: {
  filters: VolumeFilters;
  live: boolean;
  onNarrow: (time: TimeFilterValue) => void;
}) {
  const { filters, live, onNarrow } = props;
  // Both ends are Bucket starts, so committing them is already snapped to Bucket edges.
  const [drag, setDrag] = useState<{ from: string; to: string } | null>(null);

  const volume = useQuery({
    queryKey: ["logs-volume", filters],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/logs/volume", {
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
      if (error !== undefined) throw new Error("Failed to summarize log volume");
      return data;
    },
    refetchInterval: live ? 5000 : false,
    // Holding the last window while the next one loads is what keeps the list from jumping.
    placeholderData: previous => previous,
  });

  if (volume.isError) {
    return (
      <Frame>
        <p className="text-muted-foreground self-center text-xs">
          The window is too wide to summarize. Narrow the Time Filter â€” the list below is unaffected.
        </p>
      </Frame>
    );
  }

  // The first window has nothing to hold on to, so the placeholder keeps the height â€” the list
  // below must not jump â€” and the Spinner says the wait is work rather than emptiness.
  if (volume.data === undefined) {
    return (
      <Frame>
        <div className={`${BAR_AREA} bg-secondary/25 flex items-center justify-center rounded-sm`}>
          <Spinner />
        </div>
      </Frame>
    );
  }

  const { from, to, buckets } = volume.data;
  const resolved: VolumeWindow = { from, to, widthMs: durationMs(volume.data.bucket) ?? 0 };
  const edgeOf = (start: string) => bucketEdge(start, resolved);
  // A Band the severity threshold excludes is empty by construction, so it gets neither a stack
  // segment nor a legend entry â€” the chart shows the list's set and only the list's set.
  const bands = bandsAdmittedBy(filters.severityMin);

  // Holding the previous window is what keeps the list still, but it also makes a slow reload look
  // like nothing happened: the old chart simply sits there for seconds and then swaps. At a
  // retention's width summarizing takes that long, so the wait has to be visible. Not while Live is
  // on â€” that reloads every five seconds by design, and a spinner blinking on every tick is noise.
  const reloading = volume.isFetching && !live;

  const commit = () => {
    const [lower, upper] = drag === null ? [] : [drag.from, drag.to].sort();
    // A drag that never left its Bucket is a click, and a click must not rewrite the Time Filter
    // under a reader who only meant to hover.
    if (lower !== undefined && upper !== undefined && lower !== upper) {
      onNarrow({
        ...EMPTY_TIME,
        from: new Date(lower).toISOString(),
        to: new Date(new Date(upper).getTime() + resolved.widthMs).toISOString(),
      });
    }
    setDrag(null);
  };

  return (
    <Frame
      label={`${formatBoundary(from)} â†’ ${formatBoundary(to)} Â· ${describeWidth(resolved.widthMs)} buckets`}
      legend={bands}
    >
      <div className="relative">
        {reloading && (
          <div className="bg-background/60 absolute inset-0 z-10 flex items-center justify-center">
            <Spinner />
          </div>
        )}
        <Chart config={CHART_CONFIG} className={`${BAR_AREA} w-full cursor-crosshair`}>
          <BarChart
            data={buckets}
            margin={{ top: 4, right: 8, bottom: 0, left: 0 }}
            barCategoryGap={2}
            onMouseDown={(state: { activeLabel?: string | number }) =>
              state.activeLabel !== undefined &&
              setDrag({ from: String(state.activeLabel), to: String(state.activeLabel) })
            }
            onMouseMove={(state: { activeLabel?: string | number }) =>
              drag !== null &&
              state.activeLabel !== undefined &&
              setDrag({ ...drag, to: String(state.activeLabel) })
            }
            onMouseUp={commit}
            onMouseLeave={() => setDrag(null)}
          >
            <CartesianGrid vertical={false} />
            <XAxis
              dataKey="start"
              tickLine={false}
              axisLine={false}
              // A dated label is roughly twice as wide, so the ticks have to stand further apart or
              // Recharts packs them until they collide.
              minTickGap={axisNeedsDate(resolved) ? 84 : 48}
              tickFormatter={(value: string) => bucketLabel(value, resolved)}
              tick={{ fontSize: 10 }}
            />
            <YAxis
              width={38}
              tickLine={false}
              axisLine={false}
              tickCount={3}
              allowDecimals={false}
              tick={{ fontSize: 10 }}
            />
            <ChartTooltip
              cursor={{ fill: "var(--secondary)", fillOpacity: 0.35 }}
              content={
                <ChartTooltipContent
                  labelFormatter={label => (
                    <span className="font-mono text-[11px]">
                      {describeBucket(String(label), resolved, edgeOf(String(label)), filters.to !== undefined)}
                    </span>
                  )}
                />
              }
            />
            {/* Error sits on the baseline: only a segment anchored there keeps a comparable shape. */}
            {bands.map(band => (
              <Bar
                key={band.key}
                dataKey={band.key}
                name={band.key}
                stackId="volume"
                fill={`var(--color-${band.key})`}
                stroke="var(--background)"
                strokeWidth={0.5}
                isAnimationActive={false}
              >
                {buckets.map(bucket => (
                  <Cell key={bucket.start} fillOpacity={edgeOf(bucket.start) === null ? 1 : 0.45} />
                ))}
              </Bar>
            ))}
            {drag !== null && drag.from !== drag.to && (
              <ReferenceArea x1={drag.from} x2={drag.to} fill="var(--primary)" fillOpacity={0.18} />
            )}
          </BarChart>
        </Chart>
      </div>
    </Frame>
  );
}

/**
 * The wait made visible. Local to the chart rather than a kit primitive: everywhere else in Bugler
 * a wait is short enough to say in words ("Loadingâ€¦", "Sendingâ€¦"), and only summarizing a whole
 * retention takes long enough to need something that moves.
 */
function Spinner() {
  return (
    <span
      role="status"
      aria-label="Summarizing volume"
      className="border-muted-foreground/25 border-t-muted-foreground size-5 animate-spin rounded-full border-2"
    />
  );
}

/** Holds the height in every state, so the list below never jumps while a window loads. */
function Frame(props: { label?: string; legend?: typeof SEVERITY_BANDS; children: ReactNode }) {
  return (
    <div className="border-border/60 flex shrink-0 flex-col gap-1 border-b px-5 pt-2 pb-1.5">
      <div className="flex items-center gap-4">
        <span className="text-muted-foreground shrink-0 truncate font-mono text-[10px] tracking-[0.08em]">
        {props.label ?? "Â "}
        </span>
        {/* A key to the colours, not a control: severityMin is a threshold, so a clickable Band
            could only ever mean "and above" â€” a control doing something other than it looks. */}
        <div className="ml-auto flex items-center gap-3">
          {props.legend?.map(band => (
            <span key={band.key} className="flex items-center gap-1.5">
              <span className="size-2 shrink-0 rounded-[2px]" style={{ backgroundColor: band.color }} />
              <span className="text-muted-foreground font-mono text-[10px]">{band.label}</span>
            </span>
          ))}
        </div>
      </div>
      {props.children}
    </div>
  );
}

function formatBoundary(instant: string): string {
  const date = new Date(instant);
  return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
}
