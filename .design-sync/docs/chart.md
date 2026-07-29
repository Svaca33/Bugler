---
category: Data
---

Recharts wrapper for Bugler's charts. `Chart` supplies the responsive box and turns a `ChartConfig` into one `--color-<series>` custom property per series, scoped to that chart; the marks then reference `var(--color-<series>)` and never carry a hex. Bugler renders the log Volume above the Explore log list with it.

```tsx
const config = {
  error: { label: "Error", color: "var(--severity-error-fill)" },
  warn: { label: "Warn", color: "var(--severity-warn-fill)" },
} satisfies ChartConfig;

<Chart config={config} className="h-56 w-full">
  <BarChart data={buckets} barCategoryGap={2}>
    <CartesianGrid vertical={false} />
    <XAxis dataKey="start" tickLine={false} axisLine={false} />
    <YAxis width={38} tickLine={false} axisLine={false} tickCount={3} />
    <ChartTooltip content={<ChartTooltipContent />} />
    <Bar dataKey="error" stackId="v" fill="var(--color-error)" isAnimationActive={false} />
    <Bar dataKey="warn" stackId="v" fill="var(--color-warn)" isAnimationActive={false} />
  </BarChart>
</Chart>
```

## Behavior

- `config` keys must match the `dataKey` of the marks — that is what binds a series to its colour and its legend label.
- `Chart` has no intrinsic height. Give it one (`className="h-56 w-full"`) or it collapses and nothing renders.
- The marks (`BarChart`, `Bar`, `XAxis`, `YAxis`, `CartesianGrid`, `Cell`, `ReferenceArea`) are re-exported from the kit. Import them from here, not from `recharts`.
- `ChartTooltipContent` reads the container's config for labels; `labelFormatter` overrides the heading, and `<Cell>` inside a `Bar` varies one bucket's appearance without splitting the series.
- `ChartLegend`/`ChartLegendContent` render Recharts' own legend inside the plot box. A legend placed in a heading row above the chart is usually better — it costs no plot height.
- `ChartContainer` is exported as an alias of `Chart`; it is the name shadcn generates, kept so regenerated code keeps working. Write `Chart`.

## Judgment

- **Series colours are semantic tokens, never `--chart-1..5` when the series means something.** The numbered ramp is the generic categorical palette; severity wears `--severity-*-fill`, because repainting the ramp for a dashboard must not repaint the logs UI.
- **`isAnimationActive={false}` on anything that refetches.** A chart that re-animates every few seconds cannot be read while it moves.
- Stack the series a reader must compare over time **at the baseline** — a segment floating on top of a variable stack has no comparable shape.
- Keep axes recessive: `tickLine={false} axisLine={false}`, two or three y-ticks, horizontal grid only.
- Charts are dense: 10px ticks and mono labels, and give ticks `minTickGap` room when their labels carry a date as well as a time.
