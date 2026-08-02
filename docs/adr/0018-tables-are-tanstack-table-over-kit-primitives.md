---
status: accepted
---

# Tables are TanStack Table over kit primitives

Sortable tables in the UI are built from two kit layers: `components/ui/table.tsx`, presentational
parts wearing the app's navy-console table look, and `components/ui/data-table.tsx`, a `DataTable`
that owns row modelling and sorting through TanStack Table (headless, v8). Pages supply column
definitions — accessor, sort key, alignment — and rows; they do not touch table state. The Storage
admin view is the first consumer; existing hand-rolled tables (Overview's ServiceTable) migrate as
they come to need sorting.

Rejected: hand-rolling sort state per page (the behaviour — toggle direction, indicators,
remembered choice — is exactly the part that would be copied around), and a styled table library
(the look is already ours; only the mechanics were missing). TanStack Table stays a mechanics
dependency: nothing visual may come from it, so it can be swapped without a redesign.

## Consequences

- Sorting is client-side, over rows a page already holds. A table over server-paged data would
  need its sorting pushed into the query — a different design, out of `DataTable`'s scope until
  a consumer forces it.
- `DataTable` deliberately has no unsorted state: a default order is always in force, and ties
  keep the incoming row order, so pages pass rows deterministically ordered.
- A table's sort can be remembered per reader (`persistKey` → `bugler.<area>.<thing>` in
  localStorage, the same pattern as the dashboard board and detail width); a remembered column
  that no longer exists falls back to the default, never guesses.
- Features gather on demand: pagination, filtering and selection are absent until a page needs
  them, and grow inside `DataTable` rather than per page.
