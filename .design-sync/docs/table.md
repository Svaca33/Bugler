---
category: Data
---

The kit's table skeleton: `Table` (which brings its own horizontal-scroll container), `TableHeader`, `TableBody`, `TableRow`, `TableHead`, `TableCell`. Purely presentational — for sortable tables compose them through `DataTable` instead of wiring state by hand.

## Usage

- **Dark-committed chrome** like `FilterRail`: the heading strip and hairlines are navy hexes tuned for the console theme, so render under a `class="dark"` root.
- Wrap in the app's table frame: `<div className="overflow-hidden rounded-[11px] border border-[#1E344C] bg-card">…</div>` — the wrapper clips the heading strip to the rounded corner.
- Headings are authored UPPERCASE (the strip is 10px mono with tracking, styling does not transform case).
- Alignment and edge padding ride on the cells: numeric columns take `text-right` on both `TableHead` and `TableCell`; the first column takes `pl-4`, the last `pr-4`, so content clears the frame.
- One column should carry `w-full` (usually the name column) — it absorbs the free width and keeps the value columns snug.
- `TableCell` renders mono automatically; no `font-mono` needed on values.
