---
category: Forms
---

Searchable option picker: an input that filters a dropdown list as you type; picking an option fires `onSelect` and clears the input. Built for pick-one-from-many flows (Bugler uses it to choose an attribute key when building a filter). Not a value holder — unlike `Select` it does not display a chosen value; the consumer reacts to `onSelect`.

```tsx
<Combobox
  options={[
    { value: "tenant.id", label: "tenant.id", group: "Attributes" },
    { value: "service.name", label: "service.name", group: "Resource" },
  ]}
  placeholder="Filter by key…"
  onSelect={value => addFilter(value)}
/>
```

## Behavior

- `options`: `{ value, label, group? }[]` — `label` is shown and matched (case-insensitive substring); `value` is what `onSelect` receives. Consecutive options sharing a `group` get a muted group heading.
- Opens on focus or typing; arrow keys move the active row, Enter selects, Escape closes, blur closes (and fires `onBlur` if given).
- `emptyText` renders when nothing matches (default "No matches."); also the loading placeholder while options are fetched.
- `autoFocus` opens the list immediately — useful when the combobox appears on demand (e.g. after an "add filter" click).

## Judgment

- Give it an explicit width (`w-64`); the dropdown matches the input width.
- The dropdown is absolutely positioned (not a portal) — leave vertical room below in constrained layouts.
- For pick-then-configure flows, swap the combobox for the next control after `onSelect` rather than leaving it mounted.
