# Bugler UI — build conventions

Bugler is a log/trace observability tool. Its UI kit is six shadcn-style React primitives over Radix + Tailwind v4: `Button`, `Card` (+ `CardHeader`, `CardTitle`, `CardDescription`, `CardAction`, `CardContent`, `CardFooter`), `Input`, `Label`, `Select` (+ `SelectTrigger`, `SelectValue`, `SelectContent`, `SelectGroup`, `SelectLabel`, `SelectItem`, `SelectSeparator`), `Textarea`.

## Setup

No provider or theme wrapper is needed — theming is pure CSS. Load the DS stylesheet (`styles.css`); it styles `body` with `bg-background text-foreground` automatically. Light theme is the default; for dark UI put `class="dark"` on a root element — every token flips.

## Styling idiom

Style with Tailwind utility classes, **but the stylesheet is compiled, not JIT** — only classes already in `_ds_bundle.css` resolve. Stay inside this vocabulary:

- **Semantic colors** (always prefer these): `bg-background`, `bg-card`, `bg-popover`, `bg-primary`, `bg-secondary`, `bg-muted`, `bg-accent`, `bg-destructive`, `text-foreground`, `text-muted-foreground`, `text-primary`, `text-destructive`, `text-card-foreground`, `border-input`, `border` (uses `--border`).
- **Layout**: `flex`, `flex-col`, `flex-wrap`, `flex-1`, `grid`, `grid-cols-2`, `items-center`, `justify-between`, `justify-end`, `w-full`, `w-fit`, `w-44`, `w-56`, `max-w-sm`, `max-w-md`, `max-w-lg`, `min-h-16`, `h-full`.
- **Spacing is gap-based**: `gap-1` … `gap-6`, `p-1` … `p-6`, `px-2`…`px-6`, `py-1`…`py-8`. Margin utilities are essentially absent — compose with flex/grid + gap, never `mt-*`/`space-y-*`.
- **Type**: `text-xs`, `text-sm`, `text-base`, `text-lg`, `text-3xl`, `font-normal`, `font-medium`, `font-semibold`, `font-bold`, `font-mono`, `leading-none`. Faces are IBM Plex Sans (UI) and IBM Plex Mono — loaded automatically by `styles.css` (Google Fonts import), no setup needed. **Bugler renders every value, id, timestamp, and duration in mono**: use `font-mono`, or the native `code`/`kbd`/`samp`/`pre`/`time` elements (and `data-mono` attribute), which get the mono face automatically.
- **Chrome**: `rounded-sm`, `rounded-md`, `rounded-xl`, `shadow-xs`, `shadow-sm`, `shadow-md`, `border-b`, `border-t`.

Need something outside this set? Use the CSS variables inline: `style={{ background: "var(--sidebar)", borderRadius: "var(--radius)" }}`. Tokens: `--background`, `--foreground`, `--card`, `--popover`, `--primary`, `--secondary`, `--muted`, `--accent`, `--destructive` (each with `-foreground` where sensible), `--border`, `--input`, `--ring`, `--chart-1`…`--chart-5`, `--sidebar` family, and `--radius` (the base radius; use `rounded-sm/md/lg/xl` utilities for derived radii — the `--radius-*` variables are compile-time only and don't exist at runtime).

## Recurring compositions

- **Form field**: `<div className="grid gap-2"><Label htmlFor="x">…</Label><Input id="x" … /></div>`; error state = `aria-invalid` on the control + `<p className="text-destructive text-sm">…`.
- **Filter bar**: row of `flex items-center gap-2` with small selects — `<SelectTrigger className="w-44" size="sm">`.
- **Dashboard**: cards in `grid gap-4 grid-cols-2`; stat tile = `CardDescription` label over a `CardTitle className="text-3xl"` value.

## Where the truth lives

`styles.css` → `@import "_ds_bundle.css"` holds every token (`:root` / `.dark`) and every compiled utility — read it before inventing a class. Each component's API is `components/<group>/<Name>/<Name>.d.ts`; usage judgment and composition examples are in the sibling `<Name>.prompt.md`.

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
