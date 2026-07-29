import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Chart,
  ChartTooltip,
  ChartTooltipContent,
  XAxis,
  YAxis,
  type ChartConfig,
} from "bugler-frontend";

/** Severity Bands in stacking order, Error first so it sits on the baseline. */
const BANDS = [
  { key: "error", label: "Error", color: "var(--severity-error-fill)" },
  { key: "warn", label: "Warn", color: "var(--severity-warn-fill)" },
  { key: "info", label: "Info", color: "var(--severity-info-fill)" },
  { key: "debug", label: "Debug", color: "var(--severity-debug-fill)" },
];

const CONFIG: ChartConfig = Object.fromEntries(
  BANDS.map(band => [band.key, { label: band.label, color: band.color }]),
);

/** One hour of log Volume in 2-minute Buckets, shaped like a real incident. */
const BUCKETS = Array.from({ length: 30 }, (_, i) => {
  const spike = i > 17 && i < 24;
  return {
    start: `13:${(i * 2).toString().padStart(2, "0")}`,
    error: spike ? 18 + ((i * 7) % 11) : (i * 3) % 4,
    warn: spike ? 26 + ((i * 5) % 9) : 3 + ((i * 2) % 5),
    info: 60 + ((i * 13) % 25),
    debug: 140 + ((i * 29) % 60),
  };
});

const SILENT = BUCKETS.map(bucket => ({ ...bucket, error: 0, warn: 0, info: 0, debug: 0 }));

function Panel(props: {
  label: string;
  bands: typeof BANDS;
  data: typeof BUCKETS;
  dimEdges?: boolean;
}) {
  return (
    <div className="flex w-full flex-col gap-2">
      {/* The legend lives in the heading row, not inside the plot box, so it costs no plot height. */}
      <div className="flex items-center justify-between">
        <span className="text-muted-foreground font-mono text-xs">{props.label}</span>
        <div className="flex items-center gap-3">
          {props.bands.map(band => (
            <span key={band.key} className="flex items-center gap-1">
              <span className="rounded-sm" style={{ width: 8, height: 8, background: band.color }} />
              <span className="text-muted-foreground font-mono text-xs">{band.label}</span>
            </span>
          ))}
        </div>
      </div>
      <Chart config={CONFIG} className="h-56 w-full">
        <BarChart data={props.data} barCategoryGap={2}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="start" tickLine={false} axisLine={false} minTickGap={48} />
          <YAxis width={38} tickLine={false} axisLine={false} tickCount={3} />
          <ChartTooltip content={<ChartTooltipContent />} />
          {props.bands.map(band => (
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
              {props.data.map((bucket, index) => (
                <Cell
                  key={bucket.start}
                  fillOpacity={
                    props.dimEdges === true && (index === 0 || index === props.data.length - 1) ? 0.45 : 1
                  }
                />
              ))}
            </Bar>
          ))}
        </BarChart>
      </Chart>
    </div>
  );
}

/**
 * The Volume of matched log records over time, split by Severity Band. The dimmed bars at each end
 * are the Buckets the window cut short — they render lower through no fault of the traffic.
 */
export const LogVolume = () => (
  <Panel label="13:00 → 14:00 · 2 min buckets" bands={BANDS} data={BUCKETS} dimEdges />
);

/**
 * The same chart under a minimum-severity threshold. Bands the threshold excludes are empty by
 * construction, so they get neither a stack segment nor a legend entry.
 */
export const OneBandOnly = () => (
  <Panel
    label="13:00 → 14:00 · 2 min buckets · error and above"
    bands={BANDS.slice(0, 1)}
    data={BUCKETS}
    dimEdges
  />
);

/** Nothing matched. The axis stays, because the window is still the one that was asked for. */
export const Silent = () => (
  <Panel label="13:00 → 14:00 · 2 min buckets" bands={BANDS} data={SILENT} />
);
