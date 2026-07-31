# Bugler UI — build conventions

Bugler is a log/trace observability tool. Its UI kit is fifteen shadcn-style React primitives over Radix + Tailwind v4: `Button`, `Card` (+ `CardHeader`, `CardTitle`, `CardDescription`, `CardAction`, `CardContent`, `CardFooter`), `Chart` (+ `ChartTooltip`, `ChartTooltipContent`, `ChartLegend`, `ChartLegendContent`, and the re-exported Recharts marks `BarChart`, `Bar`, `XAxis`, `YAxis`, `CartesianGrid`, `Cell`, `ReferenceArea`), `Checkbox`, `Combobox` (searchable option picker), `DetailPanel` (the shared list-beside-detail chrome: divider, title row, close button, remembered width), `Dialog` (+ `DialogTrigger`, `DialogContent`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogFooter`, `DialogClose`), `FilterChip` (removable filter tag), `FilterRail` (+ `FilterGroup` — the 254px left rail of an explorer page, one Clear all), `FilterSelect` (nullable single-select whose placeholder row clears the filter), `Input`, `Label`, `Resizable` (also exported as `ResizablePanelGroup`; + `ResizablePanel`, `ResizableHandle` — panels the reader re-proportions by dragging), `Select` (+ `SelectTrigger`, `SelectValue`, `SelectContent`, `SelectGroup`, `SelectLabel`, `SelectItem`, `SelectSeparator`), `Textarea`.

## Setup

No provider or theme wrapper is needed — theming is pure CSS. Load the DS stylesheet (`styles.css`); it styles `body` with `bg-background text-foreground` automatically. Light theme is the default; for dark UI put `class="dark"` on a root element — every token flips.

## Styling idiom

Style with Tailwind utility classes, **but the stylesheet is compiled, not JIT** — only classes already in `_ds_bundle.css` resolve. Stay inside this vocabulary:

- **Semantic colors** (always prefer these): `bg-background`, `bg-card`, `bg-popover`, `bg-primary`, `bg-secondary`, `bg-muted`, `bg-accent`, `bg-destructive`, `text-foreground`, `text-muted-foreground`, `text-primary`, `text-destructive`, `text-card-foreground`, `border-input`, `border` (uses `--border`).
- **Layout**: `flex`, `flex-col`, `flex-wrap`, `flex-1`, `grid`, `grid-cols-2`, `items-center`, `justify-between`, `justify-end`, `w-full`, `w-fit`, `w-44`, `w-56`, `max-w-sm`, `max-w-md`, `max-w-lg`, `min-h-16`, `h-full`, `h-56` (a chart box has no intrinsic height).
- **Spacing is gap-based**: `gap-1` … `gap-6`, `p-1` … `p-6`, `px-2`…`px-6`, `py-1`…`py-8`. Margin utilities are essentially absent — compose with flex/grid + gap, never `mt-*`/`space-y-*`.
- **Type**: `text-xs`, `text-sm`, `text-base`, `text-lg`, `text-3xl`, `font-normal`, `font-medium`, `font-semibold`, `font-bold`, `font-mono`, `leading-none`. Faces are IBM Plex Sans (UI) and IBM Plex Mono — loaded automatically by `styles.css` (Google Fonts import), no setup needed. **Bugler renders every value, id, timestamp, and duration in mono**: use `font-mono`, or the native `code`/`kbd`/`samp`/`pre`/`time` elements (and `data-mono` attribute), which get the mono face automatically.
- **Chrome**: `rounded-sm`, `rounded-md`, `rounded-xl`, `shadow-xs`, `shadow-sm`, `shadow-md`, `border-b`, `border-t`.

Need something outside this set? Use the CSS variables inline: `style={{ background: "var(--sidebar)", borderRadius: "var(--radius)" }}`. Tokens: `--background`, `--foreground`, `--card`, `--popover`, `--primary`, `--secondary`, `--muted`, `--accent`, `--destructive` (each with `-foreground` where sensible), `--border`, `--input`, `--ring`, `--chart-1`…`--chart-5`, the severity family, `--state-solved` (below), `--sidebar` family, and `--radius` (the base radius; use `rounded-sm/md/lg/xl` utilities for derived radii — the `--radius-*` variables are compile-time only and don't exist at runtime).

## Severity colours

A log record's severity collapses into four **Severity Bands** — Error (which carries FATAL), Warn, Info, Debug (everything lower, including telemetry that declared no severity). Each Band has three roles, and they are not interchangeable:

- `text-severity-error` / `-warn` / `-info` / `-debug` — the label, tuned for readable text.
- `bg-severity-error-rail` / `-warn-rail` / `-info-rail` / `-debug-rail` — the 3px sliver at the left edge of a log row.
- `var(--severity-error-fill)` and siblings — **CSS variables, not utility classes**: filled areas in a chart. The rail steps are tuned to read as a sliver and disappear the moment they are asked to fill anything, which is why the fill is a separate, lighter step. Debug's fill is deliberately neutral — an undeclared severity is not a hue.

Never colour severity from `--chart-1`…`--chart-5`: that ramp is the generic categorical palette for dashboards, and retuning it must not repaint the logs UI.

Beside the severity bands sits one success green: `text-state-solved` / `bg-state-solved` (`--state-solved`) — the verdict that trouble was fixed by hand. It is not a severity, and it is not `--primary`: brass means "brand / interactive", never "good news".

## Recurring compositions

- **Form field**: `<div className="grid gap-2"><Label htmlFor="x">…</Label><Input id="x" … /></div>`; error state = `aria-invalid` on the control + `<p className="text-destructive text-sm">…`.
- **Checkbox row**: `<div className="flex items-center gap-2"><Checkbox id="x" /><Label htmlFor="x" className="font-normal">…</Label></div>` — horizontal, unlike the field idiom above, and the label drops to `font-normal` because the caption is a sentence. Stack a set with `grid gap-2` under a group `Label`.
- **Filter bar**: row of `flex items-center gap-2` with small selects — `<SelectTrigger className="w-44" size="sm">`; active criteria render as `FilterChip`s in the same row, added via a `Combobox` key picker.
- **Dashboard**: cards in `grid gap-4 grid-cols-2`; stat tile = `CardDescription` label over a `CardTitle className="text-3xl"` value.
- **Chart**: `<Chart config={…} className="h-56 w-full">` around a Recharts chart; series colours come from `config` as `var(--color-<key>)`, axes go recessive (`tickLine={false} axisLine={false}`, two or three y-ticks, `<CartesianGrid vertical={false} />`), and anything that refetches sets `isAnimationActive={false}`. Put the legend in a heading row above the chart rather than inside the plot box — it costs no plot height.
- **List + detail split**: prefer `DetailPanel` — `<ResizablePanelGroup className="h-full">` around a list `ResizablePanel` and a conditionally-rendered `<DetailPanel title={…} onClose={…}>`; it brings its own divider, close button and remembered width, and must be a direct child of the group, after the list. Drop to bare panels only for splits that are not list-and-detail: both panels carry a px `minSize`, the detail's `defaultSize` is a number (pixels — a bare string would mean percent), and panels and handles must be direct children of the group.
- **Modal**: `<Dialog open onOpenChange={…}><DialogContent>` wrapping `DialogHeader` (title + description), the body, then `DialogFooter` with cancel first and the confirming `Button` last. `DialogContent` is `sm:max-w-lg`; narrow it with `sm:max-w-md`. Destructive confirmations name what is lost in `DialogDescription` and gate the `variant="destructive"` button behind a typed confirmation.

## Where the truth lives

`styles.css` → `@import "_ds_bundle.css"` holds every token (`:root` / `.dark`) and every compiled utility — read it before inventing a class. `tokens/tokens.css` is the token set alone (with the design-rationale comments); custom properties that appear only in `_ds_bundle.css` — `--tw-*`, `--animate-*`, `--default-*` — are Tailwind plumbing, not tokens. Each component's API is `components/<group>/<Name>/<Name>.d.ts`; usage judgment and composition examples are in the sibling `<Name>.prompt.md`.

## Example

```tsx
<Card className="w-full max-w-md">
  <CardHeader>
    <CardTitle>Purge logs</CardTitle>
    <CardDescription>Removes records older than the retention window</CardDescription>
  </CardHeader>
  <CardContent className="grid gap-2">
    <Label htmlFor="reason">Reason</Label>
    <Textarea id="reason" placeholder="Required for the audit trail" />
  </CardContent>
  <CardFooter className="justify-end gap-2">
    <Button variant="outline">Cancel</Button>
    <Button variant="destructive">Purge</Button>
  </CardFooter>
</Card>
```
