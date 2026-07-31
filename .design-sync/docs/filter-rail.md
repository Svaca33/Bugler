---
category: Layout
---

The left rail of an explorer page: every filter the list understands, stacked and grouped by kind, with one Clear all. It is 254px wide by its own decision and fills whatever height its parent row gives it; nothing in it scrolls sideways.

## Usage

- `FilterRail` is the shell — a `Filters` header with a `Clear all` button, and a scrolling body. `FilterGroup` labels one kind of filter; its children are the controls that narrow by it:

```tsx
<div className="flex h-full">
  <FilterRail canClear={anyFilterSet} onClear={resetToDefaults}>
    <FilterGroup label="SOURCE">
      <FilterSelect className="w-full" placeholder="All services" … />
    </FilterGroup>
    <FilterGroup label="MESSAGE">
      <form onSubmit={apply}><Input placeholder="Search in message…" /></form>
    </FilterGroup>
  </FilterRail>
  <main className="min-w-0 flex-1">…the list…</main>
</div>
```

- Group labels are terse and uppercase — the caption style is built into `FilterGroup`, so pass `label="SOURCE"`, not a styled node.
- Every control inside goes full width (`className="w-full"` on selects); checkbox rows are `flex items-center gap-2` with the count right-aligned in `font-mono text-xs`.
- `canClear` should mean "any control deviates from the default view", and `onClear` should return to those defaults — not necessarily to emptiness. A rail whose Clear all empties a view the page always narrows (a default time window, a default state set) surprises the reader.
- The rail is an `aside` element: stand it first in a `flex h-full` row and let the list take `flex-1 min-w-0`.
- **Its chrome is tuned for the dark theme** — the navy console the app pins itself to. Use it under a `class="dark"` root; on the light theme its foreground text sits dark-on-navy.
