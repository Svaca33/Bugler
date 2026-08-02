---
category: Data
---

A sortable table over the `Table` parts (TanStack Table under the hood, invisible by design). Pages supply column definitions and rows; `DataTable` owns the sort state, renders the clickable headings and direction arrows, and sets `aria-sort`.

## Usage

- A column definition is `{ id, accessorFn, header, sortDescFirst, meta, cell }`: `accessorFn` yields the sort key, `cell` renders the content, `meta.headerClassName` / `meta.cellClassName` carry alignment and edge padding (see the `Table` doc), `meta.headerTitle` puts a tooltip on the heading.
- `sortDescFirst: true` for numeric columns (a first click answers "which is biggest"), `false` for text.
- `defaultSort` (e.g. `[{ id: "logs", desc: true }]`) is always in force — there is no unsorted state, a third click circles back. One column sorts at a time.
- Ties keep the incoming row order, so pass rows deterministically ordered.
- A custom comparator goes in `sortingFn(rowA, rowB)` on the column — compare via `row.original`.
- `persistKey` remembers the reader's chosen sort in localStorage under a `bugler.<area>.<thing>` key; leave it off in previews and ephemeral views.
- Dark-committed like the `Table` parts; wrap in the same rounded frame under a `class="dark"` root.
