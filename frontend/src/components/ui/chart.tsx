import * as React from "react";
import type { ReactNode } from "react";
import {
  Legend,
  ResponsiveContainer,
  Tooltip,
  type LegendPayload,
  type TooltipContentProps,
} from "recharts";

import { cn } from "@/lib/utils";

/**
 * The shadcn chart wrapper over Recharts, adapted to Recharts 3 and Tailwind v4.
 *
 * Series colours travel as a config rather than as props on each mark: the container carries one
 * `--color-<key>` custom property per series and the marks reference those. That keeps chart
 * colours in the token layer with every other colour in the kit instead of as hexes sprinkled
 * through feature code, and it is what lets a series be recoloured for dark mode without the chart
 * knowing which mode it is in — a `color` here is a `var(--…)`, and the token underneath it is what
 * answers to the theme.
 *
 * Upstream shadcn writes these properties through a `<style>` element built by interpolating the
 * config into a CSS string, with a `theme` branch on each entry naming a light and a dark colour.
 * Both are gone. The properties are set through React's `style` prop, which reaches the CSSOM and
 * so never composes markup out of a key that a monitored application chose (ADR 0022); the `theme`
 * branch went with the element that was its only reason to exist, unused here because the token
 * layer already resolves light and dark one level further down.
 */
export type ChartConfig = {
  [key: string]: {
    label?: React.ReactNode;
    icon?: React.ComponentType;
    color?: string;
  };
};

const ChartContext = React.createContext<{ config: ChartConfig } | null>(null);

function useChart() {
  const context = React.useContext(ChartContext);
  if (context === null) {
    throw new Error("useChart must be used within a <Chart />");
  }
  return context;
}

/**
 * The root of a chart. Named `Chart` because that is the primitive the design system documents —
 * `ChartContainer` is exported alongside it as the name shadcn generates, so regenerating this file
 * from upstream keeps working code working.
 */
function Chart({
  className,
  children,
  config,
  style,
  ...props
}: React.ComponentProps<"div"> & {
  config: ChartConfig;
  children: React.ComponentProps<typeof ResponsiveContainer>["children"];
}) {
  return (
    <ChartContext.Provider value={{ config }}>
      <div
        data-slot="chart"
        className={cn(
          "flex justify-center text-xs",
          "[&_.recharts-cartesian-axis-tick_text]:fill-muted-foreground",
          "[&_.recharts-cartesian-grid_line]:stroke-border/40",
          "[&_.recharts-layer]:outline-none [&_.recharts-surface]:outline-none",
          className,
        )}
        // A caller's own style wins: these are defaults the config supplies, not the chart's say.
        style={{ ...seriesColours(config), ...style }}
        {...props}
      >
        <ResponsiveContainer>{children}</ResponsiveContainer>
      </div>
    </ChartContext.Provider>
  );
}

/**
 * The `--color-<key>` custom property each configured series is drawn with. Inherited by everything
 * inside the container, so a mark asks for `var(--color-error)` without knowing which chart it is in
 * — and two charts on one screen can give the same series name a different colour.
 */
function seriesColours(config: ChartConfig): React.CSSProperties {
  // Custom properties are not in React's CSSProperties, which knows only the named ones.
  return Object.fromEntries(
    Object.entries(config)
      .filter(([, item]) => item.color !== undefined)
      .map(([key, item]) => [`--color-${key}`, item.color]),
  ) as React.CSSProperties;
}

const ChartTooltip = Tooltip;

function ChartTooltipContent({
  active,
  payload,
  label,
  labelFormatter,
  formatter,
  className,
  hideLabel = false,
  hideIndicator = false,
  nameKey,
}: // Partial: Recharts injects `active`/`payload`/`coordinate` by cloning the element passed to
// `content`, so at the call site none of them are supplied.
Partial<Omit<TooltipContentProps<number, string>, "formatter">> & {
  className?: string;
  hideLabel?: boolean;
  hideIndicator?: boolean;
  nameKey?: string;
  formatter?: (value: number | undefined, name: string | undefined) => ReactNode;
}) {
  const { config } = useChart();

  if (active !== true || payload === undefined || payload.length === 0) {
    return null;
  }

  return (
    <div
      data-slot="chart-tooltip"
      className={cn(
        "border-border/50 bg-popover grid min-w-[9rem] items-start gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs shadow-xl",
        className,
      )}
    >
      {!hideLabel && (
        <div className="font-medium">{labelFormatter ? labelFormatter(label, payload) : label}</div>
      )}
      <div className="grid gap-1.5">
        {payload.map(item => {
          const key = `${nameKey ?? item.name ?? item.dataKey ?? "value"}`;
          const itemConfig = config[key];
          return (
            <div key={key} className="flex w-full items-center gap-2">
              {!hideIndicator && (
                <div
                  className="size-2.5 shrink-0 rounded-[2px]"
                  style={{ backgroundColor: item.color ?? `var(--color-${key})` }}
                />
              )}
              <div className="flex flex-1 items-center justify-between gap-3 leading-none">
                <span className="text-muted-foreground">{itemConfig?.label ?? item.name}</span>
                <span className="text-foreground font-mono font-medium tabular-nums">
                  {formatter !== undefined
                    ? // Recharts types a payload value as string | number | array; only a number
                      // is a measure, and anything else has nothing to format.
                      formatter(typeof item.value === "number" ? item.value : undefined, key)
                    : (item.value?.toLocaleString() ?? "—")}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

const ChartLegend = Legend;

function ChartLegendContent({
  payload,
  verticalAlign = "bottom",
  className,
  nameKey,
}: {
  payload?: readonly LegendPayload[];
  verticalAlign?: "top" | "middle" | "bottom";
  className?: string;
  nameKey?: string;
}) {
  const { config } = useChart();

  if (payload === undefined || payload.length === 0) {
    return null;
  }

  return (
    <div
      data-slot="chart-legend"
      className={cn(
        "flex items-center justify-center gap-4",
        verticalAlign === "top" ? "pb-3" : "pt-3",
        className,
      )}
    >
      {payload.map(item => {
        const key = `${nameKey ?? item.dataKey ?? item.value}`;
        return (
          <div key={key} className="flex items-center gap-1.5">
            <div className="size-2 shrink-0 rounded-[2px]" style={{ backgroundColor: item.color }} />
            <span className="text-muted-foreground">{config[key]?.label ?? item.value}</span>
          </div>
        );
      })}
    </div>
  );
}

/**
 * The Recharts marks a Bugler chart is built from, re-exported so a chart composes from one import
 * surface. Callers never reach for `recharts` directly: the kit is what the design system documents
 * and what its previews can import, and a mark that arrived from somewhere else would be neither.
 * Add to this list as charts need marks — it is deliberately not the whole library.
 */
export { Bar, BarChart, CartesianGrid, Cell, ReferenceArea, ReferenceLine, XAxis, YAxis } from "recharts";

export {
  Chart,
  Chart as ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
};
